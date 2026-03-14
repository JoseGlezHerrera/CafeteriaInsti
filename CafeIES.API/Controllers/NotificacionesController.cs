using CafeIES.API.Data;
using CafeIES.API.Extensions;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/notificaciones")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly AppDbContext _db;

    public NotificacionesController(AppDbContext db) => _db = db;

    // ── POST /api/notificaciones/token ───────────────────────────────────────
    /// <summary>
    /// Registra o actualiza el token FCM del dispositivo del usuario autenticado.
    /// Hace upsert: si el token ya existe (p.ej. reinstalación) lo reasigna al usuario actual.
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> RegistrarToken([FromBody] RegistrarTokenRequest req)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var existing = await _db.DispositivoTokens
            .FirstOrDefaultAsync(t => t.Token == req.Token);

        if (existing is null)
        {
            _db.DispositivoTokens.Add(new DispositivoToken
            {
                UsuarioId          = userId.Value,
                Token              = req.Token,
                Plataforma         = req.Plataforma,
                FechaActualizacion = DateTime.UtcNow
            });
        }
        else
        {
            // Reasignar en caso de cambio de usuario en el mismo dispositivo
            existing.UsuarioId          = userId.Value;
            existing.Plataforma         = req.Plataforma;
            existing.FechaActualizacion = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── DELETE /api/notificaciones/token ─────────────────────────────────────
    /// <summary>
    /// Elimina el token FCM al cerrar sesión para no recibir notificaciones ajenas.
    /// El token se pasa en el cuerpo para evitar exponerlo en la URL (logs, proxies).
    /// </summary>
    [HttpDelete("token")]
    public async Task<IActionResult> EliminarToken([FromBody] EliminarTokenRequest req)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var dispositivo = await _db.DispositivoTokens
            .FirstOrDefaultAsync(t => t.Token == req.Token && t.UsuarioId == userId.Value);

        if (dispositivo is not null)
        {
            _db.DispositivoTokens.Remove(dispositivo);
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}
