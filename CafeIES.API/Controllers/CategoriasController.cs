using System.Security.Claims;
using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("general")]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<CategoriasController> _logger;

    public CategoriasController(AppDbContext db, ILogger<CategoriasController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoriaDto>>> GetAll()
    {
        var cats = await _db.Categorias
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden)
            .ToListAsync();

        return Ok(cats.Select(c => new CategoriaDto(c.Id, c.Nombre, c.Emoji)).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Empleado")]
    public async Task<ActionResult<CategoriaDto>> Crear([FromBody] CategoriaDto req)
    {
        var cat = new Categoria { Nombre = req.Nombre, Emoji = req.Emoji };
        _db.Categorias.Add(cat);
        await _db.SaveChangesAsync();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} creó la categoría {Id} ({Nombre})",
            adminEmail, cat.Id, cat.Nombre);

        return CreatedAtAction(nameof(GetAll), new CategoriaDto(cat.Id, cat.Nombre, cat.Emoji));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Empleado")]
    public async Task<ActionResult<CategoriaDto>> Actualizar(int id, [FromBody] CategoriaDto req)
    {
        var cat = await _db.Categorias.FindAsync(id);
        if (cat is null) return NotFound();
        cat.Nombre = req.Nombre;
        cat.Emoji  = req.Emoji;
        await _db.SaveChangesAsync();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} actualizó la categoría {Id} ({Nombre})",
            adminEmail, id, cat.Nombre);

        return Ok(new CategoriaDto(cat.Id, cat.Nombre, cat.Emoji));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Empleado")]
    public async Task<ActionResult> Eliminar(int id)
    {
        var cat = await _db.Categorias.FindAsync(id);
        if (cat is null) return NotFound();
        cat.Activa = false;
        await _db.SaveChangesAsync();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} desactivó la categoría {Id} ({Nombre})",
            adminEmail, id, cat.Nombre);

        return NoContent();
    }
}
