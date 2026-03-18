using System.Security.Claims;
using System.Data;
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

    public PedidosController(AppDbContext db, HorarioService horario, StripeService stripe,
        IHubContext<CafeteriaHub> hub, FcmService fcm, ILogger<PedidosController> logger)
    {
        _db      = db;
        _horario = horario;
        _stripe  = stripe;
        _hub     = hub;
        _fcm     = fcm;
        _logger  = logger;
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

        // 1. Comprobar horario
        var horario = await _horario.PuedePedirAhoraAsync(userId.Value);
        if (!horario.Puede)
            return BadRequest(new { mensaje = horario.Mensaje });

        // Validar método de pago
        if (!Enum.IsDefined(typeof(MetodoPago), req.MetodoPago))
            return BadRequest(new { mensaje = "Método de pago inválido." });

        // Usar transacción ReadCommitted; el control de stock se hace via EF ConcurrencyCheck en Producto
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            // 2. Calcular total y validar stock
            var lineas = new List<LineaPedido>();
            decimal total = 0;

            foreach (var l in req.Lineas)
            {
                var producto = await _db.Productos.FindAsync(l.ProductoId);
                if (producto is null || !producto.Activo)
                    return BadRequest(new { mensaje = $"Producto #{l.ProductoId} no disponible." });

                if (producto.Stock != -1 && producto.Stock < l.Cantidad)
                    return BadRequest(new { mensaje = $"Stock insuficiente para '{producto.Nombre}'. Disponibles: {producto.Stock}." });

                // Decrementar stock inmediatamente para evitar doble lectura
                if (producto.Stock != -1) producto.Stock -= l.Cantidad;

                lineas.Add(new LineaPedido
                {
                    ProductoId     = l.ProductoId,
                    Cantidad       = l.Cantidad,
                    PrecioUnitario = producto.Precio
                });
                total += producto.Precio * l.Cantidad;
            }

            // 3. Verificar pago con Stripe (si se proporcionó)
            string? referenciaPago = null;
            if (!string.IsNullOrEmpty(req.StripePaymentIntentId))
            {
                var (pagado, status) = await _stripe.VerificarPagoAsync(req.StripePaymentIntentId);
                if (!pagado)
                    return BadRequest(new { mensaje = $"El pago no se ha completado (estado: {status}). Inténtalo de nuevo." });
                referenciaPago = req.StripePaymentIntentId;
            }

            // 4. Número de pedido secuencial del día
            var hoy = DateTime.UtcNow.Date;
            var ultimoNumero = await _db.Pedidos
                .Where(p => p.FechaCreacion.Date == hoy)
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

            // 5. Notificar a la cafetería en tiempo real vía SignalR
            var dto = await GetPedidoDtoAsync(pedido.Id);
            await _hub.Clients.Group("cafeteria").SendAsync("NuevoPedido", dto);

            return CreatedAtAction(nameof(GetById), new { id = pedido.Id }, dto);
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
    public async Task<ActionResult<List<PedidoDto>>> MisPedidos()
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var pedidos = await _db.Pedidos
            .Where(p => p.UsuarioId == userId)
            .OrderByDescending(p => p.FechaCreacion)
            .Take(20)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .ToListAsync();

        return Ok(pedidos.Select(p => p.ToDto()).ToList());
    }

    // ── GET /api/pedidos/mis-stats ───────────────────────────────────────────
    [HttpGet("mis-stats")]
    public async Task<ActionResult<UsuarioStatsDto>> MisEstadisticas()
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var query = _db.Pedidos.Where(p => p.UsuarioId == userId && p.Estado != EstadoPedido.Cancelado);
        var totalPedidos = await query.CountAsync();
        var totalGastado = await query.SumAsync(p => (decimal?)p.Total) ?? 0;
        return Ok(new UsuarioStatsDto(totalPedidos, totalGastado));
    }

    // ── GET /api/pedidos/{id} ────────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<ActionResult<PedidoDto>> GetById(int id)
    {
        // Verificar identidad antes de hacer la consulta
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();

        var esStaff = User.IsInRole("Admin") || User.IsInRole("Personal");
        if (pedido.UsuarioId != userId && !esStaff)
            return Forbid();

        var dto = await GetPedidoDtoAsync(id);
        return dto is null ? NotFound() : Ok(dto);
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
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();

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
                if (linea.Producto.Stock != -1)
                    linea.Producto.Stock += linea.Cantidad;
            }
        }

        pedido.Estado = req.NuevoEstado;
        await _db.SaveChangesAsync();

        // Notificar al usuario propietario del pedido vía SignalR (tiempo real en app abierta)
        await _hub.Clients.Group($"user-{pedido.UsuarioId}")
            .SendAsync("EstadoPedidoActualizado", new { pedido.Id, Estado = req.NuevoEstado.ToString() });

        // Notificación push cuando el pedido está listo para recoger
        if (req.NuevoEstado == EstadoPedido.Listo)
        {
            var tokens = await _db.DispositivoTokens
                .Where(t => t.UsuarioId == pedido.UsuarioId)
                .Select(t => t.Token)
                .ToListAsync();

            if (tokens.Count > 0)
                await _fcm.EnviarAsync(
                    tokens,
                    "¡Tu pedido está listo! ☕",
                    $"Pedido #{pedido.NumeroPedido} — ya puedes pasar a recogerlo.",
                    new Dictionary<string, string> { ["pedidoId"] = pedido.Id.ToString() });
        }

        return NoContent();
    }

    // ── GET /api/pedidos/en-curso  (Admin / Cafetería / Empleado) ────────────
    [HttpGet("en-curso")]
    [Authorize(Roles = "Admin,Personal,Empleado")]
    public async Task<ActionResult<List<PedidoDto>>> EnCurso()
    {
        var esEmpleado = User.IsInRole("Empleado");
        var institutoId = int.TryParse(User.FindFirst("institutoId")?.Value, out var iid) && iid > 0 ? iid : (int?)null;

        var query = _db.Pedidos
            .Where(p => p.Estado == EstadoPedido.Pendiente || p.Estado == EstadoPedido.EnPreparacion)
            .OrderBy(p => p.FechaCreacion)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .AsQueryable();

        if (esEmpleado && institutoId.HasValue)
            query = query.Where(p => p.Usuario.InstitutoId == institutoId);

        var pedidos = await query.ToListAsync();
        return Ok(pedidos.Select(p => p.ToDto()).ToList());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private async Task<PedidoDto?> GetPedidoDtoAsync(int id)
    {
        var p = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .FirstOrDefaultAsync(p => p.Id == id);

        return p?.ToDto();
    }
}
