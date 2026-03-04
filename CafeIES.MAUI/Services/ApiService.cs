using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Services;

public enum RegistroResultado { Ok, EmailDuplicado, ErrorServidor }

public class ApiService
{
    private readonly HttpClient _http;
    private readonly TokenService _tokens;

    public string HubUrl => $"{_http.BaseAddress}hubs/cafeteria";

    public ApiService(HttpClient http, TokenService tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<string?> GetTokenAsync()
        => await _tokens.GetAccessTokenAsync();

    private async Task AdjuntarTokenAsync()
    {
        var token = await _tokens.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    // ── Auth ──────────────────────────────────────────────────────────────────
    /// <summary>Returns (response, isCuentaPendiente). Response is null on failure.</summary>
    public async Task<(LoginResponse? Data, bool CuentaPendiente)> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login",
                new LoginRequest(email, password));

            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return (null, true);

            if (!resp.IsSuccessStatusCode) return (null, false);

            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data is null) return (null, false);

            await _tokens.GuardarTokensAsync(data.AccessToken, data.RefreshToken);
            await _tokens.GuardarUsuarioAsync(data.Usuario);
            return (data, false);
        }
        catch { return (null, false); }
    }

    public async Task<RegistroResultado> RegistroAlumnoAsync(RegistroAlumnoRequest req)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/registro/alumno", req);
            if (resp.IsSuccessStatusCode) return RegistroResultado.Ok;
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict) return RegistroResultado.EmailDuplicado;
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

    // ── Productos ─────────────────────────────────────────────────────────────
    public async Task<List<ProductoDto>> GetProductosAsync()
    {
        try
        {
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<List<ProductoDto>>("api/productos") ?? new();
        }
        catch { return new(); }
    }

    public async Task<List<CategoriaDto>> GetCategoriasAsync()
    {
        try
        {
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<List<CategoriaDto>>("api/categorias") ?? new();
        }
        catch { return new(); }
    }

    // ── Horario ───────────────────────────────────────────────────────────────
    public async Task<HorarioStatusDto?> GetHorarioStatusAsync()
    {
        try
        {
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<HorarioStatusDto>("api/pedidos/puedo-pedir");
        }
        catch { return null; }
    }

    // ── Pedidos ───────────────────────────────────────────────────────────────
    public async Task<PedidoDto?> CrearPedidoAsync(CrearPedidoRequest req)
    {
        try
        {
            await AdjuntarTokenAsync();
            var resp = await _http.PostAsJsonAsync("api/pedidos", req);
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
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<List<PedidoDto>>("api/pedidos/mis-pedidos") ?? new();
        }
        catch { return new(); }
    }

    public async Task<PedidoDto?> GetPedidoAsync(int id)
    {
        try
        {
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<PedidoDto>($"api/pedidos/{id}");
        }
        catch { return null; }
    }

    // ── Admin: Pedidos ────────────────────────────────────────────────────────
    public async Task<List<PedidoDto>> GetAllPedidosAsync()
    {
        try
        {
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<List<PedidoDto>>("api/admin/pedidos") ?? new();
        }
        catch { return new(); }
    }

    public async Task<bool> CambiarEstadoPedidoAsync(int id, EstadoPedido estado)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PatchAsJsonAsync($"api/pedidos/{id}/estado", new CambiarEstadoRequest(estado))).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Admin: Usuarios ───────────────────────────────────────────────────────
    public async Task<List<UsuarioDto>> GetTodosUsuariosAsync()
    {
        try
        {
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<List<UsuarioDto>>("api/admin/usuarios") ?? new();
        }
        catch { return new(); }
    }

    public async Task<bool> ValidarAlumnoAsync(int id, bool aprobar)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PatchAsync($"api/admin/usuarios/{id}/validar?aprobar={aprobar}", null)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> SuspenderUsuarioAsync(int id)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PatchAsync($"api/admin/usuarios/{id}/suspender", null)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ReactivarUsuarioAsync(int id)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PatchAsync($"api/admin/usuarios/{id}/reactivar", null)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Admin: Productos ──────────────────────────────────────────────────────
    public async Task<List<ProductoDto>> GetProductosAdminAsync()
    {
        try
        {
            await AdjuntarTokenAsync();
            return await _http.GetFromJsonAsync<List<ProductoDto>>("api/productos?soloActivos=false") ?? new();
        }
        catch { return new(); }
    }

    public async Task<bool> CrearProductoAsync(CrearProductoRequest req)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PostAsJsonAsync("api/productos", req)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ActualizarProductoAsync(int id, CrearProductoRequest req)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PutAsJsonAsync($"api/productos/{id}", req)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ActualizarStockAsync(int id, int nuevoStock)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PatchAsJsonAsync($"api/productos/{id}/stock", new ActualizarStockRequest(nuevoStock))).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ToggleActivoAsync(int id)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.PatchAsync($"api/productos/{id}/toggle", null)).IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> EliminarProductoAsync(int id)
    {
        try
        {
            await AdjuntarTokenAsync();
            return (await _http.DeleteAsync($"api/productos/{id}")).IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
