using System.Net.Http.Json;
using System.Text.Json;
using CafeIES.Shared.Models;
using Microsoft.JSInterop;

namespace CafeIES.Admin.Services;

public class AuthAdminService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    private string?     _accessToken;
    private string?     _refreshToken;  // Solo en memoria — no se persiste en sessionStorage
    private UsuarioDto? _usuario;

    public AuthAdminService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js   = js;
    }

    public bool    EstaAutenticado => !string.IsNullOrEmpty(_accessToken);
    public string? Token           => _accessToken;
    public string  NombreUsuario   => _usuario?.NombreCompleto ?? "Admin";

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login",
                new LoginRequest(email, password));

            if (!resp.IsSuccessStatusCode) return false;

            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data is null) return false;

            // Solo admins pueden usar el panel
            if (data.Usuario.Rol != RolUsuario.Admin) return false;

            _accessToken  = data.AccessToken;
            _refreshToken = data.RefreshToken;   // En memoria únicamente
            _usuario      = data.Usuario;

            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            // Persistimos el access token (1h de vida) y los datos del usuario.
            // El refresh token (30 días) NO se guarda en sessionStorage por seguridad:
            // si hay una vulnerabilidad XSS podría ser robado. Al recargar la página
            // se pedirá login de nuevo, lo cual es el comportamiento correcto para un panel admin.
            await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_token",   _accessToken);
            await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_usuario",  JsonSerializer.Serialize(_usuario));

            return true;
        }
        catch { return false; }
    }

    /// <summary>Restaura el access token desde sessionStorage al recargar la página.</summary>
    public async Task RestaurarSesionAsync()
    {
        _accessToken = await _js.InvokeAsync<string?>("sessionStorage.getItem", "admin_token");
        // _refreshToken permanece null al recargar; si el access token expira se requerirá login.

        if (!string.IsNullOrEmpty(_accessToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var json = await _js.InvokeAsync<string?>("sessionStorage.getItem", "admin_usuario");
            if (!string.IsNullOrEmpty(json))
                _usuario = JsonSerializer.Deserialize<UsuarioDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }

    public async Task<bool> RefrescarTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        var resp = await _http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(_refreshToken));
        if (!resp.IsSuccessStatusCode) return false;

        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        if (data is null) return false;

        _accessToken  = data.AccessToken;
        _refreshToken = data.RefreshToken;   // Actualizar en memoria
        _usuario      = data.Usuario;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        // Actualizar solo el access token y usuario en sessionStorage
        await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_token",   _accessToken);
        await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_usuario",  JsonSerializer.Serialize(_usuario));
        return true;
    }

    public async Task LogoutAsync()
    {
        _accessToken  = null;
        _refreshToken = null;
        _usuario      = null;
        _http.DefaultRequestHeaders.Authorization = null;
        await _js.InvokeVoidAsync("sessionStorage.clear");
    }
}
