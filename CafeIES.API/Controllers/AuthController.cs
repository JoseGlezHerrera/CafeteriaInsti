using CafeIES.API.Data;
using CafeIES.API.Extensions;
using CafeIES.API.Services;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public AuthController(AppDbContext db, AuthService auth)
    {
        _db   = db;
        _auth = auth;
    }

    // ── POST /api/auth/login ─────────────────────────────────────────────────
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Instituto)
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());

        if (usuario is null || !_auth.VerificarPassword(req.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });

        if (usuario.Estado != EstadoCuenta.Activa)
        {
            var motivo = usuario.Estado switch
            {
                EstadoCuenta.PendienteValidacion => "pendiente",
                EstadoCuenta.Suspendida          => "suspendida",
                EstadoCuenta.Rechazada           => "rechazada",
                _                                => "inactiva"
            };
            return StatusCode(403, new { motivo });
        }

        var accessToken  = _auth.GenerarAccessToken(usuario);
        var refreshToken = _auth.GenerarRefreshToken();

        usuario.RefreshToken       = refreshToken;
        usuario.RefreshTokenExpiry = DateTime.Now.AddDays(30);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(
            accessToken,
            refreshToken,
            usuario.ToDto()));
    }

    // ── POST /api/auth/registro/alumno ───────────────────────────────────────
    [HttpPost("registro/alumno")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult> RegistroAlumno([FromBody] RegistroAlumnoRequest req)
    {
        if (!req.Email.Contains('@'))
            return BadRequest(new { mensaje = "El email no tiene un formato válido. Ejemplo: nombre@ies.edu" });

        if (await _db.Usuarios.AnyAsync(u => u.Email == req.Email.ToLower()))
            return Conflict(new { mensaje = "Ya existe una cuenta con ese email." });

        var instituto = await _db.Institutos.FindAsync(req.InstitutoId);
        if (instituto is null || !instituto.Activo)
            return BadRequest(new { mensaje = "El instituto seleccionado no es válido." });

        var usuario = new Usuario
        {
            NombreCompleto = req.NombreCompleto,
            Email          = req.Email.ToLower(),
            PasswordHash   = _auth.HashPassword(req.Password),
            Rol            = RolUsuario.Alumno,
            Turno          = req.Turno,
            Estado         = EstadoCuenta.PendienteValidacion,
            InstitutoId    = req.InstitutoId
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Registro completado. Tu cuenta está pendiente de validación por el administrador." });
    }

    // ── POST /api/auth/registro/invitacion ───────────────────────────────────
    [HttpPost("registro/invitacion")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> RegistroInvitado([FromBody] RegistroInvitadoRequest req)
    {
        // Validar token de invitación
        var invitacion = await _db.Invitaciones
            .FirstOrDefaultAsync(i => i.Token == req.TokenInvitacion);

        if (invitacion is null || !invitacion.EsValida)
            return BadRequest(new { mensaje = "El enlace de invitación no es válido o ha expirado." });

        if (await _db.Usuarios.AnyAsync(u => u.Email == req.Email.ToLower()))
            return Conflict(new { mensaje = "Ya existe una cuenta con ese email." });

        var instituto = await _db.Institutos.FindAsync(req.InstitutoId);
        if (instituto is null || !instituto.Activo)
            return BadRequest(new { mensaje = "El instituto seleccionado no es válido." });

        // Asignar rol según tipo de invitación
        var rol = invitacion.Tipo == TipoInvitacion.Profesor
            ? RolUsuario.Profesor
            : RolUsuario.Personal;

        var usuario = new Usuario
        {
            NombreCompleto  = req.NombreCompleto,
            Email           = req.Email.ToLower(),
            PasswordHash    = _auth.HashPassword(req.Password),
            Rol             = rol,
            Turno           = null,  // Sin restricción horaria
            Estado          = EstadoCuenta.Activa,
            FechaValidacion = DateTime.Now,
            InstitutoId     = req.InstitutoId,
            Instituto       = instituto
        };

        _db.Usuarios.Add(usuario);

        // Incrementar usos de la invitación
        invitacion.UsosActuales++;
        if (invitacion.UsosMaximos.HasValue && invitacion.UsosActuales >= invitacion.UsosMaximos)
            invitacion.Activa = false;

        await _db.SaveChangesAsync();

        // Login automático
        var accessToken  = _auth.GenerarAccessToken(usuario);
        var refreshToken = _auth.GenerarRefreshToken();
        usuario.RefreshToken       = refreshToken;
        usuario.RefreshTokenExpiry = DateTime.Now.AddDays(30);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(accessToken, refreshToken, usuario.ToDto()));
    }

    // ── POST /api/auth/refresh ───────────────────────────────────────────────
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Instituto)
            .FirstOrDefaultAsync(u => u.RefreshToken == req.RefreshToken
                                   && u.RefreshTokenExpiry > DateTime.Now);

        if (usuario is null)
            return Unauthorized(new { mensaje = "Refresh token inválido o expirado." });

        var accessToken  = _auth.GenerarAccessToken(usuario);
        var refreshToken = _auth.GenerarRefreshToken();
        usuario.RefreshToken       = refreshToken;
        usuario.RefreshTokenExpiry = DateTime.Now.AddDays(30);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(accessToken, refreshToken, usuario.ToDto()));
    }

    // ── POST /api/auth/cambiar-password ──────────────────────────────────────
    [HttpPost("cambiar-password")]
    [Authorize]
    public async Task<ActionResult> CambiarPassword([FromBody] CambiarPasswordRequest req)
    {
        var userId = User.GetUserId();
        if (userId is null) return Unauthorized();

        var usuario = await _db.Usuarios.FindAsync(userId.Value);
        if (usuario is null) return NotFound();

        if (!_auth.VerificarPassword(req.PasswordActual, usuario.PasswordHash))
            return BadRequest(new { mensaje = "La contraseña actual no es correcta." });

        usuario.PasswordHash = _auth.HashPassword(req.NuevaPassword);
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Contraseña actualizada correctamente." });
    }
}
