using System.Security.Claims;
using CafeIES.API.Data;
using CafeIES.API.Extensions;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/alergenos")]
[Authorize(Roles = "Admin,Empleado")]
[EnableRateLimiting("general")]
public class AlergenosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AlergenosController> _logger;

    public AlergenosController(AppDbContext db, ILogger<AlergenosController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── GET /api/alergenos ────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult<List<AlergenoDto>>> GetAll()
    {
        var alergenos = await _db.Alergenos
            .OrderBy(a => a.Id)
            .ToListAsync();
        return Ok(alergenos.Select(a => a.ToDto()).ToList());
    }

    // ── POST /api/alergenos ───────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<AlergenoDto>> Crear([FromBody] AlergenoDto req)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio." });

        var alergeno = new Alergeno { Nombre = req.Nombre.Trim(), Emoji = req.Emoji };
        _db.Alergenos.Add(alergeno);
        await _db.SaveChangesAsync();

        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "empleado";
        _logger.LogInformation("[AUDIT] {User} creó el alérgeno {Id} ({Nombre})",
            email, alergeno.Id, alergeno.Nombre);

        return CreatedAtAction(nameof(GetAll), alergeno.ToDto());
    }

    // ── DELETE /api/alergenos/{id} ────────────────────────────────────────────
    [HttpDelete("{id}")]
    public async Task<ActionResult> Eliminar(int id)
    {
        var alergeno = await _db.Alergenos.FindAsync(id);
        if (alergeno is null) return NotFound();

        _db.Alergenos.Remove(alergeno);
        await _db.SaveChangesAsync();

        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? "empleado";
        _logger.LogWarning("[AUDIT] {User} eliminó el alérgeno {Id} ({Nombre})",
            email, id, alergeno.Nombre);

        return NoContent();
    }
}
