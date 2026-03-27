using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace CafeIES.Admin.Services;

public class AdminApiService
{
    private readonly HttpClient _http;
    private readonly AuthAdminService _auth;

    public AdminApiService(HttpClient http, AuthAdminService auth)
    {
        _http = http;
        _auth = auth;
    }

    /// <summary>FIX-18: Expone la URL base de la API para conexiones SignalR.</summary>
    public string GetApiBaseUrl() => (_http.BaseAddress?.ToString().TrimEnd('/') ?? "") + "/";

    // ── Helper: ejecuta request con auto-refresh en 401 ──────────────────────
    private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> action)
    {
        var resp = await action();
        if (resp.StatusCode != HttpStatusCode.Unauthorized) return resp;

        if (!await _auth.RefrescarTokenAsync()) return resp;
        return await action();
    }

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        try
        {
            var resp = await SendAsync(() => _http.GetAsync(url));
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<T>()
                : default;
        }
        catch { return default; }
    }

    private async Task<List<T>> GetListAsync<T>(string url)
    {
        try
        {
            var resp = await SendAsync(() => _http.GetAsync(url));
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<T>>() ?? new()
                : new();
        }
        catch { return new(); }
    }

    private async Task<bool> SendBoolAsync(Func<Task<HttpResponseMessage>> action)
    {
        try { return (await SendAsync(action)).IsSuccessStatusCode; }
        catch { return false; }
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    public async Task<DashboardDto?> GetDashboardAsync(int? institutoId = null)
    {
        var url = institutoId.HasValue
            ? $"api/admin/dashboard?institutoId={institutoId}"
            : "api/admin/dashboard";
        return await GetAsync<DashboardDto>(url);
    }

    // ── Productos ─────────────────────────────────────────────────────────────
    public async Task<List<ProductoDto>> GetProductosAsync()
        => await GetListAsync<ProductoDto>("api/productos?soloActivos=false");

    public async Task<bool> CrearProductoAsync(CrearProductoRequest req)
        => await SendBoolAsync(() => _http.PostAsJsonAsync("api/productos", req));

    public async Task<bool> ActualizarProductoAsync(int id, CrearProductoRequest req)
        => await SendBoolAsync(() => _http.PutAsJsonAsync($"api/productos/{id}", req));

    public async Task<bool> ActualizarStockAsync(int id, int nuevoStock)
        => await SendBoolAsync(() => _http.PatchAsJsonAsync($"api/productos/{id}/stock", new ActualizarStockRequest(nuevoStock)));

    public async Task<bool> ToggleActivoAsync(int id)
        => await SendBoolAsync(() => _http.PatchAsync($"api/productos/{id}/toggle", null));

    public async Task<bool> EliminarProductoAsync(int id)
        => await SendBoolAsync(() => _http.DeleteAsync($"api/productos/{id}"));

    // ── Categorías ────────────────────────────────────────────────────────────
    public async Task<List<CategoriaDto>> GetCategoriasAsync()
        => await GetListAsync<CategoriaDto>("api/categorias");

    public async Task<bool> CrearCategoriaAsync(string nombre, string emoji)
        => await SendBoolAsync(() => _http.PostAsJsonAsync("api/categorias", new CategoriaDto(0, nombre, emoji)));

    public async Task<bool> ActualizarCategoriaAsync(int id, string nombre, string emoji)
        => await SendBoolAsync(() => _http.PutAsJsonAsync($"api/categorias/{id}", new CategoriaDto(id, nombre, emoji)));

    public async Task<bool> EliminarCategoriaAsync(int id)
        => await SendBoolAsync(() => _http.DeleteAsync($"api/categorias/{id}"));

    // ── Usuarios ──────────────────────────────────────────────────────────────
    public async Task<List<UsuarioDto>> GetUsuariosPendientesAsync()
        => await GetListAsync<UsuarioDto>("api/admin/usuarios?estado=0");

    public async Task<List<UsuarioDto>> GetTodosUsuariosAsync()
        => await GetListAsync<UsuarioDto>("api/admin/usuarios");

    public async Task<bool> ValidarAlumnoAsync(int id, bool aprobar)
        => await SendBoolAsync(() => _http.PatchAsync($"api/admin/usuarios/{id}/validar?aprobar={aprobar}", null));

    public async Task<bool> SuspenderUsuarioAsync(int id)
        => await SendBoolAsync(() => _http.PatchAsync($"api/admin/usuarios/{id}/suspender", null));

    public async Task<bool> ReactivarUsuarioAsync(int id)
        => await SendBoolAsync(() => _http.PatchAsync($"api/admin/usuarios/{id}/reactivar", null));

    public async Task<bool> SetDesayunoGratuitoAsync(int id, bool activo)
        => await SendBoolAsync(() => _http.PatchAsync($"api/admin/usuarios/{id}/desayuno-gratuito?activo={activo}", null));

    public async Task<object?> GetConsumosDesayunoAsync(DateOnly? fecha = null)
    {
        try
        {
            var url = fecha.HasValue
                ? $"api/admin/desayunos/consumos?fecha={fecha:yyyy-MM-dd}"
                : "api/admin/desayunos/consumos";
            var resp = await SendAsync(() => _http.GetAsync(url));
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<object>() : null;
        }
        catch { return null; }
    }

    public async Task<(bool Ok, string? Error)> EliminarUsuarioAsync(int id)
    {
        try
        {
            var resp = await SendAsync(() => _http.DeleteAsync($"api/admin/usuarios/{id}"));
            if (resp.IsSuccessStatusCode) return (true, null);
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body?.GetValueOrDefault("mensaje") ?? "Error al eliminar el usuario.");
        }
        catch { return (false, "Error de conexión."); }
    }

    public async Task<int> GetPendientesCountAsync()
    {
        try
        {
            var lista = await GetListAsync<UsuarioDto>("api/admin/usuarios?estado=0");
            return lista.Count;
        }
        catch { return 0; }
    }

    // ── Invitaciones ──────────────────────────────────────────────────────────
    public async Task<List<InvitacionDto>> GetInvitacionesAsync()
        => await GetListAsync<InvitacionDto>("api/invitaciones");

    public async Task<InvitacionDto?> CrearInvitacionAsync(CrearInvitacionRequest req)
    {
        try
        {
            var r = await SendAsync(() => _http.PostAsJsonAsync("api/invitaciones", req));
            return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<InvitacionDto>() : null;
        }
        catch { return null; }
    }

    public async Task<bool> RevocarInvitacionAsync(int id)
        => await SendBoolAsync(() => _http.DeleteAsync($"api/invitaciones/{id}"));

    public string GetQrUrl(int id) => $"{_http.BaseAddress}api/invitaciones/{id}/qr";

    // ── Institutos ──────────────────────────────────────────────────────────────
    public async Task<List<InstitutoDto>> GetInstitutosAsync()
        => await GetListAsync<InstitutoDto>("api/admin/institutos");

    // FIX-24: CRUD de institutos
    public async Task<bool> CrearInstitutoAsync(CrearInstitutoRequest req)
        => await SendBoolAsync(() => _http.PostAsJsonAsync("api/admin/institutos", req));

    public async Task<bool> ActualizarInstitutoAsync(int id, CrearInstitutoRequest req)
        => await SendBoolAsync(() => _http.PutAsJsonAsync($"api/admin/institutos/{id}", req));

    public async Task<bool> ToggleInstitutoAsync(int id)
        => await SendBoolAsync(() => _http.PatchAsync($"api/admin/institutos/{id}/toggle", null));

    // ── Alérgenos ──────────────────────────────────────────────────────────────
    public async Task<List<AlergenoDto>> GetAlergenosAsync()
        => await GetListAsync<AlergenoDto>("api/admin/alergenos");

    // ── Horarios ──────────────────────────────────────────────────────────────
    public async Task<List<FranjaHorariaDto>> GetHorariosAsync()
        => await GetListAsync<FranjaHorariaDto>("api/admin/horarios");

    public async Task<bool> CrearFranjaAsync(UpsertFranjaRequest req)
        => await SendBoolAsync(() => _http.PostAsJsonAsync("api/admin/horarios", req));

    public async Task<bool> EliminarFranjaAsync(int id)
        => await SendBoolAsync(() => _http.DeleteAsync($"api/admin/horarios/{id}"));

    // ── Pedidos (paginado — fix #1) ───────────────────────────────────────────
    public async Task<List<PedidoDto>> GetPedidosAsync(DateTime? desde = null, DateTime? hasta = null, int pageSize = 500, int? institutoId = null)
    {
        try
        {
            var qs = $"?pageSize={pageSize}";
            if (desde.HasValue)      qs += $"&desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
            if (institutoId.HasValue) qs += $"&institutoId={institutoId}";
            var resp = await SendAsync(() => _http.GetAsync($"api/admin/pedidos{qs}"));
            if (!resp.IsSuccessStatusCode) return new();
            var paginated = await resp.Content.ReadFromJsonAsync<PaginatedResponse<PedidoDto>>();
            return paginated?.Items ?? new();
        }
        catch { return new(); }
    }

    public async Task<List<PedidoDto>> GetAllPedidosAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        var all = new List<PedidoDto>();
        int page = 1;
        const int pageSize = 500;
        while (true)
        {
            try
            {
                var qs = $"?pageSize={pageSize}&page={page}";
                if (desde.HasValue) qs += $"&desde={desde:yyyy-MM-dd}";
                if (hasta.HasValue) qs += $"&hasta={hasta:yyyy-MM-dd}";
                var resp = await SendAsync(() => _http.GetAsync($"api/admin/pedidos{qs}"));
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
        => await SendBoolAsync(() => _http.PatchAsJsonAsync($"api/pedidos/{id}/estado", new CambiarEstadoRequest(estado)));

    // ── Imágenes ───────────────────────────────────────────────────────────────
    public async Task<string?> SubirImagenProductoAsync(int id, IBrowserFile archivo)
    {
        try
        {
            // BUG-005: leer bytes en memoria antes de reintentar para evitar "stream already consumed"
            using var stream = archivo.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024);
            var bytes = new byte[archivo.Size];
            await stream.ReadAsync(bytes);
            var contentType = archivo.ContentType;
            var fileName    = archivo.Name;

            var resp = await SendAsync(() =>
            {
                var content     = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Add(fileContent, "imagen", fileName);
                return _http.PostAsync($"api/productos/{id}/imagen", content);
            });
            if (!resp.IsSuccessStatusCode) return null;

            var result = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return result?.GetValueOrDefault("imagenUrl");
        }
        catch { return null; }
    }

    // ── Reportes ───────────────────────────────────────────────────────────────
    public async Task<byte[]?> DescargarExcelAsync(DateTime? desde = null, DateTime? hasta = null)
        => await DescargarBytesAsync(ConstruirUrlReporte("api/reportes/excel", desde, hasta));

    public async Task<byte[]?> DescargarPdfAsync(DateTime? desde = null, DateTime? hasta = null)
        => await DescargarBytesAsync(ConstruirUrlReporte("api/reportes/pdf", desde, hasta));

    private static string ConstruirUrlReporte(string endpoint, DateTime? desde, DateTime? hasta)
    {
        var qs = new List<string>();
        if (desde.HasValue) qs.Add($"desde={desde:yyyy-MM-dd}");
        if (hasta.HasValue) qs.Add($"hasta={hasta:yyyy-MM-dd}");
        return qs.Count > 0 ? $"{endpoint}?{string.Join("&", qs)}" : endpoint;
    }

    private async Task<byte[]?> DescargarBytesAsync(string url)
    {
        try
        {
            var resp = await SendAsync(() => _http.GetAsync(url));
            return resp.IsSuccessStatusCode ? await resp.Content.ReadAsByteArrayAsync() : null;
        }
        catch { return null; }
    }
}