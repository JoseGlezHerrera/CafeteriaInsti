using System.Net.Http.Json;
using System.Text.Json;
using CafeIES.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.Services;

// ── Admin: usuarios, productos, horarios, invitaciones y push ─────────────────
public partial class ApiService
{
    // ── Usuarios ──────────────────────────────────────────────────────────────
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

    public async Task<bool> SetDesayunoGratuitoAsync(int id, bool activo)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/admin/usuarios/{id}/desayuno-gratuito?activo={activo}", null);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cambiar desayuno gratuito del usuario {Id}.", id);
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

    // ── Productos ─────────────────────────────────────────────────────────────
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

    /// <summary>Crea un producto y devuelve su nuevo Id, o null si falla.</summary>
    public async Task<int?> CrearProductoAsync(CrearProductoRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/productos",
                JsonContent.Create(req));
            if (!resp.IsSuccessStatusCode) return null;
            var dto = await resp.Content.ReadFromJsonAsync<ProductoDto>();
            return dto?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al crear el producto.");
            return null;
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

    // ── Franjas horarias ──────────────────────────────────────────────────────
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

    // ── Invitaciones ──────────────────────────────────────────────────────────
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
            var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
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

    // ── Ingredientes ──────────────────────────────────────────────────────────
    public async Task<List<IngredienteDto>> GetIngredientesAdminAsync()
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Get, "api/ingredientes");
            return resp.IsSuccessStatusCode
                ? await resp.Content.ReadFromJsonAsync<List<IngredienteDto>>() ?? new()
                : new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener ingredientes (admin).");
            return new();
        }
    }

    public async Task<bool> CrearIngredienteAsync(CrearIngredienteRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/ingredientes",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al crear el ingrediente.");
            return false;
        }
    }

    public async Task<bool> ActualizarIngredienteAsync(int id, CrearIngredienteRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Put, $"api/ingredientes/{id}",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al actualizar el ingrediente {Id}.", id);
            return false;
        }
    }

    public async Task<bool> ToggleActivoIngredienteAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Patch, $"api/ingredientes/{id}/toggle", null);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al cambiar el estado activo del ingrediente {Id}.", id);
            return false;
        }
    }

    public async Task<(bool Ok, string? Error)> EliminarIngredienteAsync(int id)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Delete, $"api/ingredientes/{id}");
            if (resp.IsSuccessStatusCode) return (true, null);
            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var err = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                return (false, err?.GetValueOrDefault("mensaje") ?? "No se puede eliminar: está asignado a productos.");
            }
            return (false, "Error al eliminar el ingrediente.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar el ingrediente {Id}.", id);
            return (false, "Error de conexión.");
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
}
