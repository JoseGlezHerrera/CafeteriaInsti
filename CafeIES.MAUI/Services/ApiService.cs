using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CafeIES.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace CafeIES.MAUI.Services;

public enum RegistroResultado { Ok, EmailDuplicado, ErrorServidor }

/// <summary>Razón por la que la API devuelve 403 en login.</summary>
public enum MotivoRechazo { Ninguno, Pendiente, Suspendida, Rechazada }

/// <summary>Mensaje SignalR: el estado de un pedido cambió.</summary>
public record PedidoActualizadoMessage(int PedidoId, string NuevoEstado);

/// <summary>Mensaje SignalR: se ha creado un pedido nuevo (para staff de cafetería).</summary>
public record NuevoPedidoMessage();

/// <summary>
/// Mensaje enviado cuando la sesión expira definitivamente (refresh token inválido).
/// Suscríbete a este mensaje en App.xaml.cs o AppShell para redirigir al login.
/// </summary>
public record SesionExpiradaMessage();

// Clase principal — los métodos de dominio están en archivos partial:
//   ApiService.Auth.cs    — Login, registro, cambio de contraseña
//   ApiService.Pagos.cs   — Stripe PaymentIntent
//   ApiService.Catalog.cs — Institutos, alérgenos, productos, categorías, horario, desayuno
//   ApiService.Pedidos.cs — Pedidos (usuario, empleado, admin)
//   ApiService.Admin.cs   — Usuarios, productos, horarios, invitaciones, push
public partial class ApiService
{
    private readonly HttpClient _http;
    private readonly TokenService _tokens;
    private readonly ILogger<ApiService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public string HubUrl => $"{_http.BaseAddress}hubs/cafeteria";
    public string ApiBaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    /// <summary>Construye la URL absoluta de una imagen relativa devuelta por la API.</summary>
    public string BuildImageUrl(string relativePath)
        => $"{_http.BaseAddress!.ToString().TrimEnd('/')}{relativePath}";

    // ── SignalR ───────────────────────────────────────────────────────────────
    private HubConnection? _hub;
    public HubConnection? Hub => _hub;

    public ApiService(HttpClient http, TokenService tokens, ILogger<ApiService> logger)
    {
        _http   = http;
        _tokens = tokens;
        _logger = logger;

        // Warmup: ping en background para despertar Azure App Service y reducir
        // la latencia del primer login. Se ignora cualquier error.
        _ = Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _http.GetAsync("health", cts.Token);
            }
            catch { /* best-effort */ }
        });
    }

    public async Task<string?> GetTokenAsync()
        => await _tokens.GetAccessTokenAsync();

    /// <summary>Crea un request con el token Bearer sin mutar DefaultRequestHeaders.</summary>
    private async Task<HttpRequestMessage> CrearRequestAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        var token = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    /// <summary>
    /// Ejecuta un request. Si recibe 401, refresca el token e intenta de nuevo.
    /// Si el refresh falla, desconecta SignalR y notifica al resto de la app.
    /// </summary>
    private async Task<HttpResponseMessage> EnviarConRefreshAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        // Pre-leer el content a bytes para poder reconstruirlo en el retry.
        // Los streams de HttpContent se agotan al primer SendAsync y no son reutilizables.
        byte[]? bytes = null;
        string mediaType = "application/json";
        if (content is not null)
        {
            bytes     = await content.ReadAsByteArrayAsync();
            mediaType = content.Headers.ContentType?.MediaType ?? "application/json";
        }
        HttpContent? Build() => bytes is null ? null
            : new ByteArrayContent(bytes)
              { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType) } };

        var request  = await CrearRequestAsync(method, url, Build());
        var response = await _http.SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // Intentar refresh
        if (!await IntentarRefreshAsync())
        {
            // Sesión expirada definitivamente: limpiar estado y notificar
            await DesconectarSignalRAsync();
            _logger.LogWarning("Sesión expirada definitivamente. Enviando SesionExpiradaMessage y forzando navegación al login.");
            WeakReferenceMessenger.Default.Send(new SesionExpiradaMessage());

            // Fallback: navegar directamente al login por si nadie procesa el mensaje
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try { await Shell.Current.GoToAsync("//Login"); }
                catch (Exception ex) { _logger.LogError(ex, "Error navegando a login tras sesión expirada."); }
            });

            return response;
        }

        // Refresh exitoso: reconectar SignalR si se había desconectado
        if (_hub is null || _hub.State == HubConnectionState.Disconnected)
        {
            try
            {
                _hub = null;
                await ConectarSignalRAsync();
                _logger.LogInformation("SignalR reconectado tras refresh de token.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo reconectar SignalR tras refresh de token.");
            }
        }

        // Re-intentar con nuevo token y content reconstruido desde los bytes originales
        var retry = await CrearRequestAsync(method, url, Build());
        return await _http.SendAsync(retry);
    }

    /// <summary>Refresca el access token usando el refresh token almacenado.</summary>
    private async Task<bool> IntentarRefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            var refreshToken = await _tokens.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken)) return false;

            var resp = await _http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(refreshToken));
            if (!resp.IsSuccessStatusCode) return false;

            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data is null) return false;

            await _tokens.GuardarTokensAsync(data.AccessToken, data.RefreshToken);
            await _tokens.GuardarUsuarioAsync(data.Usuario);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al intentar refrescar el token de acceso.");
            return false;
        }
        finally { _refreshLock.Release(); }
    }

    // ── SignalR ───────────────────────────────────────────────────────────────
    public async Task ConectarSignalRAsync()
    {
        if (_hub is not null) return;

        _hub = new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                options.AccessTokenProvider = () => _tokens.GetAccessTokenAsync()!;
#if DEBUG
                // Solo en desarrollo: aceptar certificados autofirmados.
                // NUNCA habilitar en producción.
                options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                };
#endif
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<object>("NuevoPedido", _ =>
        {
            WeakReferenceMessenger.Default.Send(new NuevoPedidoMessage());
        });

        _hub.On<object>("EstadoPedidoActualizado", raw =>
        {
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(raw.ToString()!);
                var id = doc.RootElement.GetProperty("id").GetInt32();
                var estado = doc.RootElement.GetProperty("estado").GetString() ?? string.Empty;
                WeakReferenceMessenger.Default.Send(new PedidoActualizadoMessage(id, estado));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Payload inesperado en EstadoPedidoActualizado.");
            }
        });

        // FIX-13: Reconectar SignalR tras desconexión definitiva.
        _hub.Closed += async (ex) =>
        {
            var hub = _hub; // captura antes del delay
            _logger.LogWarning(ex, "SignalR desconectado. Reintentando en 5 segundos...");
            await Task.Delay(5000);
            if (hub is null || hub != _hub) return;
            try
            {
                await hub.StartAsync();
                _logger.LogInformation("SignalR reconectado tras desconexión.");
            }
            catch (Exception reconnectEx)
            {
                _logger.LogWarning(reconnectEx, "No se pudo reconectar a SignalR tras desconexión.");
            }
        };

        try
        {
            await _hub.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo conectar a SignalR. La actualización en tiempo real no estará disponible.");
        }
    }

    public async Task DesconectarSignalRAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }

    /// <summary>
    /// D-4: Cierra la sesión explícitamente (p. ej. tras cambio de contraseña).
    /// Limpia los tokens locales, desconecta SignalR y navega al login.
    /// </summary>
    public async Task CerrarSesionAsync()
    {
        await DesconectarSignalRAsync();
        _tokens.LimpiarTokens();
        WeakReferenceMessenger.Default.Send(new SesionExpiradaMessage());
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try { await Shell.Current.GoToAsync("//Login"); }
            catch (Exception ex) { _logger.LogError(ex, "Error navegando a login tras cerrar sesión."); }
        });
    }
}
