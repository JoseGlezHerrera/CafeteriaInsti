using System.Net.Http.Json;
using CafeIES.Shared.Models;

namespace CafeIES.Admin.Services;

public class AdminApiService
{
    private readonly HttpClient _http;

    public AdminApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<DashboardDto?> GetDashboardAsync()
    {
        try { return await _http.GetFromJsonAsync<DashboardDto>("api/admin/dashboard"); }
        catch { return null; }
    }

    public async Task<List<ProductoDto>> GetProductosAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ProductoDto>>("api/productos?soloActivos=false") ?? new(); }
        catch { return new(); }
    }

    public async Task<bool> CrearProductoAsync(CrearProductoRequest req)
    {
        try { return (await _http.PostAsJsonAsync("api/productos", req)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> ActualizarProductoAsync(int id, CrearProductoRequest req)
    {
        try { return (await _http.PutAsJsonAsync($"api/productos/{id}", req)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> ActualizarStockAsync(int id, int nuevoStock)
    {
        try { return (await _http.PatchAsJsonAsync($"api/productos/{id}/stock", new ActualizarStockRequest(nuevoStock))).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> ToggleActivoAsync(int id)
    {
        try { return (await _http.PatchAsync($"api/productos/{id}/toggle", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> EliminarProductoAsync(int id)
    {
        try { return (await _http.DeleteAsync($"api/productos/{id}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<List<CategoriaDto>> GetCategoriasAsync()
    {
        try { return await _http.GetFromJsonAsync<List<CategoriaDto>>("api/categorias") ?? new(); }
        catch { return new(); }
    }

    public async Task<List<UsuarioDto>> GetUsuariosPendientesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<UsuarioDto>>("api/admin/usuarios?estado=0") ?? new(); }
        catch { return new(); }
    }

    public async Task<List<UsuarioDto>> GetTodosUsuariosAsync()
    {
        try { return await _http.GetFromJsonAsync<List<UsuarioDto>>("api/admin/usuarios") ?? new(); }
        catch { return new(); }
    }

    public async Task<bool> ValidarAlumnoAsync(int id, bool aprobar)
    {
        try { return (await _http.PatchAsync($"api/admin/usuarios/{id}/validar?aprobar={aprobar}", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> SuspenderUsuarioAsync(int id)
    {
        try { return (await _http.PatchAsync($"api/admin/usuarios/{id}/suspender", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> ReactivarUsuarioAsync(int id)
    {
        try { return (await _http.PatchAsync($"api/admin/usuarios/{id}/reactivar", null)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<int> GetPendientesCountAsync()
    {
        try
        {
            var lista = await _http.GetFromJsonAsync<List<UsuarioDto>>("api/admin/usuarios?estado=0");
            return lista?.Count ?? 0;
        }
        catch { return 0; }
    }

    public async Task<List<InvitacionDto>> GetInvitacionesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<InvitacionDto>>("api/invitaciones") ?? new(); }
        catch { return new(); }
    }

    public async Task<InvitacionDto?> CrearInvitacionAsync(CrearInvitacionRequest req)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/invitaciones", req);
            return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<InvitacionDto>() : null;
        }
        catch { return null; }
    }

    public async Task<bool> RevocarInvitacionAsync(int id)
    {
        try { return (await _http.DeleteAsync($"api/invitaciones/{id}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public string GetQrUrl(int id) => $"{_http.BaseAddress}api/invitaciones/{id}/qr";

    public async Task<bool> CrearCategoriaAsync(string nombre, string emoji)
    {
        try { return (await _http.PostAsJsonAsync("api/categorias", new CategoriaDto(0, nombre, emoji))).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> ActualizarCategoriaAsync(int id, string nombre, string emoji)
    {
        try { return (await _http.PutAsJsonAsync($"api/categorias/{id}", new CategoriaDto(id, nombre, emoji))).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> EliminarCategoriaAsync(int id)
    {
        try { return (await _http.DeleteAsync($"api/categorias/{id}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<List<FranjaHorariaDto>> GetHorariosAsync()
    {
        try { return await _http.GetFromJsonAsync<List<FranjaHorariaDto>>("api/admin/horarios") ?? new(); }
        catch { return new(); }
    }

    public async Task<bool> CrearFranjaAsync(UpsertFranjaRequest req)
    {
        try { return (await _http.PostAsJsonAsync("api/admin/horarios", req)).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> EliminarFranjaAsync(int id)
    {
        try { return (await _http.DeleteAsync($"api/admin/horarios/{id}")).IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<List<PedidoDto>> GetPedidosAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        try
        {
            var query = desde.HasValue ? $"?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}" : string.Empty;
            return await _http.GetFromJsonAsync<List<PedidoDto>>($"api/admin/pedidos{query}") ?? new();
        }
        catch { return new(); }
    }

    public async Task<bool> CambiarEstadoPedidoAsync(int id, EstadoPedido estado)
    {
        try { return (await _http.PatchAsJsonAsync($"api/pedidos/{id}/estado", new CambiarEstadoRequest(estado))).IsSuccessStatusCode; }
        catch { return false; }
    }
}