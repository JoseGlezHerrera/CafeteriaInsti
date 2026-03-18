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

/// <summary>
/// Mensaje enviado cuando la sesión expira definitivamente (refresh token inválido).
/// Suscríbete a este mensaje en App.xaml.cs o AppShell para redirigir al login.
/// </summary>
public record SesionExpiradaMessage();

public class ApiService
{
    private readonly HttpClient _http;
    private readonly TokenService _tokens;
    private readonly ILogger<ApiService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public string HubUrl => $"{_http.BaseAddress}hubs/cafeteria";
    public string ApiBaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    /// <summary>Construye la URL absoluta de una imagen relativa devuelta por la API (ej: /uploads/productos/1_abc.jpg).</summary>
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
        var request = await CrearRequestAsync(method, url, content);
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
                try { await Shell.Current.GoToAsync("//LoginPage"); }
                catch (Exception ex) { _logger.LogError(ex, "Error navegando a login tras sesión expirada."); }
            });

            return response;
        }

        // Refresh exitoso: reconectar SignalR si se había desconectado o eliminado
        if (_hub is null || _hub.State == HubConnectionState.Disconnected)
        {
            try
            {
                _hub = null; // asegurar limpieza antes de reconectar
                await ConectarSignalRAsync();
                _logger.LogInformation("SignalR reconectado tras refresh de token.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo reconectar SignalR tras refresh de token.");
            }
        }

        // Re-intentar con nuevo token
        var retry = await CrearRequestAsync(method, url, content);
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

    // ── Auth ──────────────────────────────────────────────────────────────────
    public async Task<(LoginResponse? Data, MotivoRechazo Motivo)> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login",
                new LoginRequest(email, password));

            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                try
                {
                    var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    var motivo = body?.GetValueOrDefault("motivo") switch
                    {
                        "pendiente"  => MotivoRechazo.Pendiente,
                        "suspendida" => MotivoRechazo.Suspendida,
                        "rechazada"  => MotivoRechazo.Rechazada,
                        _            => MotivoRechazo.Pendiente
                    };
                    return (null, motivo);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo leer el motivo del rechazo en login.");
                    return (null, MotivoRechazo.Pendiente);
                }
            }

            if (!resp.IsSuccessStatusCode) return (null, MotivoRechazo.Ninguno);

            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data is null) return (null, MotivoRechazo.Ninguno);

            await _tokens.GuardarTokensAsync(data.AccessToken, data.RefreshToken);
            await _tokens.GuardarUsuarioAsync(data.Usuario);
            return (data, MotivoRechazo.Ninguno);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en LoginAsync.");
            return (null, MotivoRechazo.Ninguno);
        }
    }

    public async Task<RegistroResultado> RegistroAlumnoAsync(RegistroAlumnoRequest req)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/registro/alumno", req);
            if (resp.IsSuccessStatusCode) return RegistroResultado.Ok;
            if (resp.StatusCode == HttpStatusCode.Conflict) return RegistroResultado.EmailDuplicado;
            return RegistroResultado.ErrorServidor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en RegistroAlumnoAsync.");
            return RegistroResultado.ErrorServidor;
        }
    }

    public async Task<LoginResponse?> RegistroInvitadoAsync(RegistroInvitadoRequest req)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/registro/invitacion", req);
            if (!resp.IsSuccessStatusCode) return null;
            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data is null) return null;
            await _tokens.GuardarTokensAsync(data.AccessToken, data.RefreshToken);
            await _tokens.GuardarUsuarioAsync(data.Usuario);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en RegistroInvitadoAsync.");
            return null;
        }
    }

    public async Task<RegistroResultado> RegistroEmpleadoAsync(RegistroEmpleadoRequest req)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/registro/empleado", req);
            if (resp.IsSuccessStatusCode) return RegistroResultado.Ok;
            if (resp.StatusCode == HttpStatusCode.Conflict) return RegistroResultado.EmailDuplicado;
            return RegistroResultado.ErrorServidor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en RegistroEmpleadoAsync.");
            return RegistroResultado.ErrorServidor;
        }
    }

    public async Task<bool> CambiarPasswordAsync(CambiarPasswordRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/auth/cambiar-password",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en CambiarPasswordAsync.");
            return false;
        }
    }

    // ── Pagos (Stripe) ────────────────────────────────────────────────────────
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

    public async Task<PagoIntentResponse?> CrearPagoIntentAsync(CrearPagoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/pagos/crear-intent",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<PagoIntentResponse>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al crear el PaymentIntent.");
            return null;
        }
    }

    // ── Institutos ────────────────────────────────────────────────────────────
    public async Task<List<InstitutoDto>> GetInstitutosAsync()
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<InstitutoDto>>("api/institutos");
            return list ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener la lista de institutos.");
            return [];
        }
    }

    // ── Alérgenos ─────────────────────────────────────────────────────────────
    public async Task<List<AlergenoDto>> GetAlergenosAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/admin/alergenos");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<AlergenoDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener la lista de alérgenos.");
            return new();
        }
    }

    // ── Productos ─────────────────────────────────────────────────────────────
    public async Task<List<ProductoDto>> GetProductosAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/productos");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<ProductoDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener productos.");
            return new();
        }
    }

    public async Task<List<CategoriaDto>> GetCategoriasAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/categorias");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<CategoriaDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener categorías.");
            return new();
        }
    }

    // ── Horario ───────────────────────────────────────────────────────────────
    public async Task<HorarioStatusDto?> GetHorarioStatusAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/pedidos/puedo-pedir");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<HorarioStatusDto>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar el estado de horario.");
            return null;
        }
    }

    // ── Pedidos ───────────────────────────────────────────────────────────────
    public async Task<PedidoDto?> CrearPedidoAsync(CrearPedidoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/pedidos",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<PedidoDto>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear el pedido.");
            return null;
        }
    }

    public async Task<List<PedidoDto>> GetMisPedidosAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/pedidos/mis-pedidos");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<PedidoDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener mis pedidos.");
            return new();
        }
    }

    public async Task<UsuarioStatsDto?> GetMisEstadisticasAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/pedidos/mis-stats");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<UsuarioStatsDto>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener estadísticas.");
            return null;
        }
    }

    public async Task<PedidoDto?> GetPedidoAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, $"api/pedidos/{id}");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<PedidoDto>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener el pedido {Id}.", id);
            return null;
        }
    }

    // ── Empleado: Pedidos en curso ────────────────────────────────────────────
    public async Task<List<PedidoDto>> GetPedidosEnCursoAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/pedidos/en-curso");
            if (!resp.IsSuccessStatusCode) return new();
            return await resp.Content.ReadFromJsonAsync<List<PedidoDto>>() ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en GetPedidosEnCursoAsync.");
            return new();
        }
    }

    // ── Admin: Pedidos ────────────────────────────────────────────────────────
    public async Task<List<PedidoDto>> GetAllPedidosAsync()
    {
        var all = new List<PedidoDto>();
        int page = 1;
        const int pageSize = 500;
        while (true)
        {
            try
            {
                var resp = await EnviarConRefreshAsync(HttpMethod.Get,
                    $"api/admin/pedidos?pageSize={pageSize}&page={page}");
                if (!resp.IsSuccessStatusCode) break;
                var paginated = await resp.Content.ReadFromJsonAsync<PaginatedResponse<PedidoDto>>();
                if (paginated is null || paginated.Items.Count == 0) break;
                all.AddRange(paginated.Items);
                if (all.Count >= paginated.TotalCount) break;
                page++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener pedidos admin (página {Page}).", page);
                break;
            }
        }
        return all;
    }

    public async Task<bool> CambiarEstadoPedidoAsync(int id, EstadoPedido estado)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/pedidos/{id}/estado",
                JsonContent.Create(new CambiarEstadoRequest(estado)));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cambiar el estado del pedido {Id}.", id);
            return false;
        }
    }

    // ── Admin: Usuarios ───────────────────────────────────────────────────────
    public async Task<List<UsuarioDto>> GetTodosUsuariosAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/admin/usuarios");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<UsuarioDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener la lista de usuarios.");
            return new();
        }
    }

    public async Task<bool> ValidarAlumnoAsync(int id, bool aprobar)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/admin/usuarios/{id}/validar?aprobar={aprobar}", null);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al validar al alumno {Id}.", id);
            return false;
        }
    }

    public async Task<bool> SuspenderUsuarioAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/admin/usuarios/{id}/suspender", null);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al suspender al usuario {Id}.", id);
            return false;
        }
    }

    public async Task<bool> ReactivarUsuarioAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/admin/usuarios/{id}/reactivar", null);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al reactivar al usuario {Id}.", id);
            return false;
        }
    }

    public async Task<(bool Ok, string? Error)> EliminarUsuarioAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Delete, $"api/admin/usuarios/{id}");
            if (resp.IsSuccessStatusCode) return (true, null);
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body?.GetValueOrDefault("mensaje") ?? "Error al eliminar el usuario.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar al usuario {Id}.", id);
            return (false, "Error de conexión.");
        }
    }

    // ── Admin: Productos ──────────────────────────────────────────────────────
    public async Task<List<ProductoDto>> GetProductosAdminAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/productos?soloActivos=false");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<ProductoDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener productos (admin).");
            return new();
        }
    }

    public async Task<ProductoDto?> GetProductoByIdAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, $"api/productos/{id}");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<ProductoDto>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener el producto {Id}.", id);
            return null;
        }
    }

    public async Task<bool> CrearProductoAsync(CrearProductoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/productos",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al crear el producto.");
            return false;
        }
    }

    public async Task<bool> ActualizarProductoAsync(int id, CrearProductoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Put, $"api/productos/{id}",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al actualizar el producto {Id}.", id);
            return false;
        }
    }

    public async Task<bool> ActualizarStockAsync(int id, int nuevoStock)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/productos/{id}/stock",
                JsonContent.Create(new ActualizarStockRequest(nuevoStock)));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al actualizar el stock del producto {Id}.", id);
            return false;
        }
    }

    public async Task<bool> ToggleActivoAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/productos/{id}/toggle", null);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cambiar el estado activo del producto {Id}.", id);
            return false;
        }
    }

    public async Task<bool> EliminarProductoAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Delete, $"api/productos/{id}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar el producto {Id}.", id);
            return false;
        }
    }

    public async Task<string?> SubirImagenProductoAsync(int id, Stream stream, string fileName, string contentType)
    {
        try
        {
            using var content     = new MultipartFormDataContent();
            var       fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "imagen", fileName);

            var resp = await EnviarConRefreshAsync(HttpMethod.Post, $"api/productos/{id}/imagen", content);
            if (!resp.IsSuccessStatusCode) return null;

            var result = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return result?.GetValueOrDefault("imagenUrl");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al subir la imagen del producto {Id}.", id);
            return null;
        }
    }

    // ── Admin: Franjas horarias ───────────────────────────────────────────────
    public async Task<List<FranjaHorariaDto>> GetHorariosAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/admin/horarios");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<FranjaHorariaDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener las franjas horarias.");
            return new();
        }
    }

    public async Task<bool> CrearFranjaAsync(UpsertFranjaRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/admin/horarios",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al crear la franja horaria.");
            return false;
        }
    }

    public async Task<bool> ActualizarFranjaAsync(int id, UpsertFranjaRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Put, $"api/admin/horarios/{id}",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al actualizar la franja horaria {Id}.", id);
            return false;
        }
    }

    public async Task<bool> EliminarFranjaAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Delete, $"api/admin/horarios/{id}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar la franja horaria {Id}.", id);
            return false;
        }
    }

    // ── Admin: Invitaciones ───────────────────────────────────────────────────
    public async Task<List<InvitacionDto>> GetInvitacionesAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/invitaciones");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<InvitacionDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener las invitaciones.");
            return new();
        }
    }

    public async Task<bool> CrearInvitacionAsync(CrearInvitacionRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/invitaciones",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al crear la invitación.");
            return false;
        }
    }

    public async Task<bool> EliminarInvitacionAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Delete, $"api/invitaciones/{id}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al revocar la invitación {Id}.", id);
            return false;
        }
    }

    public async Task<(bool Valida, string Tipo, string Token)> ValidarInvitacionAsync(string token)
    {
        try
        {
            var resp = await _http.GetAsync($"api/invitaciones/validar/{token}");
            if (!resp.IsSuccessStatusCode) return (false, string.Empty, string.Empty);
            var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var valida = json.GetProperty("valida").GetBoolean();
            var tipo   = json.GetProperty("tipo").GetString() ?? string.Empty;
            var tok    = json.GetProperty("token").GetString() ?? string.Empty;
            return (valida, tipo, tok);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al validar token de invitación.");
            return (false, string.Empty, string.Empty);
        }
    }

    // ── Notificaciones push ───────────────────────────────────────────────────
    public async Task RegistrarTokenPushAsync(string token, string plataforma)
    {
        try
        {
            await EnviarConRefreshAsync(HttpMethod.Post, "api/notificaciones/token",
                JsonContent.Create(new CafeIES.Shared.Models.RegistrarTokenRequest(token, plataforma)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al registrar el token FCM en la API.");
        }
    }

    public async Task EliminarTokenPushAsync(string token)
    {
        try
        {
            await EnviarConRefreshAsync(HttpMethod.Delete, "api/notificaciones/token",
                JsonContent.Create(new CafeIES.Shared.Models.EliminarTokenRequest(token)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar el token FCM de la API.");
        }
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
}
