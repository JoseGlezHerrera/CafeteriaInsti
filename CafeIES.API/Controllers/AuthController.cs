using CafeIES.API.Data;
using CafeIES.Shared.Models;
using CafeIES.API.Services;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower());

        if (usuario is null || !_auth.VerificarPassword(req.Password, usuario.PasswordHash))
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });

        if (usuario.Estado == EstadoCuenta.PendienteValidacion)
            return Forbid(); // 403 → la app mostrará "pendiente de validación"

        if (usuario.Estado == EstadoCuenta.Suspendida || usuario.Estado == EstadoCuenta.Rechazada)
            return Forbid();

        var accessToken  = _auth.GenerarAccessToken(usuario);
        var refreshToken = _auth.GenerarRefreshToken();

        usuario.RefreshToken       = refreshToken;
        usuario.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(
            accessToken,
            refreshToken,
            MapUsuarioDto(usuario)));
    }

    // ── POST /api/auth/registro/alumno ───────────────────────────────────────
    [HttpPost("registro/alumno")]
    public async Task<ActionResult> RegistroAlumno([FromBody] RegistroAlumnoRequest req)
    {
        if (await _db.Usuarios.AnyAsync(u => u.Email == req.Email.ToLower()))
            return Conflict(new { mensaje = "Ya existe una cuenta con ese email." });

        var usuario = new Usuario
        {
            NombreCompleto = req.NombreCompleto,
            Email          = req.Email.ToLower(),
            PasswordHash   = _auth.HashPassword(req.Password),
            Rol            = RolUsuario.Alumno,
            Turno          = req.Turno,
            Estado         = EstadoCuenta.PendienteValidacion
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = "Registro completado. Tu cuenta está pendiente de validación por el administrador." });
    }

    // ── POST /api/auth/registro/invitacion ───────────────────────────────────
    [HttpPost("registro/invitacion")]
    public async Task<ActionResult<LoginResponse>> RegistroInvitado([FromBody] RegistroInvitadoRequest req)
    {
        // Validar token de invitación
        var invitacion = await _db.Invitaciones
            .FirstOrDefaultAsync(i => i.Token == req.TokenInvitacion);

        if (invitacion is null || !invitacion.EsValida)
            return BadRequest(new { mensaje = "El enlace de invitación no es válido o ha expirado." });

        if (await _db.Usuarios.AnyAsync(u => u.Email == req.Email.ToLower()))
            return Conflict(new { mensaje = "Ya existe una cuenta con ese email." });

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
            FechaValidacion = DateTime.UtcNow
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
        usuario.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(accessToken, refreshToken, MapUsuarioDto(usuario)));
    }

    // ── POST /api/auth/refresh ───────────────────────────────────────────────
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest req)
    {
        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.RefreshToken == req.RefreshToken
                                   && u.RefreshTokenExpiry > DateTime.UtcNow);

        if (usuario is null)
            return Unauthorized(new { mensaje = "Refresh token inválido o expirado." });

        var accessToken  = _auth.GenerarAccessToken(usuario);
        var refreshToken = _auth.GenerarRefreshToken();
        usuario.RefreshToken       = refreshToken;
        usuario.RefreshTokenExpiry = DateTime.UtcNow.AddDays(30);
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse(accessToken, refreshToken, MapUsuarioDto(usuario)));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static UsuarioDto MapUsuarioDto(Usuario u) => new(
        u.Id, u.NombreCompleto, u.Email, u.Rol, u.Turno, u.Estado);
}
