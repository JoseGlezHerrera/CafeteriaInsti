using System.Net.Http.Json;
using CafeIES.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.Services;

// ── Pagos (Stripe) ────────────────────────────────────────────────────────────
public partial class ApiService
{
    public async Task<StripeConfigDto?> GetStripeConfigAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<StripeConfigDto>("api/pagos/config");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener la configuración de Stripe.");
            return null;
        }
    }

    /// <summary>
    /// Crea un PaymentIntent. Devuelve (respuesta, mensajeError).
    /// Si el servidor devuelve 400/403 con {"mensaje":"..."}, ese texto se devuelve como error.
    /// </summary>
    public async Task<(PagoIntentResponse? Intent, string? Error)> CrearPagoIntentAsync(CrearPagoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/pagos/crear-intent",
                JsonContent.Create(req));
            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<PagoIntentResponse>(), null);

            string? mensajeServidor = null;
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                mensajeServidor = err?.GetValueOrDefault("mensaje");
            }
            catch { /* ignorar si el body no es JSON */ }

            return (null, mensajeServidor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al crear el PaymentIntent.");
            return (null, null);
        }
    }

    // FIX-10: Cancelar PaymentIntent al abandonar el pago
    public async Task CancelarPagoIntentAsync(string paymentIntentId)
    {
        try
        {
            await EnviarConRefreshAsync(HttpMethod.Post, "api/pagos/cancelar-intent",
                JsonContent.Create(new CancelarIntentRequest(paymentIntentId)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cancelar el PaymentIntent {Id}.", paymentIntentId);
        }
    }
}
