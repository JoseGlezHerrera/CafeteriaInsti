using System.Data;
using Microsoft.Data.SqlClient;
using CafeIES.API.Data;
using CafeIES.API.Extensions;
using CafeIES.API.Hubs;
using CafeIES.API.Services;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("general")]
public class PedidosController : ControllerBase
{
    private readonly AppDbContext    _db;
    private readonly HorarioService  _horario;
    private readonly StripeService   _stripe;
    private readonly IHubContext<CafeteriaHub> _hub;
    private readonly FcmService      _fcm;
    private readonly ILogger<PedidosController> _logger;
    private readonly DesayunoService _desayuno;

    public PedidosController(AppDbContext db, HorarioService horario, StripeService stripe,
        IHubContext<CafeteriaHub> hub, FcmService fcm, ILogger<PedidosController> logger,
        DesayunoService desayuno)
    {
        _db       = db;
        _horario  = horario;
        _stripe   = stripe;
        _hub      = hub;
        _fcm      = fcm;
        _logger   = logger;
        _desayuno = desayuno;
    }

    // ── GET /api/pedidos/desayuno-status ────────────────────────────────────
    /// <summary>
    /// Devuelve si el usuario es beneficiario del desayuno gratuito y qué
    /// componentes quedan disponibles para hoy.
    /// </summary>
    [HttpGet("desayuno-status")]
    public async Task<ActionResult<DesayunoStatusDto>> DesayunoStatus()
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var usuario = await _db.Usuarios.FindAsync(userId.Value);
        if (usuario is null) return Unauthorized();

        if (!usuario.DesayunoGratuito)
            return Ok(new DesayunoStatusDto(false, false, false));

        var hoy = DesayunoService.HoyEspaña();

        var consumo = await _db.ConsumoDesayunos
            .FirstOrDefaultAsync(c => c.UsuarioId == userId.Value && c.Fecha == hoy);

        return Ok(new DesayunoStatusDto(
            TieneDesayunoGratuito: true,
            ZumoDisponible:   consumo is null || !consumo.ZumoConsumido,
            BocataDisponible: consumo is null || !consumo.BocataConsumido
        ));
    }

    // ── GET /api/pedidos/puedo-pedir ─────────────────────────────────────────
    /// <summary>La app consulta esto al abrir la pantalla para mostrar/ocultar el banner.</summary>
    [HttpGet("puedo-pedir")]
    public async Task<ActionResult<HorarioStatusDto>> PuedoPedir()
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _horario.PuedePedirAhoraAsync(userId.Value);

        return Ok(new HorarioStatusDto(
            result.Puede,
            result.Mensaje,
            result.ProximaFranja?.Descripcion,
            result.ProximaFranja?.HoraInicio
        ));
    }

    // ── POST /api/pedidos ────────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<PedidoDto>> Crear([FromBody] CrearPedidoRequest req)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        // FIX-05: Verificar que el usuario no esté suspendido/rechazado
        var usuario = await _db.Usuarios.FindAsync(userId.Value);
        if (usuario is null) return Unauthorized();
        if (usuario.Estado != EstadoCuenta.Activa)
            return StatusCode(403, new { mensaje = "Tu cuenta no está activa. No puedes realizar pedidos." });

        // 1. Comprobar horario:
        //    - Tarjeta (Stripe): se omite; la ventana ya fue validada al crear el PaymentIntent —
        //      revocar ahora dejaría al usuario cobrado sin pedido.
        //    - Gratuito (desayuno) y Efectivo: sí se valida. El desayuno se recoge en el recreo,
        //      fuera del horario de clase, igual que cualquier otro pedido. Decisión de negocio intencional.
        if (string.IsNullOrEmpty(req.StripePaymentIntentId))
        {
            var horario = await _horario.PuedePedirAhoraAsync(userId.Value);
            if (!horario.Puede)
                return BadRequest(new { mensaje = horario.Mensaje });
        }

        // Validar método de pago
        if (!Enum.IsDefined(typeof(MetodoPago), req.MetodoPago))
            return BadRequest(new { mensaje = "Método de pago inválido." });

        // Para pagos con tarjeta (Stripe) se debe proporcionar el PaymentIntentId
        if (req.MetodoPago == MetodoPago.Tarjeta && string.IsNullOrEmpty(req.StripePaymentIntentId))
            return BadRequest(new { mensaje = "Se requiere un identificador de pago de Stripe para pagos con tarjeta." });

        // Pago gratuito solo permitido a beneficiarios del programa de desayuno
        if (req.MetodoPago == MetodoPago.Gratuito && !usuario.DesayunoGratuito)
            return StatusCode(403, new { mensaje = "No tienes acceso al desayuno gratuito." });

        // FIX-03: Serializable para evitar race condition en NumeroPedido
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // 2. Calcular total y validar stock
            var lineas = new List<LineaPedido>();
            decimal total = 0;

            // Cargar o crear consumo de desayuno del día (solo si es beneficiario)
            var consumoDesayuno = await _desayuno.ObtenerOCrearConsumoHoyAsync(userId.Value, usuario.DesayunoGratuito);

            // Cargar todos los productos del carrito en una sola query (evita N round-trips a SQL)
            var productoIds = req.Lineas.Select(l => l.ProductoId).ToHashSet();
            var productos = await _db.Productos
                .Include(p => p.ProductoIngredientes)
                .Where(p => productoIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            // Batch-cargar todos los ingredientes referenciados en el pedido
            var todosIngredienteIds = req.Lineas
                .Where(l => l.Ingredientes is { Count: > 0 })
                .SelectMany(l => l.Ingredientes!.Select(i => i.IngredienteId))
                .ToHashSet();
            var ingredientesDict = todosIngredienteIds.Count > 0
                ? await _db.Ingredientes
                    .Where(i => todosIngredienteIds.Contains(i.Id) && i.Activo)
                    .ToDictionaryAsync(i => i.Id)
                : new Dictionary<int, Ingrediente>();

            foreach (var l in req.Lineas)
            {
                if (!productos.TryGetValue(l.ProductoId, out var producto) || !producto.Activo)
                    return BadRequest(new { mensaje = $"Producto #{l.ProductoId} no disponible." });

                if (producto.Stock != -1 && producto.Stock < l.Cantidad)
                    return BadRequest(new { mensaje = $"Stock insuficiente para '{producto.Nombre}'. Disponibles: {producto.Stock}." });

                // Decrementar stock inmediatamente para evitar doble lectura
                if (producto.Stock != -1) producto.Stock -= l.Cantidad;

                // ── Ingredientes personalizables ───────────────────────────────
                // Cada modificación es (IngredienteId, Cantidad, Accion, PrecioUnitario)
                var modificaciones = new List<(int IngId, int Cantidad, AccionIngrediente Accion, decimal Precio)>();
                decimal extraPorUnidad = 0;

                if (l.Ingredientes is { Count: > 0 })
                {
                    var permitidos = producto.ProductoIngredientes.ToDictionary(pi => pi.IngredienteId);

                    foreach (var ir in l.Ingredientes)
                    {
                        if (!ingredientesDict.TryGetValue(ir.IngredienteId, out var ingrediente))
                            return BadRequest(new { mensaje = $"Ingrediente #{ir.IngredienteId} no válido o no activo." });

                        if (!permitidos.TryGetValue(ir.IngredienteId, out var cfg))
                            return BadRequest(new { mensaje = $"El ingrediente '{ingrediente.Nombre}' no pertenece al producto '{producto.Nombre}'." });

                        var cantIngrediente = ir.Cantidad;

                        if (ir.Accion == AccionIngrediente.Quitar)
                        {
                            if (!cfg.EsBase || !cfg.EsQuitable)
                                return BadRequest(new { mensaje = $"El ingrediente '{ingrediente.Nombre}' no se puede quitar de este producto." });
                            modificaciones.Add((ir.IngredienteId, 1, ir.Accion, 0m));
                        }
                        else // Añadir
                        {
                            var stockNecesario = l.Cantidad * cantIngrediente;
                            if (ingrediente.Stock != -1 && ingrediente.Stock < stockNecesario)
                                return BadRequest(new { mensaje = $"Stock insuficiente del ingrediente '{ingrediente.Nombre}'." });

                            if (ingrediente.Stock != -1) ingrediente.Stock -= stockNecesario;

                            extraPorUnidad += ingrediente.PrecioExtra * cantIngrediente;
                            modificaciones.Add((ir.IngredienteId, cantIngrediente, ir.Accion, ingrediente.PrecioExtra));
                        }
                    }
                }

                // ── Desayuno gratuito: solo la primera unidad es gratis ───────
                bool primeraUnidadGratis = consumoDesayuno is not null &&
                    DesayunoService.AplicarDescuentoPrimeraUnidad(producto.ComponenteDesayuno, consumoDesayuno);

                if (primeraUnidadGratis)
                {
                    // Unidad gratuita: precio 0, extras también gratuitos (decisión de negocio)
                    var lineaGratis = new LineaPedido
                        { ProductoId = l.ProductoId, Cantidad = 1, PrecioUnitario = 0, Notas = l.Notas?.Trim() };
                    foreach (var (ingId, cant, accion, _) in modificaciones)
                        lineaGratis.Ingredientes.Add(new LineaPedidoIngrediente
                            { IngredienteId = ingId, Accion = accion, PrecioAplicado = 0, Cantidad = cant });
                    lineas.Add(lineaGratis);

                    if (l.Cantidad > 1)
                    {
                        var lineaPagada = new LineaPedido
                        {
                            ProductoId = l.ProductoId, Cantidad = l.Cantidad - 1,
                            PrecioUnitario = producto.Precio + extraPorUnidad,
                            Notas = l.Notas?.Trim()
                        };
                        foreach (var (ingId, cant, accion, precio) in modificaciones)
                            lineaPagada.Ingredientes.Add(new LineaPedidoIngrediente
                                { IngredienteId = ingId, Accion = accion, PrecioAplicado = precio, Cantidad = cant });
                        lineas.Add(lineaPagada);
                        total += (producto.Precio + extraPorUnidad) * (l.Cantidad - 1);
                    }
                }
                else
                {
                    var linea = new LineaPedido
                    {
                        ProductoId = l.ProductoId, Cantidad = l.Cantidad,
                        PrecioUnitario = producto.Precio + extraPorUnidad,
                        Notas = l.Notas?.Trim()
                    };
                    foreach (var (ingId, cant, accion, precio) in modificaciones)
                        linea.Ingredientes.Add(new LineaPedidoIngrediente
                            { IngredienteId = ingId, Accion = accion, PrecioAplicado = precio, Cantidad = cant });
                    lineas.Add(linea);
                    total += (producto.Precio + extraPorUnidad) * l.Cantidad;
                }
            }

            // 3. Detección de double-submit: rechazar si el mismo usuario tiene un pedido
            //    idéntico (mismas líneas y cantidades) creado en los últimos 30 segundos.
            //    La comparación de líneas se hace en memoria para evitar predicados no traducibles a SQL.
            var ventana = DateTime.UtcNow.AddSeconds(-30);
            var candidatos = await _db.Pedidos
                .Where(p => p.UsuarioId == userId.Value && p.FechaCreacion >= ventana && p.Total == total)
                .Include(p => p.Lineas)
                .ToListAsync();
            var pedidoReciente = candidatos.FirstOrDefault(p =>
                p.Lineas.Count == lineas.Count &&
                p.Lineas.All(l => lineas.Any(nl => nl.ProductoId == l.ProductoId && nl.Cantidad == l.Cantidad)));
            if (pedidoReciente is not null)
            {
                _logger.LogWarning("Double-submit detectado para usuario {UserId}: devolviendo pedido existente #{Num}.", userId, pedidoReciente.NumeroPedido);
                await transaction.RollbackAsync();
                var dtoExistente = await GetPedidoDtoAsync(pedidoReciente.Id);
                return CreatedAtAction(nameof(GetById), new { id = pedidoReciente.Id }, dtoExistente);
            }

            // 4. Verificar pago (Stripe o Gratuito)
            string? referenciaPago = null;
            if (req.MetodoPago == MetodoPago.Gratuito)
            {
                // Pedido de desayuno gratuito: total debe ser 0
                if (total != 0)
                    return BadRequest(new { mensaje = "El pedido no es completamente gratuito. Revisa el carrito." });
            }
            else if (!string.IsNullOrEmpty(req.StripePaymentIntentId))
            {
                var (pagado, status, amount, metaUserId) = await _stripe.VerificarPagoAsync(req.StripePaymentIntentId);
                if (!pagado)
                    return BadRequest(new { mensaje = $"El pago no se ha completado (estado: {status}). Inténtalo de nuevo." });

                // FIX-01: Verificar que el PaymentIntent pertenezca al usuario autenticado
                if (metaUserId != userId.Value.ToString())
                    return StatusCode(403, new { mensaje = "Este pago no pertenece a tu cuenta." });

                // FIX-02: Verificar que el importe cobrado coincida con el total calculado (con descuentos aplicados)
                var totalEsperadoCentimos = (long)Math.Round(total * 100, MidpointRounding.AwayFromZero);
                if (totalEsperadoCentimos != amount)
                    return BadRequest(new { mensaje = $"El importe cobrado ({amount / 100m:F2}€) no coincide con el total del pedido ({total:F2}€)." });

                referenciaPago = req.StripePaymentIntentId;

                // Si el webhook ya creó el pedido antes de que llegara esta llamada, devolverlo directamente
                var pedidoExistente = await _db.Pedidos
                    .Include(p => p.Lineas).ThenInclude(l => l.Producto)
                    .Include(p => p.Usuario).ThenInclude(u => u!.Instituto)
                    .FirstOrDefaultAsync(p => p.ReferenciasPago == referenciaPago);
                if (pedidoExistente is not null)
                {
                    _logger.LogInformation("PaymentIntent {PI} ya tiene pedido #{Num} — devolviendo existente.",
                        referenciaPago, pedidoExistente.NumeroPedido);
                    await transaction.RollbackAsync();
                    return CreatedAtAction(nameof(GetById), new { id = pedidoExistente.Id }, pedidoExistente.ToDto());
                }
            }

            // 4. Número de pedido secuencial del día (FIX-04: SARGable query)
            // Usar zona horaria España para que el contador se reinicie a medianoche local,
            // no a las 23:00 UTC (01:00 CET) del invierno o 22:00 UTC (00:00 CEST) del verano.
            var spainTz  = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Madrid");
            var ahoraEsp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spainTz);
            var hoyEspUtcInicio = TimeZoneInfo.ConvertTimeToUtc(ahoraEsp.Date, spainTz);
            var hoyEspUtcFin    = TimeZoneInfo.ConvertTimeToUtc(ahoraEsp.Date.AddDays(1), spainTz);
            var ultimoNumero = await _db.Pedidos
                .Where(p => p.FechaCreacion >= hoyEspUtcInicio && p.FechaCreacion < hoyEspUtcFin)
                .MaxAsync(p => (int?)p.NumeroPedido) ?? 0;

            var pedido = new Pedido
            {
                UsuarioId      = userId.Value,
                NumeroPedido   = ultimoNumero + 1,
                MetodoPago     = req.MetodoPago,
                Total          = total,
                Notas          = req.Notas?.Trim().Replace("<", "&lt;").Replace(">", "&gt;"),
                Lineas         = lineas,
                ReferenciasPago = referenciaPago
            };

            _db.Pedidos.Add(pedido);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            // 5. Cargar DTO y notificar a la cafetería vía SignalR.
            //    SignalR se lanza fire-and-forget para no bloquear la respuesta al cliente.
            var dto = await GetPedidoDtoAsync(pedido.Id);
            var institutoIdStr = User.FindFirst("institutoId")?.Value;
            var grupoInstituto = int.TryParse(institutoIdStr, out var instId) && instId > 0
                ? $"cafeteria-{instId}"
                : "cafeteria-global";
            _ = _hub.Clients.Groups(grupoInstituto, "cafeteria-global")
                .SendAsync("NuevoPedido", dto);

            return CreatedAtAction(nameof(GetById), new { id = pedido.Id }, dto);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otro pedido simultáneo agotó el stock entre nuestra lectura y escritura
            await transaction.RollbackAsync();
            return Conflict(new { mensaje = "El stock de uno o más productos cambió mientras procesabas el pedido. Comprueba la disponibilidad y vuelve a intentarlo." });
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            // Deadlock SQL Server — la transacción Serializable puede verse afectada bajo carga alta.
            // Devolver 503 para que el cliente pueda reintentar el pedido.
            await transaction.RollbackAsync();
            _logger.LogWarning("Deadlock al crear pedido para usuario {UserId}. El cliente debe reintentar.", userId);
            return StatusCode(503, new { mensaje = "El sistema está ocupado en este momento. Inténtalo de nuevo en unos segundos." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al procesar el pedido para el usuario {UserId}.", userId);
            await transaction.RollbackAsync();
            return StatusCode(500, new { mensaje = "Error al procesar el pedido. Inténtalo de nuevo." });
        }
    }

    // ── GET /api/pedidos/mis-pedidos ─────────────────────────────────────────
    [HttpGet("mis-pedidos")]
    public async Task<ActionResult<List<PedidoDto>>> MisPedidos([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 50);
        page     = Math.Max(page, 1);

        var pedidos = await _db.Pedidos
            .Where(p => p.UsuarioId == userId)
            .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto).ThenInclude(p => p.Alergenos)
            .Include(p => p.Lineas).ThenInclude(l => l.Ingredientes).ThenInclude(li => li.Ingrediente)
            .Include(p => p.Usuario) // Instituto omitido: no se muestra en historial propio
            .ToListAsync();

        return Ok(pedidos.Select(p => p.ToDto()).ToList());
    }

    // ── GET /api/pedidos/mis-stats ───────────────────────────────────────────
    [HttpGet("mis-stats")]
    public async Task<ActionResult<UsuarioStatsDto>> MisEstadisticas()
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        // Fusionar Count + Sum en una sola query GroupBy (evita 2 round-trips al servidor)
        var stats = await _db.Pedidos
            .Where(p => p.UsuarioId == userId && p.Estado != EstadoPedido.Cancelado)
            .GroupBy(_ => 0)
            .Select(g => new { Count = g.Count(), Sum = (decimal?)g.Sum(p => p.Total) })
            .FirstOrDefaultAsync();
        return Ok(new UsuarioStatsDto(stats?.Count ?? 0, stats?.Sum ?? 0m));
    }

    // ── GET /api/pedidos/{id} ────────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<ActionResult<PedidoDto>> GetById(int id)
    {
        // Verificar identidad antes de hacer la consulta
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        // BUG-008: una sola consulta con todos los includes necesarios para DTO y autorización
        var pedido = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto).ThenInclude(p => p.Alergenos)
            .Include(p => p.Lineas).ThenInclude(l => l.Ingredientes).ThenInclude(li => li.Ingrediente)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();

        var esAdmin = User.IsInRole("Admin");
        var esStaff = esAdmin || User.IsInRole("Personal") || User.IsInRole("Empleado");

        if (pedido.UsuarioId != userId && !esStaff)
            return Forbid();

        // Empleado/Personal solo pueden ver pedidos de su propio instituto.
        // SEC-013: si no tienen instituto asignado en el JWT se deniega por defecto.
        if (esStaff && !esAdmin)
        {
            var miInstitutoId = int.TryParse(User.FindFirst("institutoId")?.Value, out var iid) && iid > 0 ? iid : (int?)null;
            if (!miInstitutoId.HasValue || pedido.Usuario?.InstitutoId != miInstitutoId)
                return Forbid();
        }

        return Ok(pedido.ToDto());
    }

    // ── Transiciones de estado válidas ────────────────────────────────────────
    private static readonly Dictionary<EstadoPedido, EstadoPedido[]> _transicionesValidas = new()
    {
        [EstadoPedido.Pendiente]     = [EstadoPedido.EnPreparacion, EstadoPedido.Cancelado],
        [EstadoPedido.EnPreparacion] = [EstadoPedido.Listo, EstadoPedido.Cancelado],
        [EstadoPedido.Listo]         = [EstadoPedido.Entregado],
        [EstadoPedido.Entregado]     = [],
        [EstadoPedido.Cancelado]     = []
    };

    // ── PATCH /api/pedidos/{id}/estado  (Admin / Cafetería / Empleado) ────────
    [HttpPatch("{id}/estado")]
    [Authorize(Roles = "Admin,Personal,Empleado")]
    public async Task<ActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoRequest req)
    {
        var pedido = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto).ThenInclude(p => p.Alergenos)
            .Include(p => p.Lineas).ThenInclude(l => l.Ingredientes).ThenInclude(li => li.Ingrediente)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();

        // Empleado/Personal solo pueden gestionar pedidos de su propio instituto.
        // SEC-013: si no tienen instituto asignado en el JWT se deniega por defecto.
        if (!User.IsInRole("Admin"))
        {
            var miInstitutoId = int.TryParse(User.FindFirst("institutoId")?.Value, out var iid) && iid > 0 ? iid : (int?)null;
            if (!miInstitutoId.HasValue || pedido.Usuario?.InstitutoId != miInstitutoId)
                return Forbid();
        }

        // Validar transición de estado
        if (!_transicionesValidas.TryGetValue(pedido.Estado, out var permitidos) ||
            !permitidos.Contains(req.NuevoEstado))
        {
            return BadRequest(new { mensaje = $"No se puede cambiar de '{pedido.Estado}' a '{req.NuevoEstado}'." });
        }

        // Restaurar stock si se cancela un pedido
        if (req.NuevoEstado == EstadoPedido.Cancelado)
        {
            foreach (var linea in pedido.Lineas)
            {
                if (linea.Producto is not null && linea.Producto.Stock != -1)
                    linea.Producto.Stock += linea.Cantidad;

                // Restaurar stock de ingredientes añadidos
                foreach (var modif in linea.Ingredientes)
                {
                    if (modif.Accion == AccionIngrediente.Añadir &&
                        modif.Ingrediente is not null && modif.Ingrediente.Stock != -1)
                        modif.Ingrediente.Stock += linea.Cantidad * modif.Cantidad;
                }
            }
        }

        var estadoAnterior = pedido.Estado;
        pedido.Estado = req.NuevoEstado;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] Usuario {ActorId} cambió estado del pedido #{NumeroPedido} (ID:{PedidoId}): {EstadoAnterior} → {EstadoNuevo}.",
            User.GetUserId(), pedido.NumeroPedido, pedido.Id, estadoAnterior, req.NuevoEstado);

        // Notificaciones: envueltas en try-catch para no revertir el cambio de estado si fallan
        try
        {
            // Notificar al usuario propietario del pedido vía SignalR (tiempo real en app abierta)
            await _hub.Clients.Group($"user-{pedido.UsuarioId}")
                .SendAsync("EstadoPedidoActualizado", new { pedido.Id, Estado = req.NuevoEstado.ToString() });

            // Notificar al panel de la cafetería para que actualice la lista en tiempo real
            var institutoId = pedido.Usuario?.InstitutoId;
            var grupoCafeteria = institutoId.HasValue ? $"cafeteria-{institutoId}" : "cafeteria-global";
            _ = _hub.Clients.Groups(grupoCafeteria, "cafeteria-global")
                .SendAsync("EstadoPedidoActualizado", new { pedido.Id, Estado = req.NuevoEstado.ToString() });

            // Notificación push cuando el pedido está listo para recoger
            if (req.NuevoEstado == EstadoPedido.Listo)
            {
                var tokens = await _db.DispositivoTokens
                    .Where(t => t.UsuarioId == pedido.UsuarioId)
                    .Select(t => t.Token)
                    .ToListAsync();

                if (tokens.Count > 0)
                {
                    var invalidos = await _fcm.EnviarAsync(
                        tokens,
                        "¡Tu pedido está listo! ☕",
                        $"Pedido #{pedido.NumeroPedido} — ya puedes pasar a recogerlo.",
                        new Dictionary<string, string> { ["pedidoId"] = pedido.Id.ToString() });

                    if (invalidos.Count > 0)
                    {
                        _db.DispositivoTokens.RemoveRange(
                            _db.DispositivoTokens.Where(t => invalidos.Contains(t.Token)));
                        await _db.SaveChangesAsync();
                        _logger.LogInformation("Eliminados {N} tokens FCM expirados del usuario {UserId}.",
                            invalidos.Count, pedido.UsuarioId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // El estado ya fue guardado correctamente — las notificaciones son best-effort
            _logger.LogWarning(ex, "Error en notificaciones tras cambiar estado del pedido {PedidoId}. Estado guardado correctamente.", id);
        }

        return NoContent();
    }

    // ── GET /api/pedidos/historial  (Admin / Personal / Empleado) ────────────
    /// <summary>
    /// Devuelve los pedidos de hoy en todos los estados (hasta 200), para la
    /// vista de historial del empleado. Los empleados/personal solo ven su instituto.
    /// </summary>
    [HttpGet("historial")]
    [Authorize(Roles = "Admin,Personal,Empleado")]
    public async Task<ActionResult<List<PedidoDto>>> Historial()
    {
        var esAdmin    = User.IsInRole("Admin");
        var esPersonal = User.IsInRole("Personal");
        var esEmpleado = User.IsInRole("Empleado");
        var institutoId = int.TryParse(User.FindFirst("institutoId")?.Value, out var iid) && iid > 0 ? iid : (int?)null;

        if ((esEmpleado || esPersonal) && !institutoId.HasValue)
            return Ok(new List<PedidoDto>());

        var spainTz  = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Madrid");
        var ahoraEsp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spainTz);
        // Si hoy es lunes, incluir también los pre-pedidos del domingo anterior
        var desdeEsp = ahoraEsp.DayOfWeek == DayOfWeek.Monday
            ? ahoraEsp.Date.AddDays(-1)
            : ahoraEsp.Date;
        var desde    = TimeZoneInfo.ConvertTimeToUtc(desdeEsp, spainTz);

        IQueryable<Pedido> query = _db.Pedidos.Where(p => p.FechaCreacion >= desde);

        if (esEmpleado || esPersonal)
            query = query.Where(p => p.Usuario!.InstitutoId == institutoId);

        var pedidos = await query
            .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.Id)
            .Take(200)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto).ThenInclude(p => p.Alergenos)
            .Include(p => p.Lineas).ThenInclude(l => l.Ingredientes).ThenInclude(li => li.Ingrediente)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .ToListAsync();

        return Ok(pedidos.Select(p => p.ToDto()).ToList());
    }

    // ── GET /api/pedidos/en-curso  (Admin / Cafetería / Empleado) ────────────
    [HttpGet("en-curso")]
    [Authorize(Roles = "Admin,Personal,Empleado")]
    public async Task<ActionResult<List<PedidoDto>>> EnCurso()
    {
        var esEmpleado = User.IsInRole("Empleado");
        var institutoId = int.TryParse(User.FindFirst("institutoId")?.Value, out var iid) && iid > 0 ? iid : (int?)null;

        // SEC-013/BUG-010: Empleado/Personal sin instituto asignado en JWT → lista vacía por seguridad
        var esPersonal = User.IsInRole("Personal");
        if ((esEmpleado || esPersonal) && !institutoId.HasValue)
            return Ok(new List<PedidoDto>());

        var query = _db.Pedidos
            .Where(p => p.Estado == EstadoPedido.Pendiente || p.Estado == EstadoPedido.EnPreparacion)
            .OrderBy(p => p.FechaCreacion).ThenBy(p => p.Id)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Lineas).ThenInclude(l => l.Ingredientes).ThenInclude(li => li.Ingrediente)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .AsQueryable();

        // Empleado y Personal solo ven pedidos de su propio instituto
        if (esEmpleado || esPersonal)
            query = query.Where(p => p.Usuario.InstitutoId == institutoId);

        var pedidos = await query.ToListAsync();
        return Ok(pedidos.Select(p => p.ToDto()).ToList());
    }

    // ── GET /api/pedidos/by-intent/{paymentIntentId} ─────────────────────────
    /// <summary>
    /// Recupera el pedido asociado a un PaymentIntent de Stripe.
    /// Usado por el cliente MAUI para recuperarse de un timeout en POST /api/pedidos.
    /// </summary>
    [HttpGet("by-intent/{paymentIntentId}")]
    public async Task<ActionResult<PedidoDto>> GetByIntent(string paymentIntentId)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var pedido = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Lineas).ThenInclude(l => l.Ingredientes).ThenInclude(li => li.Ingrediente)
            .Include(p => p.Usuario).ThenInclude(u => u!.Instituto)
            .FirstOrDefaultAsync(p => p.ReferenciasPago == paymentIntentId && p.UsuarioId == userId.Value);

        return pedido is null ? NotFound() : Ok(pedido.ToDto());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private async Task<PedidoDto?> GetPedidoDtoAsync(int id)
    {
        var p = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Lineas).ThenInclude(l => l.Ingredientes).ThenInclude(li => li.Ingrediente)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .FirstOrDefaultAsync(p => p.Id == id);

        return p?.ToDto();
    }
}
