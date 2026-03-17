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
    /// Confirma un PaymentIntent server-side con los datos de tarjeta.
    /// Evita llamadas directas a Stripe desde el cliente (requeriría SDK nativo).
    /// </summary>
    [HttpPost("confirmar")]
    [Authorize]
    public async Task<IActionResult> ConfirmarPago([FromBody] ConfirmarPagoRequest req)
    {
        var (succeeded, error) = await _stripe.ConfirmarPagoAsync(
            req.PaymentIntentId,
            req.CardNumber, req.ExpMonth, req.ExpYear, req.Cvc);

        if (!succeeded)
            return BadRequest(new { error });

        return Ok();
    }

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
