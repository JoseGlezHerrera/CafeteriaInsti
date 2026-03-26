using Stripe;

namespace CafeIES.API.Services;

public class StripeService
{
    private readonly string _currency;

    public StripeService(IConfiguration config)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"]
            ?? throw new InvalidOperationException(
                "Stripe:SecretKey no está configurado. Añádelo como variable de entorno o en appsettings.");
        _currency = config["Stripe:Currency"] ?? "eur";
    }

    /// <summary>
    /// Crea un PaymentIntent en Stripe para el importe indicado.
    /// Devuelve el clientSecret (para el cliente) y el paymentIntentId (para verificar después).
    /// </summary>
    public async Task<(string ClientSecret, string PaymentIntentId)> CrearPaymentIntentAsync(
        decimal total, string descripcion, Dictionary<string, string>? metadata = null)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount             = (long)Math.Round(total * 100, MidpointRounding.AwayFromZero), // Stripe trabaja en céntimos
            Currency           = _currency,
            Description        = descripcion,
            Metadata           = metadata ?? new(),
            PaymentMethodTypes = new List<string> { "card" },
            ConfirmationMethod = "automatic",
        };

        var service = new PaymentIntentService();
        var intent  = await service.CreateAsync(options);

        return (intent.ClientSecret, intent.Id);
    }

    /// <summary>
    /// Verifica que un PaymentIntent esté pagado (status = "succeeded").
    /// Devuelve también el Amount (en céntimos) y el userId de los metadatos.
    /// </summary>
    public async Task<(bool Pagado, string Status, long Amount, string? MetadataUserId)> VerificarPagoAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();
        var intent  = await service.GetAsync(paymentIntentId);

        intent.Metadata.TryGetValue("userId", out var metaUserId);
        return (intent.Status == "succeeded", intent.Status, intent.Amount, metaUserId);
    }

    /// <summary>
    /// Cancela un PaymentIntent en Stripe.
    /// </summary>
    public async Task CancelarIntentAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();
        await service.CancelAsync(paymentIntentId);
    }

    /// <summary>
    /// Construye un evento Stripe a partir del body y la firma del webhook.
    /// </summary>
    public Event? ConstruirEvento(string json, string signature, string webhookSecret)
    {
        try
        {
            return EventUtility.ConstructEvent(json, signature, webhookSecret);
        }
        catch
        {
            return null;
        }
    }
}
