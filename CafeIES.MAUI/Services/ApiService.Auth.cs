using System.Net;
using System.Net.Http.Json;
using CafeIES.Shared.Models;
using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.Services;

// ── Autenticación y registro ──────────────────────────────────────────────────
public partial class ApiService
{
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo leer el motivo del rechazo en login.");
                    return (null, MotivoRechazo.Pendiente);
                }
            }

            if (!resp.IsSuccessStatusCode) return (null, MotivoRechazo.Ninguno);

            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data is null) return (null, MotivoRechazo.Ninguno);

            await _tokens.GuardarTokensAsync(data.AccessToken, data.RefreshToken);
            await _tokens.GuardarUsuarioAsync(data.Usuario);
            return (data, MotivoRechazo.Ninguno);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en LoginAsync.");
            return (null, MotivoRechazo.Ninguno);
        }
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en RegistroAlumnoAsync.");
            return RegistroResultado.ErrorServidor;
        }
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en RegistroInvitadoAsync.");
            return null;
        }
    }

    public async Task<RegistroResultado> RegistroEmpleadoAsync(RegistroEmpleadoRequest req)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/registro/empleado", req);
            if (resp.IsSuccessStatusCode) return RegistroResultado.Ok;
            if (resp.StatusCode == HttpStatusCode.Conflict) return RegistroResultado.EmailDuplicado;
            return RegistroResultado.ErrorServidor;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en RegistroEmpleadoAsync.");
            return RegistroResultado.ErrorServidor;
        }
    }

    public async Task<bool> CambiarPasswordAsync(CambiarPasswordRequest req)
    {
        try
        {
            var resp = await EnviarConRefreshAsync(HttpMethod.Post, "api/auth/cambiar-password",
                JsonContent.Create(req));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error en CambiarPasswordAsync.");
            return false;
        }
    }
}
