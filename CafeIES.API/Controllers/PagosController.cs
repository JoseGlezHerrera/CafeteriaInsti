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
    private readonly IConfiguration            _config;
    private readonly IHubContext<CafeteriaHub> _hub;
    private readonly ILogger<PagosController>  _logger;

    public PagosController(AppDbContext db, StripeService stripe, IConfiguration config,
        IHubContext<CafeteriaHub> hub, ILogger<PagosController> logger)
    {
        _db     = db;
        _stripe = stripe;
        _config = config;
        _hub    = hub;
        _logger = logger;
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
    public async Task<ActionResult<PagoIntentResponse>> CrearIntent([FromBody] CrearPagoRequest req)
    {
        // 1. Validar productos y calcular total en servidor
        decimal total = 0;
        var descripcionItems = new List<string>();

        foreach (var l in req.Lineas)
        {
            var producto = await _db.Productos.FindAsync(l.ProductoId);
            if (producto is null || !producto.Activo)
                return BadRequest(new { mensaje = $"Producto #{l.ProductoId} no disponible." });

            if (producto.Stock != -1 && producto.Stock < l.Cantidad)
                return BadRequest(new { mensaje = $"Stock insuficiente para '{producto.Nombre}'." });

            total += producto.Precio * l.Cantidad;
            descripcionItems.Add($"{producto.Nombre} ×{l.Cantidad}");
        }

        if (total < 0.50m)
            return BadRequest(new { mensaje = "El importe mínimo es 0.50€." });

        // 2. Crear PaymentIntent en Stripe
        // Guardamos en metadata todo lo necesario para reconstruir el pedido
        // desde el webhook si el usuario cierra la app antes de confirmarlo.
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anon";
        var lineasJson = JsonSerializer.Serialize(
            req.Lineas.Select(l => new { l.ProductoId, l.Cantidad }));
        var metadata = new Dictionary<string, string>
        {
            ["userId"]      = userId,
            ["notas"]       = req.Notas ?? "",
            ["metodo_pago"] = ((int)MetodoPago.Tarjeta).ToString(),
            ["lineas"]      = lineasJson   // ej: [{"ProductoId":1,"Cantidad":2}]
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

        // Parsear las líneas del pedido
        List<(int ProductoId, int Cantidad)> lineas;
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
                e.GetProperty("Cantidad").GetInt32()
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Webhook: error al parsear líneas del pedido {Id}.", intent.Id);
            return;
        }

        // Crear el pedido con transacción
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var lineasPedido = new List<LineaPedido>();
            var notasAjuste  = new List<string>();
            decimal total = 0;

            foreach (var (productoId, cantidad) in lineas)
            {
                var producto = await _db.Productos.FindAsync(productoId);
                if (producto is null || !producto.Activo)
                {
                    _logger.LogWarning(
                        "⚠️ Webhook: producto {PId} no disponible al reconstruir pedido {IntentId}.",
                        productoId, intent.Id);
                    continue;
                }

                if (producto.Stock != -1)
                {
                    if (producto.Stock < cantidad)
                    {
                        _logger.LogWarning(
                            "⚠️ Webhook: stock insuficiente para producto {PId} — ajustando cantidad.",
                            productoId);
                        if (producto.Stock <= 0) continue;
                        // Usar el stock disponible si no alcanza para la cantidad original
                        var cantidadReal = Math.Min(cantidad, producto.Stock);
                        notasAjuste.Add($"{producto.Nombre} (pedido: {cantidad}, servido: {cantidadReal})");
                        producto.Stock -= cantidadReal;
                        lineasPedido.Add(new LineaPedido
                        {
                            ProductoId = productoId, Cantidad = cantidadReal,
                            PrecioUnitario = producto.Precio
                        });
                        total += producto.Precio * cantidadReal;
                        continue;
                    }
                    producto.Stock -= cantidad;
                }

                lineasPedido.Add(new LineaPedido
                {
                    ProductoId = productoId, Cantidad = cantidad,
                    PrecioUnitario = producto.Precio
                });
                total += producto.Precio * cantidad;
            }

            if (lineasPedido.Count == 0)
            {
                _logger.LogError(
                    "❌ Webhook: no se pudo reconstruir ninguna línea para el pedido {Id}.",
                    intent.Id);
                await transaction.RollbackAsync();
                return;
            }

            var hoy = DateTime.UtcNow.Date;
            var ultimoNumero = await _db.Pedidos
                .Where(p => p.FechaCreacion.Date == hoy)
                .MaxAsync(p => (int?)p.NumeroPedido) ?? 0;

            var notaFinal = notas?.Trim().Replace("<", "&lt;").Replace(">", "&gt;");
            if (notasAjuste.Count > 0)
            {
                var avisoStock = "⚠️ Stock ajustado: " + string.Join(", ", notasAjuste);
                notaFinal = string.IsNullOrEmpty(notaFinal) ? avisoStock : $"{notaFinal} | {avisoStock}";
            }

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
