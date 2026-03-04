using System.Net.Http.Json;
using CafeIES.Shared.Models;
using Microsoft.JSInterop;

namespace CafeIES.Admin.Services;

public class AuthAdminService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    private string?     _accessToken;
    private string?     _refreshToken;
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
            _refreshToken = data.RefreshToken;
            _usuario      = data.Usuario;

            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_token",   _accessToken);
            await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_refresh",  _refreshToken);

            return true;
        }
        catch { return false; }
    }

    /// <summary>Restaura la sesión desde sessionStorage (al recargar la página)</summary>
    public async Task RestaurarSesionAsync()
    {
        _accessToken  = await _js.InvokeAsync<string?>("sessionStorage.getItem", "admin_token");
        _refreshToken = await _js.InvokeAsync<string?>("sessionStorage.getItem", "admin_refresh");

        if (!string.IsNullOrEmpty(_accessToken))
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
    }

    public async Task<bool> RefrescarTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken)) return false;

        var resp = await _http.PostAsJsonAsync("api/auth/refresh", new RefreshRequest(_refreshToken));
        if (!resp.IsSuccessStatusCode) return false;

        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        if (data is null) return false;

        _accessToken  = data.AccessToken;
        _refreshToken = data.RefreshToken;
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_token",   _accessToken);
        await _js.InvokeVoidAsync("sessionStorage.setItem", "admin_refresh",  _refreshToken);
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
