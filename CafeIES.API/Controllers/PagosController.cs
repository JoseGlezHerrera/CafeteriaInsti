using System.Data;
using System.Security.Claims;
using System.Text.Json;
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
using Stripe;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/pagos")]
[EnableRateLimiting("general")]
public class PagosController : ControllerBase
{
    private readonly AppDbContext              _db;
    private readonly StripeService             _stripe;
    private readonly HorarioService            _horario;
    private readonly IConfiguration            _config;
    private readonly IHubContext<CafeteriaHub> _hub;
    private readonly ILogger<PagosController>  _logger;
    private readonly DesayunoService           _desayuno;

    public PagosController(AppDbContext db, StripeService stripe, HorarioService horario,
        IConfiguration config, IHubContext<CafeteriaHub> hub, ILogger<PagosController> logger,
        DesayunoService desayuno)
    {
        _db       = db;
        _stripe   = stripe;
        _horario  = horario;
        _config   = config;
        _hub      = hub;
        _logger   = logger;
        _desayuno = desayuno;
    }

    /// <summary>
    /// Devuelve la clave pública de Stripe para el cliente.
    /// </summary>
    [HttpGet("config")]
    public ActionResult<StripeConfigDto> GetConfig()
    {
        var pk = _config["Stripe:PublishableKey"] ?? "";
        return Ok(new StripeConfigDto(pk));
    }

    /// <summary>
    /// Crea un PaymentIntent de Stripe para los productos del carrito.
    /// Valida stock y calcula total en servidor (nunca confiar en el cliente).
    /// </summary>
    [HttpPost("crear-intent")]
    [Authorize]
    [EnableRateLimiting("pagos")]
    public async Task<ActionResult<PagoIntentResponse>> CrearIntent([FromBody] CrearPagoRequest req)
    {
        // 0. Validar que el usuario está activo y puede pedir ahora
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var usuario = await _db.Usuarios.FindAsync(userId.Value);
        if (usuario is null) return Unauthorized();
        if (usuario.Estado != EstadoCuenta.Activa)
            return StatusCode(403, new { mensaje = "Tu cuenta no está activa." });

        var horario = await _horario.PuedePedirAhoraAsync(userId.Value);
        if (!horario.Puede)
            return BadRequest(new { mensaje = horario.Mensaje });

        // 1. Validar productos y calcular total en servidor (aplicando descuento de desayuno)
        decimal total = 0;
        var descripcionItems = new List<string>();

        // Comprobar estado de desayuno gratuito del usuario
        var hoyPago = DesayunoService.HoyEspaña();
        ConsumoDesayuno? consumoPago = null;
        bool zumoAplicadoPago    = false;
        bool bocataAplicadoPago  = false;
        if (usuario.DesayunoGratuito)
        {
            consumoPago = await _db.ConsumoDesayunos
                .FirstOrDefaultAsync(c => c.UsuarioId == userId.Value && c.Fecha == hoyPago);
            zumoAplicadoPago   = consumoPago?.ZumoConsumido   ?? false;
            bocataAplicadoPago = consumoPago?.BocataConsumido ?? false;
        }

        var lineasConPrecio = new List<(int ProductoId, int Cantidad, decimal Precio)>();
        foreach (var l in req.Lineas)
        {
            var producto = await _db.Productos.FindAsync(l.ProductoId);
            if (producto is null || !producto.Activo)
                return BadRequest(new { mensaje = $"Producto #{l.ProductoId} no disponible." });

            if (producto.Stock != -1 && producto.Stock < l.Cantidad)
                return BadRequest(new { mensaje = $"Stock insuficiente para '{producto.Nombre}'." });

            decimal precio = producto.Precio;
            bool primeraGratisPago = false;
            if (usuario.DesayunoGratuito)
            {
                if (producto.ComponenteDesayuno == ComponenteDesayuno.Zumo && !zumoAplicadoPago)
                {
                    primeraGratisPago = true; zumoAplicadoPago = true;
                }
                else if (producto.ComponenteDesayuno == ComponenteDesayuno.Bocata && !bocataAplicadoPago)
                {
                    primeraGratisPago = true; bocataAplicadoPago = true;
                }
            }

            // Solo 1 unidad gratuita; el resto al precio normal.
            // Almacenamos líneas separadas en la metadata para que el webhook
            // pueda reconstruir el pedido con los precios correctos.
            if (primeraGratisPago)
            {
                total += producto.Precio * (l.Cantidad - 1);
                lineasConPrecio.Add((l.ProductoId, 1, 0m));                              // unidad gratuita
                if (l.Cantidad > 1)
                    lineasConPrecio.Add((l.ProductoId, l.Cantidad - 1, producto.Precio)); // resto al precio normal
            }
            else
            {
                total += precio * l.Cantidad;
                lineasConPrecio.Add((l.ProductoId, l.Cantidad, precio));
            }
            descripcionItems.Add($"{producto.Nombre} ×{l.Cantidad}");
        }

        if (total < 0.50m)
            return BadRequest(new { mensaje = "El importe mínimo para pago con tarjeta es 0.50€. Si tu pedido es completamente gratuito, usa el flujo de desayuno gratuito." });

        // 2. Crear PaymentIntent en Stripe
        // La metadata incluye PrecioUnitario para que el webhook pueda reconstruir el pedido
        // con los mismos precios (ya descontados) sin necesidad de re-aplicar la lógica.
        var userIdStr = userId.Value.ToString();
        var lineasJson = JsonSerializer.Serialize(
            lineasConPrecio.Select(l => new { l.ProductoId, l.Cantidad, l.Precio }));
        var metadata = new Dictionary<string, string>
        {
            ["userId"]      = userIdStr,
            ["notas"]       = req.Notas ?? "",
            ["metodo_pago"] = ((int)MetodoPago.Tarjeta).ToString(),
            ["lineas"]      = lineasJson
        };

        var (clientSecret, paymentIntentId) = await _stripe.CrearPaymentIntentAsync(
            total,
            $"CaféIES: {string.Join(", ", descripcionItems)}",
            metadata);

        return Ok(new PagoIntentResponse(clientSecret, paymentIntentId, total));
    }

    /// <summary>
    /// Página HTML con Stripe.js para recoger datos de tarjeta sin manipular
    /// números de tarjeta en bruto. El JS lee pk y cs de los query params.
    /// </summary>
    [HttpGet("stripe-form")]
    [AllowAnonymous]
    public ContentResult StripeForm()
        => Content(StripeFormHtml, "text/html");

    private const string StripeFormHtml = """
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1, user-scalable=no">
          <title>Pago seguro</title>
          <style>
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body { background: #1a1916; color: #f0ede6; font-family: -apple-system, BlinkMacSystemFont, sans-serif; }
            .wrap { padding: 28px 20px; max-width: 480px; margin: 0 auto; }
            h2 { color: #f5a623; font-size: 18px; font-weight: 700; margin-bottom: 22px; }
            .card-box { background: #252320; border: 1.5px solid #3a3835; border-radius: 14px; padding: 16px 14px; margin-bottom: 20px; }
            #error { color: #e05252; font-size: 14px; margin-bottom: 14px; line-height: 1.4; min-height: 18px; }
            #status { color: #f5a623; font-size: 14px; text-align: center; margin-bottom: 14px; min-height: 18px; }
            #pay-btn { background: #f5a623; color: #1a1916; border: none; border-radius: 14px; padding: 16px 24px; width: 100%; font-size: 16px; font-weight: 700; cursor: pointer; }
            #pay-btn:disabled { opacity: .55; cursor: default; }
            .secure { color: #7a7468; font-size: 11px; text-align: center; margin-top: 16px; }
          </style>
        </head>
        <body>
          <div class="wrap">
            <h2>💳 Datos de tarjeta</h2>
            <div class="card-box"><div id="card-element"></div></div>
            <div id="error"></div>
            <div id="status"></div>
            <button id="pay-btn">Confirmar pago</button>
            <p class="secure">🔒 Encriptado y procesado por Stripe.<br>CaféIES nunca accede a los datos de tu tarjeta.</p>
          </div>
          <script src="https://js.stripe.com/v3/"></script>
          <script>
            (function () {
              var p = new URLSearchParams(location.search);
              var pk = p.get('pk'), cs = p.get('cs');
              if (!pk || !cs) { document.getElementById('error').textContent = 'Error de configuración.'; return; }

              var stripe = Stripe(pk);
              var card = stripe.elements().create('card', {
                style: {
                  base: { color: '#f0ede6', fontFamily: '-apple-system, sans-serif', fontSize: '16px', '::placeholder': { color: '#7a7468' } },
                  invalid: { color: '#e05252' }
                }
              });
              card.mount('#card-element');

              var btn = document.getElementById('pay-btn');
              var errDiv = document.getElementById('error');
              var statusDiv = document.getElementById('status');

              btn.addEventListener('click', async function () {
                btn.disabled = true;
                errDiv.textContent = '';
                statusDiv.textContent = 'Procesando…';

                var result = await stripe.confirmCardPayment(cs, { payment_method: { card: card } });

                if (result.error) {
                  errDiv.textContent = result.error.message;
                  statusDiv.textContent = '';
                  btn.disabled = false;
                } else if (result.paymentIntent && result.paymentIntent.status === 'succeeded') {
                  statusDiv.textContent = '✓ Pago confirmado';
                  btn.disabled = true;
                  setTimeout(function () {
                    window.location.href = 'cafeies://success/' + result.paymentIntent.id;
                  }, 700);
                }
              });
            })();
          </script>
        </body>
        </html>
        """;

    /// <summary>
    /// FIX-10: Cancela un PaymentIntent en Stripe al abandonar el pago.
    /// Verifica que el intent pertenezca al usuario autenticado via metadata.
    /// </summary>
    [HttpPost("cancelar-intent")]
    [Authorize]
    public async Task<IActionResult> CancelarIntent([FromBody] CancelarIntentRequest req)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();
        var userIdStr = userId.Value.ToString();

        try
        {
            var (_, _, _, metaUserId) = await _stripe.VerificarPagoAsync(req.PaymentIntentId);
            if (metaUserId != userIdStr)
                return StatusCode(403, new { mensaje = "Este pago no pertenece a tu cuenta." });

            await _stripe.CancelarIntentAsync(req.PaymentIntentId);
            return Ok(new { mensaje = "Pago cancelado." });
        }
        catch (Stripe.StripeException ex) when (ex.StripeError?.Code == "payment_intent_unexpected_state")
        {
            // El intent ya fue confirmado o cancelado — ignorar
            _logger.LogDebug("PaymentIntent {Id} ya estaba en estado final.", req.PaymentIntentId);
            return Ok(new { mensaje = "El pago ya estaba finalizado." });
        }
        catch (Stripe.StripeException ex) when (ex.StripeError?.Code == "resource_missing")
        {
            // El PaymentIntent no existe en Stripe (ID inválido o de otro entorno)
            _logger.LogWarning("PaymentIntent {Id} no encontrado en Stripe.", req.PaymentIntentId);
            return BadRequest(new { mensaje = "El identificador de pago no existe." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cancelar PaymentIntent {Id}.", req.PaymentIntentId);
            return StatusCode(500, new { mensaje = "Error al cancelar el pago." });
        }
    }

    /// <summary>
    /// Webhook de Stripe — recibe notificaciones de pago completado/fallido.
    /// Si el pago se completó pero no existe pedido (usuario cerró la app),
    /// reconstruye el pedido automáticamente desde los metadatos del PaymentIntent.
    /// Endpoint público (Stripe no envía JWT).
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();
        var webhookSecret = _config["Stripe:WebhookSecret"] ?? "";

        // BUG-002: Si el secret no está configurado, rechazar cualquier petición al webhook
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogError("Stripe:WebhookSecret no está configurado. Webhook rechazado para evitar fraude.");
            return StatusCode(503, "Webhook no configurado.");
        }

        if (string.IsNullOrEmpty(signature))
            return BadRequest();

        var stripeEvent = _stripe.ConstruirEvento(json, signature, webhookSecret);
        if (stripeEvent is null)
            return BadRequest("Firma inválida.");

        switch (stripeEvent.Type)
        {
            case EventTypes.PaymentIntentSucceeded:
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent is not null)
                {
                    _logger.LogInformation(
                        "✅ Webhook: PaymentIntent {Id} succeeded — {Amount} céntimos",
                        intent.Id, intent.Amount);

                    var existePedido = await _db.Pedidos
                        .AnyAsync(p => p.ReferenciasPago == intent.Id);

                    if (!existePedido)
                        await ReconstruirPedidoAsync(intent);
                }
                break;
            }

            case EventTypes.PaymentIntentPaymentFailed:
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent is not null)
                {
                    _logger.LogWarning(
                        "❌ Webhook: PaymentIntent {Id} failed — {Error}",
                        intent.Id,
                        intent.LastPaymentError?.Message ?? "sin detalle");
                }
                break;
            }

            default:
                _logger.LogDebug("Webhook Stripe: evento {Type} ignorado", stripeEvent.Type);
                break;
        }

        return Ok();
    }

    /// <summary>
    /// Reconstruye un pedido desde los metadatos de un PaymentIntent cuyo pago
    /// ya se cobró pero el cliente cerró la app antes de llamar a POST /api/pedidos.
    /// </summary>
    private async Task ReconstruirPedidoAsync(PaymentIntent intent)
    {
        _logger.LogWarning(
            "⚠️ Webhook: pago {Id} sin pedido asociado — intentando reconstruir desde metadatos.",
            intent.Id);

        // Leer metadatos guardados al crear el PaymentIntent
        if (!intent.Metadata.TryGetValue("userId",  out var userIdStr) ||
            !intent.Metadata.TryGetValue("lineas",   out var lineasJson) ||
            !int.TryParse(userIdStr, out var userId))
        {
            _logger.LogError(
                "❌ Webhook: no se puede reconstruir el pedido {Id} — faltan metadatos (userId/lineas).",
                intent.Id);
            return;
        }

        intent.Metadata.TryGetValue("notas", out var notas);
        intent.Metadata.TryGetValue("metodo_pago", out var metodoPagoStr);
        var metodo = int.TryParse(metodoPagoStr, out var mp) && Enum.IsDefined(typeof(MetodoPago), mp)
            ? (MetodoPago)mp
            : MetodoPago.Tarjeta;

        // Parsear las líneas del pedido (incluyen PrecioUnitario ya descontado si hay desayuno gratuito)
        List<(int ProductoId, int Cantidad, decimal? PrecioUnitario)> lineas;
        try
        {
            var parsed = JsonSerializer.Deserialize<List<JsonElement>>(lineasJson);
            if (parsed is null || parsed.Count == 0)
            {
                _logger.LogError("❌ Webhook: metadatos de líneas vacíos para {Id}.", intent.Id);
                return;
            }
            lineas = parsed.Select(e => (
                e.GetProperty("ProductoId").GetInt32(),
                e.GetProperty("Cantidad").GetInt32(),
                e.TryGetProperty("Precio", out var pElem) && pElem.TryGetDecimal(out var p) ? (decimal?)p : null
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Webhook: error al parsear líneas del pedido {Id}.", intent.Id);
            return;
        }

        // FIX-03: Serializable para evitar race condition en NumeroPedido
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var lineasPedido = new List<LineaPedido>();
            var notasAjuste  = new List<string>();
            decimal total = 0;

            // Cargar consumo de desayuno para marcarlo si alguna línea es gratuita
            var usuarioWh = await _db.Usuarios.FindAsync(userId);
            var consumoWh = await _desayuno.ObtenerOCrearConsumoHoyAsync(
                userId, usuarioWh?.DesayunoGratuito == true);

            foreach (var (productoId, cantidad, precioMetadata) in lineas)
            {
                var producto = await _db.Productos.FindAsync(productoId);
                if (producto is null || !producto.Activo)
                {
                    _logger.LogWarning(
                        "⚠️ Webhook: producto {PId} no disponible al reconstruir pedido {IntentId}.",
                        productoId, intent.Id);
                    continue;
                }

                // Usar el precio de la metadata: desde la versión actual ya incluye 0€ para la unidad
                // gratuita y precio normal para el resto (split de líneas). Fallback al precio actual
                // del producto si la metadata es de una versión anterior y no lo incluye.
                var precioUnitario = precioMetadata ?? producto.Precio;

                // Si esta línea es gratuita (precio 0), marcar ConsumoDesayuno para que el usuario
                // no pueda volver a usar el beneficio en el mismo día
                if (consumoWh is not null && precioUnitario == 0m)
                    DesayunoService.MarcarConsumoForzado(producto.ComponenteDesayuno, consumoWh);

                if (producto.Stock != -1)
                {
                    if (producto.Stock < cantidad)
                    {
                        _logger.LogWarning(
                            "⚠️ Webhook: stock insuficiente para producto {PId} — ajustando cantidad.",
                            productoId);
                        if (producto.Stock <= 0) continue;
                        var cantidadReal = Math.Min(cantidad, producto.Stock);
                        notasAjuste.Add($"{producto.Nombre} (pedido: {cantidad}, servido: {cantidadReal})");
                        producto.Stock -= cantidadReal;
                        lineasPedido.Add(new LineaPedido
                        {
                            ProductoId = productoId, Cantidad = cantidadReal,
                            PrecioUnitario = precioUnitario
                        });
                        total += precioUnitario * cantidadReal;
                        continue;
                    }
                    producto.Stock -= cantidad;
                }

                lineasPedido.Add(new LineaPedido
                {
                    ProductoId = productoId, Cantidad = cantidad,
                    PrecioUnitario = precioUnitario
                });
                total += precioUnitario * cantidad;
            }

            if (lineasPedido.Count == 0)
            {
                _logger.LogError(
                    "❌ Webhook: no se pudo reconstruir ninguna línea para el pedido {Id}.",
                    intent.Id);
                await transaction.RollbackAsync();
                return;
            }

            // Número de pedido: usar zona horaria España (igual que PedidosController)
            var spainTz  = TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");
            var ahoraEsp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spainTz);
            var hoyEspUtcInicio = TimeZoneInfo.ConvertTimeToUtc(ahoraEsp.Date, spainTz);
            var hoyEspUtcFin    = TimeZoneInfo.ConvertTimeToUtc(ahoraEsp.Date.AddDays(1), spainTz);
            var ultimoNumero = await _db.Pedidos
                .Where(p => p.FechaCreacion >= hoyEspUtcInicio && p.FechaCreacion < hoyEspUtcFin)
                .MaxAsync(p => (int?)p.NumeroPedido) ?? 0;

            var notaFinal = notas?.Trim().Replace("<", "&lt;").Replace(">", "&gt;");
            if (notasAjuste.Count > 0)
            {
                var avisoStock = "⚠️ Stock ajustado: " + string.Join(", ", notasAjuste);
                notaFinal = string.IsNullOrEmpty(notaFinal) ? avisoStock : $"{notaFinal} | {avisoStock}";
            }
            // Truncar a 300 chars para no exceder la columna Notas (MaxLength 300)
            if (notaFinal?.Length > 300) notaFinal = notaFinal[..300];

            var pedido = new Pedido
            {
                UsuarioId       = userId,
                NumeroPedido    = ultimoNumero + 1,
                MetodoPago      = metodo,
                Total           = total,
                Notas           = notaFinal,
                Lineas          = lineasPedido,
                ReferenciasPago = intent.Id
            };

            _db.Pedidos.Add(pedido);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                when (dbEx.InnerException?.Message.Contains("IX_Pedidos_ReferenciasPago") == true ||
                      dbEx.InnerException?.Message.Contains("UNIQUE") == true)
            {
                // El webhook llegó dos veces antes de que el primero hiciera commit.
                // El índice único en ReferenciasPago evitó el doble pedido — es seguro ignorar.
                _logger.LogWarning(
                    "⚠️ Webhook: pedido duplicado detectado para PaymentIntent {Id} — ignorando segunda inserción.",
                    intent.Id);
                await transaction.RollbackAsync();
                return;
            }
            await transaction.CommitAsync();

            _logger.LogInformation(
                "✅ Webhook: pedido #{Num} reconstruido para usuario {UserId} desde PaymentIntent {IntentId}.",
                pedido.NumeroPedido, userId, intent.Id);

            // Notificar a la cafetería en tiempo real
            var dto = await _db.Pedidos
                .Where(p => p.Id == pedido.Id)
                .Include(p => p.Lineas).ThenInclude(l => l.Producto)
                .Include(p => p.Usuario).ThenInclude(u => u!.Instituto)
                .Select(p => p.ToDto())
                .FirstOrDefaultAsync();

            if (dto is not null)
            {
                var usuarioInstitutoId = await _db.Usuarios
                    .Where(u => u.Id == userId)
                    .Select(u => u.InstitutoId)
                    .FirstOrDefaultAsync();
                var grupo = usuarioInstitutoId.HasValue
                    ? $"cafeteria-{usuarioInstitutoId}"
                    : "cafeteria-global";
                await _hub.Clients.Groups(grupo, "cafeteria-global")
                    .SendAsync("NuevoPedido", dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "❌ Webhook: error al reconstruir el pedido desde PaymentIntent {Id}.", intent.Id);
            await transaction.RollbackAsync();
        }
    }
}
