using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("general")]
public class InvitacionesController : ControllerBase
{
    private readonly AppDbContext    _db;
    private readonly IConfiguration  _config;
    private readonly ILogger<InvitacionesController> _logger;  // SEC-021

    public InvitacionesController(AppDbContext db, IConfiguration config, ILogger<InvitacionesController> logger)
    {
        _db     = db;
        _config = config;
        _logger = logger;
    }

    // ── GET /api/invitaciones ─────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<InvitacionDto>>> GetAll()
    {
        var invitaciones = await _db.Invitaciones
            .OrderByDescending(i => i.FechaCreacion)
            .ToListAsync();

        return Ok(invitaciones.Select(MapDto).ToList());
    }

    // ── POST /api/invitaciones ────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<InvitacionDto>> Crear([FromBody] CrearInvitacionRequest req)
    {
        if (req.DiasValidez < 1 || req.DiasValidez > 365)
            return BadRequest("DiasValidez debe estar entre 1 y 365 días.");

        // Desactivar invitaciones anteriores del mismo tipo
        var anteriores = await _db.Invitaciones
            .Where(i => i.Tipo == req.Tipo && i.Activa)
            .ToListAsync();
        anteriores.ForEach(i => i.Activa = false);

        var invitacion = new Invitacion
        {
            Tipo             = req.Tipo,
            Activa           = true,
            FechaExpiracion  = DateTime.UtcNow.AddDays(req.DiasValidez),
            UsosMaximos      = req.UsosMaximos
        };

        _db.Invitaciones.Add(invitacion);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] Invitación creada: tipo={Tipo} token={Token} expira={Expira} usosMax={UsosMax} por admin={Admin}",
            invitacion.Tipo, invitacion.Token, invitacion.FechaExpiracion, invitacion.UsosMaximos,
            User.Identity?.Name ?? "desconocido");

        return Ok(MapDto(invitacion));
    }

    // ── GET /api/invitaciones/{id}/qr  → devuelve imagen PNG del QR ──────────
    [HttpGet("{id}/qr")]
    public async Task<ActionResult> GetQr(int id)
    {
        var invitacion = await _db.Invitaciones.FindAsync(id);
        if (invitacion is null || !invitacion.EsValida)
            return NotFound(new { mensaje = "Invitación no válida." });

        // FIX-25: El QR muestra el codigo corto de la invitacion directamente.
        // El usuario abre la app y escribe el codigo, en lugar de navegar a una URL inexistente.
        var qrContent = invitacion.Token;

        using var qrGenerator = new QRCodeGenerator();
        var qrData   = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var qrCode   = new PngByteQRCode(qrData);
        var qrBytes  = qrCode.GetGraphic(10);

        return File(qrBytes, "image/png", $"invitacion-{invitacion.Tipo}-{invitacion.Token[..8]}.png");
    }

    // ── DELETE /api/invitaciones/{id}  → eliminar definitivamente ────────────
    [HttpDelete("{id}")]
    public async Task<ActionResult> Revocar(int id)
    {
        var invitacion = await _db.Invitaciones.FindAsync(id);
        if (invitacion is null) return NotFound();

        _logger.LogInformation("[AUDIT] Invitación revocada: id={Id} tipo={Tipo} token={Token} por admin={Admin}",
            invitacion.Id, invitacion.Tipo, invitacion.Token, User.Identity?.Name ?? "desconocido");

        _db.Invitaciones.Remove(invitacion);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/invitaciones/validar/{token}  (público, sin auth) ───────────
    [HttpGet("validar/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting("invitaciones")]
    public async Task<ActionResult> Validar(string token)
    {
        var inv = await _db.Invitaciones.FirstOrDefaultAsync(i => i.Token == token);
        if (inv is null || !inv.EsValida)
            return BadRequest(new { valida = false, mensaje = "Enlace inválido o expirado." });

        return Ok(new
        {
            valida = true,
            tipo   = inv.Tipo.ToString(),
            token  = inv.Token
        });
    }

    private InvitacionDto MapDto(Invitacion i)
    {
        // Construir URL completa usando el host de la request actual
        var urlCompleta = $"{Request.Scheme}://{Request.Host}/registro/invitacion/{i.Token}";
        return new InvitacionDto(
            i.Id, i.Token, i.Tipo, i.Activa,
            i.FechaExpiracion, i.UsosMaximos, i.UsosActuales,
            urlCompleta, i.EsValida);
    }
}
