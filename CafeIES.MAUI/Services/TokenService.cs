using System.Text.Json;
using CafeIES.Shared.Models;

namespace CafeIES.MAUI.Services;

/// <summary>
/// Gestiona el almacenamiento seguro de los tokens JWT y datos del usuario.
/// Usa SecureStorage de MAUI (Keychain en iOS, EncryptedSharedPreferences en Android).
/// </summary>
public class TokenService
{
    private const string AccessTokenKey  = "access_token";
    private const string RefreshTokenKey = "refresh_token";
    private const string UserKey         = "user_data";

    public async Task GuardarTokensAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey,  accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public async Task GuardarUsuarioAsync(UsuarioDto usuario)
    {
        var json = JsonSerializer.Serialize(usuario);
        await SecureStorage.Default.SetAsync(UserKey, json);
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<UsuarioDto?> GetUsuarioAsync()
    {
        var json = await SecureStorage.Default.GetAsync(UserKey);
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<UsuarioDto>(json, _jsonOptions); }
        catch { return null; }
    }

    public Task<string?> GetAccessTokenAsync()
        => SecureStorage.Default.GetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync()
        => SecureStorage.Default.GetAsync(RefreshTokenKey);

    public void LimpiarTokens()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(UserKey);
    }

    public async Task<bool> HaySessionAsync()
    {
        var token = await GetAccessTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
}
