using System.Security.Claims;
using CafeIES.API.Data;
using CafeIES.API.Services;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/pagos")]
public class PagosController : ControllerBase
{
    private readonly AppDbContext   _db;
    private readonly StripeService  _stripe;
    private readonly IConfiguration _config;

    public PagosController(AppDbContext db, StripeService stripe, IConfiguration config)
    {
        _db     = db;
        _stripe = stripe;
        _config = config;
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
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anon";
        var metadata = new Dictionary<string, string>
        {
            ["userId"] = userId,
            ["notas"]  = req.Notas ?? ""
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

        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<PagosController>>();

        switch (stripeEvent.Type)
        {
            case EventTypes.PaymentIntentSucceeded:
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent is not null)
                {
                    logger.LogInformation(
                        "✅ Webhook: PaymentIntent {Id} succeeded — {Amount} céntimos",
                        intent.Id, intent.Amount);

                    // Comprobar si ya existe un pedido con esta referencia
                    var existePedido = await _db.Pedidos
                        .AnyAsync(p => p.ReferenciasPago == intent.Id);

                    if (!existePedido)
                    {
                        logger.LogWarning(
                            "⚠️ Pago {Id} completado pero sin pedido asociado. " +
                            "El usuario puede haber cerrado la app antes de confirmar.",
                            intent.Id);
                    }
                }
                break;
            }

            case EventTypes.PaymentIntentPaymentFailed:
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent is not null)
                {
                    logger.LogWarning(
                        "❌ Webhook: PaymentIntent {Id} failed — {Error}",
                        intent.Id,
                        intent.LastPaymentError?.Message ?? "sin detalle");
                }
                break;
            }

            default:
                logger.LogDebug("Webhook Stripe: evento {Type} ignorado", stripeEvent.Type);
                break;
        }

        return Ok();
    }
}
