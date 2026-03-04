using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly AppDbContext _db;
    public CategoriasController(AppDbContext db) => _db = db;

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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoriaDto>> Crear([FromBody] CategoriaDto req)
    {
        var cat = new Categoria { Nombre = req.Nombre, Emoji = req.Emoji };
        _db.Categorias.Add(cat);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new CategoriaDto(cat.Id, cat.Nombre, cat.Emoji));
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CategoriaDto>> Actualizar(int id, [FromBody] CategoriaDto req)
    {
        var cat = await _db.Categorias.FindAsync(id);
        if (cat is null) return NotFound();
        cat.Nombre = req.Nombre;
        cat.Emoji  = req.Emoji;
        await _db.SaveChangesAsync();
        return Ok(new CategoriaDto(cat.Id, cat.Nombre, cat.Emoji));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Eliminar(int id)
    {
        var cat = await _db.Categorias.FindAsync(id);
        if (cat is null) return NotFound();
        cat.Activa = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
