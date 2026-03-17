using Stripe;

namespace CafeIES.API.Services;

public class StripeService
{
    private readonly string _currency;

    public StripeService(IConfiguration config)
    {
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
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
            Amount             = (long)(total * 100), // Stripe trabaja en céntimos
            Currency           = _currency,
            Description        = descripcion,
            Metadata           = metadata ?? new(),
            PaymentMethodTypes = new List<string> { "card" },
            ConfirmationMethod = "manual",
        };

        var service = new PaymentIntentService();
        var intent  = await service.CreateAsync(options);

        return (intent.ClientSecret, intent.Id);
    }

    /// <summary>
    /// Verifica que un PaymentIntent esté pagado (status = "succeeded").
    /// </summary>
    public async Task<(bool Pagado, string Status)> VerificarPagoAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();
        var intent  = await service.GetAsync(paymentIntentId);

        return (intent.Status == "succeeded", intent.Status);
    }

    /// <summary>
    /// Crea un PaymentMethod y confirma el PaymentIntent server-side usando la secret key.
    /// Más fiable que la confirmación client-side, que requiere SDK nativo de Stripe.
    /// </summary>
    public async Task<(bool Succeeded, string Error)> ConfirmarPagoAsync(
        string paymentIntentId,
        string cardNumber, string expMonth, string expYear, string cvc)
    {
        // 1. Crear PaymentMethod con los datos de tarjeta
        var pmOptions = new PaymentMethodCreateOptions
        {
            Type = "card",
            Card = new PaymentMethodCardOptions
            {
                Number   = cardNumber,
                ExpMonth = long.Parse(expMonth),
                ExpYear  = long.Parse(expYear),
                Cvc      = cvc,
            },
        };
        var pm     = await new PaymentMethodService().CreateAsync(pmOptions);

        // 2. Confirmar el PaymentIntent adjuntando el PaymentMethod
        var piOptions = new PaymentIntentConfirmOptions
        {
            PaymentMethod = pm.Id,
        };
        var intent = await new PaymentIntentService().ConfirmAsync(paymentIntentId, piOptions);

        return (intent.Status == "succeeded",
                intent.LastPaymentError?.Message ?? string.Empty);
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
