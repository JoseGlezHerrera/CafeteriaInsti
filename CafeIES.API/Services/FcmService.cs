using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Apis.Auth.OAuth2;

namespace CafeIES.API.Services;

/// <summary>
/// Envía notificaciones push mediante la FCM HTTP v1 API.
/// Requiere un Service Account de Firebase con el rol "Firebase Cloud Messaging API Admin".
/// Configurar en appsettings.json:
///   "Fcm": {
///     "ProjectId": "&lt;tu-project-id&gt;",
///     "ServiceAccountJson": "&lt;contenido-del-service-account.json como string&gt;"
///   }
/// Si ProjectId o ServiceAccountJson están vacíos, el servicio se deshabilita
/// silenciosamente sin romper ningún flujo de la aplicación.
/// </summary>
public class FcmService
{
    private readonly IHttpClientFactory  _httpFactory;
    private readonly IConfiguration      _config;
    private readonly ILogger<FcmService> _logger;
    private readonly ITokenAccess?       _credential; // cached per instance (singleton)

    private const string FcmScope = "https://www.googleapis.com/auth/firebase.messaging";

    public FcmService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<FcmService> logger)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;

        var json = config["Fcm:ServiceAccountJson"];
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _credential = (ITokenAccess)GoogleCredential.FromJson(json).CreateScoped(FcmScope);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo parsear Fcm:ServiceAccountJson en el constructor.");
            }
        }
    }

    /// <summary>
    /// Envía una notificación push a la lista de tokens FCM indicada.
    /// Devuelve la lista de tokens inválidos (UNREGISTERED) que deben eliminarse de la BD.
    /// Silencia cualquier otro error para no interrumpir el flujo principal del servidor.
    /// </summary>
    public async Task<List<string>> EnviarAsync(
        IEnumerable<string>         tokens,
        string                      titulo,
        string                      cuerpo,
        Dictionary<string, string>? datos = null)
    {
        var projectId = _config["Fcm:ProjectId"];
        if (string.IsNullOrEmpty(projectId))
        {
            _logger.LogDebug("FCM desactivado: Fcm:ProjectId no configurado.");
            return [];
        }

        string? accessToken;
        try
        {
            accessToken = await ObtenerAccessTokenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener el access token de FCM.");
            return [];
        }

        if (accessToken is null) return [];

        var url  = $"https://fcm.googleapis.com/v1/projects/{projectId}/messages:send";
        using var http = _httpFactory.CreateClient("fcm");

        var tokensInvalidos = new List<string>();
        foreach (var token in tokens)
        {
            var esInvalido = await EnviarATokenAsync(http, token, titulo, cuerpo, datos ?? [], url, accessToken);
            if (esInvalido) tokensInvalidos.Add(token);
        }
        return tokensInvalidos;
    }

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Returns true if the token is invalid and should be removed from the DB.</summary>
    private async Task<bool> EnviarATokenAsync(
        HttpClient                  http,
        string                      token,
        string                      titulo,
        string                      cuerpo,
        Dictionary<string, string>  datos,
        string                      url,
        string                      accessToken)
    {
        try
        {
            // Payload FCM HTTP v1 — incluye configuraciones Android e APNs
            var payload = new
            {
                message = new
                {
                    token,
                    notification = new { title = titulo, body = cuerpo },
                    data = datos,
                    android = new
                    {
                        priority = "high",
                        notification = new { sound = "default", channel_id = "pedidos" }
                    },
                    apns = new
                    {
                        headers = new Dictionary<string, string>
                        {
                            ["apns-priority"] = "10",
                            ["apns-push-type"] = "alert"
                        },
                        payload = new
                        {
                            aps = new { alert = new { title = titulo, body = cuerpo }, sound = "default", badge = 1 }
                        }
                    }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Content = JsonContent.Create(payload);

            var resp = await http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "FCM rechazó notificación al token ...{Suffix}. HTTP {Status}: {Body}",
                    token.Length > 8 ? token[^8..] : token,
                    (int)resp.StatusCode,
                    body);

                // Token expirado/desregistrado — debe eliminarse de la BD
                return resp.StatusCode == System.Net.HttpStatusCode.NotFound ||
                       body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Error de red transitorio: no marcar el token como inválido, se reintentará en la próxima notificación
            _logger.LogWarning(ex, "⚠️ Fallo de red transitorio al notificar FCM (token ...{Suffix}). Token conservado.",
                token.Length > 8 ? token[^8..] : token);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error inesperado al enviar notificación FCM al token ...{Suffix}.",
                token.Length > 8 ? token[^8..] : token);
            return false;
        }
    }

    private async Task<string?> ObtenerAccessTokenAsync()
    {
        if (_credential is null) return null;
        return await _credential.GetAccessTokenForRequestAsync();
    }
}
