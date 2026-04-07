using System.Net.Http.Json;
using CafeIES.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.Services;

// ── Catálogo: institutos, alérgenos, productos, categorías, horario, desayuno ─
public partial class ApiService
{
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

    public async Task<List<AlergenoDto>> GetAlergenosAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/alergenos");
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

    public async Task<DesayunoStatusDto?> GetDesayunoStatusAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/pedidos/desayuno-status");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<DesayunoStatusDto>() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener estado de desayuno gratuito.");
            return null;
        }
    }
}
