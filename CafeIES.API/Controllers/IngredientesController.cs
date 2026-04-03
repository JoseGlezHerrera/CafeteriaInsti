using CafeIES.API.Data;
using CafeIES.API.Extensions;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("general")]
public class IngredientesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<IngredientesController> _logger;

    public IngredientesController(AppDbContext db, ILogger<IngredientesController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── GET /api/ingredientes  (Admin / Empleado) ─────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin,Empleado")]
    public async Task<ActionResult<List<IngredienteDto>>> GetAll([FromQuery] bool? soloActivos = null)
    {
        var query = _db.Ingredientes.AsQueryable();

        if (soloActivos == true)
            query = query.Where(i => i.Activo);

        var ingredientes = await query
            .OrderBy(i => i.Nombre)
            .ToListAsync();

        return Ok(ingredientes.Select(i => i.ToDto()).ToList());
    }

    // ── GET /api/ingredientes/{id}  (Admin / Empleado) ────────────────────────
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Empleado")]
    public async Task<ActionResult<IngredienteDto>> GetById(int id)
    {
        var i = await _db.Ingredientes.FindAsync(id);
        return i is null ? NotFound() : Ok(i.ToDto());
    }

    // ── POST /api/ingredientes  (Admin) ───────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IngredienteDto>> Crear([FromBody] CrearIngredienteRequest req)
    {
        var ingrediente = new Ingrediente
        {
            Nombre     = req.Nombre.Trim(),
            Emoji      = req.Emoji?.Trim() ?? string.Empty,
            PrecioExtra = req.PrecioExtra,
            Stock      = req.Stock,
            Activo     = true
        };

        _db.Ingredientes.Add(ingrediente);
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] Admin {UserId} creó ingrediente '{Nombre}' (ID:{Id}).",
            User.GetUserId(), ingrediente.Nombre, ingrediente.Id);

        return CreatedAtAction(nameof(GetById), new { id = ingrediente.Id }, ingrediente.ToDto());
    }

    // ── PUT /api/ingredientes/{id}  (Admin) ───────────────────────────────────
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IngredienteDto>> Actualizar(int id, [FromBody] CrearIngredienteRequest req)
    {
        var ingrediente = await _db.Ingredientes.FindAsync(id);
        if (ingrediente is null) return NotFound();

        ingrediente.Nombre      = req.Nombre.Trim();
        ingrediente.Emoji       = req.Emoji?.Trim() ?? string.Empty;
        ingrediente.PrecioExtra = req.PrecioExtra;
        ingrediente.Stock       = req.Stock;

        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] Admin {UserId} actualizó ingrediente '{Nombre}' (ID:{Id}).",
            User.GetUserId(), ingrediente.Nombre, ingrediente.Id);

        return Ok(ingrediente.ToDto());
    }

    // ── PATCH /api/ingredientes/{id}/stock  (Admin / Empleado) ───────────────
    [HttpPatch("{id}/stock")]
    [Authorize(Roles = "Admin,Empleado")]
    public async Task<ActionResult> ActualizarStock(int id, [FromBody] ActualizarStockRequest req)
    {
        if (req.NuevoStock < -1)
            return BadRequest(new { mensaje = "El stock no puede ser menor que -1 (ilimitado)." });

        var ingrediente = await _db.Ingredientes.FindAsync(id);
        if (ingrediente is null) return NotFound();

        ingrediente.Stock = req.NuevoStock;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── PATCH /api/ingredientes/{id}/toggle  (Admin) ─────────────────────────
    [HttpPatch("{id}/toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ToggleActivo(int id)
    {
        var ingrediente = await _db.Ingredientes.FindAsync(id);
        if (ingrediente is null) return NotFound();

        ingrediente.Activo = !ingrediente.Activo;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] Admin {UserId} {Accion} ingrediente '{Nombre}' (ID:{Id}).",
            User.GetUserId(), ingrediente.Activo ? "activó" : "desactivó", ingrediente.Nombre, ingrediente.Id);

        return Ok(new { ingrediente.Id, ingrediente.Activo });
    }

    // ── DELETE /api/ingredientes/{id}  (Admin) ────────────────────────────────
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Eliminar(int id)
    {
        var ingrediente = await _db.Ingredientes.FindAsync(id);
        if (ingrediente is null) return NotFound();

        // EF lanzará DbUpdateException si hay ProductoIngrediente con este ingrediente
        // (OnDelete.Restrict). Lo capturamos para devolver un 409 legible.
        try
        {
            _db.Ingredientes.Remove(ingrediente);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "No se puede eliminar el ingrediente porque está asignado a uno o más productos. Desasígnalo primero." });
        }

        _logger.LogInformation("[AUDIT] Admin {UserId} eliminó ingrediente '{Nombre}' (ID:{Id}).",
            User.GetUserId(), ingrediente.Nombre, id);

        return NoContent();
    }
}
