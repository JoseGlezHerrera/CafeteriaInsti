using System.Net.Http.Json;
using CafeIES.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.Services;

// ── Pedidos (usuario, empleado y admin) ───────────────────────────────────────
public partial class ApiService
{
    public async Task<(PedidoDto? Pedido, string? Error)> CrearPedidoAsync(CrearPedidoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/pedidos",
                JsonContent.Create(req));
            if (resp.IsSuccessStatusCode)
                return (await resp.Content.ReadFromJsonAsync<PedidoDto>(), null);

            string? mensajeServidor = null;
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                mensajeServidor = err?.GetValueOrDefault("mensaje");
            }
            catch { /* body no es JSON */ }

            return (null, mensajeServidor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear el pedido.");
            return (null, null);
        }
    }

    /// <summary>
    /// Recupera el pedido asociado a un PaymentIntent. Devuelve null si no existe o hay error.
    /// Usado para recuperarse de un timeout en CrearPedidoAsync.
    /// </summary>
    public async Task<PedidoDto?> GetPedidoByIntentAsync(string paymentIntentId)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get,
                $"api/pedidos/by-intent/{Uri.EscapeDataString(paymentIntentId)}");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<PedidoDto>()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al recuperar pedido por PaymentIntent.");
            return null;
        }
    }

    public async Task<List<PedidoDto>> GetMisPedidosAsync(int page = 1, int pageSize = 20)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get,
                $"api/pedidos/mis-pedidos?page={page}&pageSize={pageSize}");
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

    // ── Empleado: Historial pedidos del día (todos los estados) ──────────────
    public async Task<List<PedidoDto>> GetHistorialStaffAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/pedidos/historial");
            if (!resp.IsSuccessStatusCode) return new();
            return await resp.Content.ReadFromJsonAsync<List<PedidoDto>>() ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en GetHistorialStaffAsync.");
            return new();
        }
    }

    // ── Admin: Pedidos (paginado) ─────────────────────────────────────────────
    public async Task<PaginatedResponse<PedidoDto>?> GetPedidosAdminPaginadoAsync(
        int page = 1, int pageSize = 30, int? institutoId = null, DateTime? desde = null)
    {
        try
        {
            var institutoParam = institutoId.HasValue ? $"&institutoId={institutoId}" : "";
            var desdeParam     = desde.HasValue ? $"&desde={desde.Value:yyyy-MM-dd}" : "";
            var resp = await EnviarConRefreshAsync(HttpMethod.Get,
                $"api/admin/pedidos?page={page}&pageSize={pageSize}{institutoParam}{desdeParam}");
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PaginatedResponse<PedidoDto>>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener pedidos admin (página {Page}).", page);
            return null;
        }
    }

    // ── Admin: Pedidos (todos - legacy para reportes) ─────────────────────────
    public async Task<List<PedidoDto>> GetAllPedidosAsync(int? institutoId = null)
    {
        var all = new List<PedidoDto>();
        int page = 1;
        const int pageSize = 500;
        var institutoParam = institutoId.HasValue ? $"&institutoId={institutoId}" : "";
        while (true)
        {
            try
            {
                var resp = await EnviarConRefreshAsync(HttpMethod.Get,
                    $"api/admin/pedidos?pageSize={pageSize}&page={page}{institutoParam}");
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
}
