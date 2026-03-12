using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CafeIES.Shared.Models;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.SignalR.Client;

namespace CafeIES.MAUI.Services;

public enum RegistroResultado { Ok, EmailDuplicado, ErrorServidor }

/// <summary>Razón por la que la API devuelve 403 en login.</summary>
public enum MotivoRechazo { Ninguno, Pendiente, Suspendida, Rechazada }

/// <summary>Mensaje SignalR: el estado de un pedido cambió.</summary>
public record PedidoActualizadoMessage(int PedidoId, string NuevoEstado);

public class ApiService
{
    private readonly HttpClient _http;
    private readonly TokenService _tokens;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public string HubUrl => $"{_http.BaseAddress}hubs/cafeteria";

    // ── SignalR ───────────────────────────────────────────────────────────────
    private HubConnection? _hub;
    public HubConnection? Hub => _hub;

    public ApiService(HttpClient http, TokenService tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<string?> GetTokenAsync()
        => await _tokens.GetAccessTokenAsync();

    /// <summary>Crea un request con el token Bearer sin mutar DefaultRequestHeaders (#4).</summary>
    private async Task<HttpRequestMessage> CrearRequestAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        var token = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    /// <summary>Ejecuta un request. Si recibe 401, refresca el token e intenta de nuevo (#1).</summary>
    private async Task<HttpResponseMessage> EnviarConRefreshAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        var request = await CrearRequestAsync(method, url, content);
        var response = await _http.SendAsync(request);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // Intentar refresh
        if (!await IntentarRefreshAsync())
            return response;

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
        catch { return false; }
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
                catch { return (null, MotivoRechazo.Pendiente); }
            }

            if (!resp.IsSuccessStatusCode) return (null, MotivoRechazo.Ninguno);

            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data is null) return (null, MotivoRechazo.Ninguno);

            await _tokens.GuardarTokensAsync(data.AccessToken, data.RefreshToken);
            await _tokens.GuardarUsuarioAsync(data.Usuario);
            return (data, MotivoRechazo.Ninguno);
        }
        catch { return (null, MotivoRechazo.Ninguno); }
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
        catch { return RegistroResultado.ErrorServidor; }
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
        catch { return null; }
    }

    public async Task<bool> CambiarPasswordAsync(CambiarPasswordRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/auth/cambiar-password",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Institutos ──────────────────────────────────────────────────────────────
    public async Task<List<InstitutoDto>> GetInstitutosAsync()
    {
        try
        {
            var list = await _http.GetFromJsonAsync<List<InstitutoDto>>("api/institutos");
            return list ?? [];
        }
        catch { return []; }
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
        catch { return new(); }
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
        catch { return new(); }
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
        catch { return null; }
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
        catch { return null; }
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
        catch { return new(); }
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
        catch { return null; }
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
        catch { return null; }
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
            catch { break; }
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
        catch { return false; }
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
        catch { return new(); }
    }

    public async Task<bool> ValidarAlumnoAsync(int id, bool aprobar)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/admin/usuarios/{id}/validar?aprobar={aprobar}", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> SuspenderUsuarioAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/admin/usuarios/{id}/suspender", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ReactivarUsuarioAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/admin/usuarios/{id}/reactivar", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
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
        catch { return (false, "Error de conexión."); }
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
        catch { return new(); }
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
        catch { return null; }
    }

    public async Task<bool> CrearProductoAsync(CrearProductoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/productos",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ActualizarProductoAsync(int id, CrearProductoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Put, $"api/productos/{id}",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ActualizarStockAsync(int id, int nuevoStock)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/productos/{id}/stock",
                JsonContent.Create(new ActualizarStockRequest(nuevoStock)));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ToggleActivoAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/productos/{id}/toggle", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> EliminarProductoAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Delete, $"api/productos/{id}");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── SignalR (#10) ─────────────────────────────────────────────────────────
    public async Task ConectarSignalRAsync()
    {
        if (_hub is not null) return;

        var token = await _tokens.GetAccessTokenAsync();
        _hub = new HubConnectionBuilder()
            .WithUrl(HubUrl, options =>
            {
                options.AccessTokenProvider = () => _tokens.GetAccessTokenAsync()!;
                options.HttpMessageHandlerFactory = _ => new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                };
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
            catch { /* payload inesperado */ }
        });

        try { await _hub.StartAsync(); }
        catch { /* SignalR no es crítico */ }
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
