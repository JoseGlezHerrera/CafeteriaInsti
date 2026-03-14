using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductosController(AppDbContext db) => _db = db;

    // ── GET /api/productos  (público para usuarios autenticados) ─────────────
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<ProductoDto>>> GetAll(
        [FromQuery] int? categoriaId,
        [FromQuery] bool? soloActivos = true)
    {
        var query = _db.Productos
            .Include(p => p.Categoria)
            .AsQueryable();

        if (soloActivos == true)
            query = query.Where(p => p.Activo);

        if (categoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == categoriaId.Value);

        var productos = await query
            .OrderBy(p => p.Categoria.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync();

        return Ok(productos.Select(MapDto).ToList());
    }

    // ── GET /api/productos/{id} ───────────────────────────────────────────────
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<ProductoDto>> GetById(int id)
    {
        var p = await _db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);
        return p is null ? NotFound() : Ok(MapDto(p));
    }

    // ── POST /api/productos  (solo Admin) ────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductoDto>> Crear([FromBody] CrearProductoRequest req)
    {
        if (!await _db.Categorias.AnyAsync(c => c.Id == req.CategoriaId))
            return BadRequest(new { mensaje = "Categoría no válida." });

        var producto = new Producto
        {
            Nombre      = req.Nombre,
            Descripcion = req.Descripcion,
            Precio      = req.Precio,
            Stock       = req.Stock,
            CategoriaId = req.CategoriaId,
            ImagenUrl   = req.ImagenUrl,
            Activo      = true
        };

        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();

        await _db.Entry(producto).Reference(p => p.Categoria).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = producto.Id }, MapDto(producto));
    }

    // ── PUT /api/productos/{id}  (solo Admin) ────────────────────────────────
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, [FromBody] CrearProductoRequest req)
    {
        var producto = await _db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);
        if (producto is null) return NotFound();

        producto.Nombre      = req.Nombre;
        producto.Descripcion = req.Descripcion;
        producto.Precio      = req.Precio;
        producto.Stock       = req.Stock;
        producto.CategoriaId = req.CategoriaId;
        producto.ImagenUrl   = req.ImagenUrl;

        await _db.SaveChangesAsync();
        await _db.Entry(producto).Reference(p => p.Categoria).LoadAsync();
        return Ok(MapDto(producto));
    }

    // ── PATCH /api/productos/{id}/stock  (Admin) ─────────────────────────────
    [HttpPatch("{id}/stock")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ActualizarStock(int id, [FromBody] ActualizarStockRequest req)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        producto.Stock = req.NuevoStock;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── PATCH /api/productos/{id}/toggle  (Admin) ────────────────────────────
    [HttpPatch("{id}/toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ToggleActivo(int id)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        producto.Activo = !producto.Activo;
        await _db.SaveChangesAsync();
        return Ok(new { producto.Id, producto.Activo });
    }

    // ── DELETE /api/productos/{id}  (Admin) ──────────────────────────────────
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Eliminar(int id)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        // Soft delete: desactivar en vez de borrar (los pedidos históricos lo referencian)
        producto.Activo = false;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/productos/{id}/imagen  (Admin) ──────────────────────────────
    [HttpPost("{id}/imagen")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> SubirImagen(int id, IFormFile imagen)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        if (imagen.Length > 5 * 1024 * 1024)
            return BadRequest(new { mensaje = "La imagen no puede superar 5 MB." });

        var ext = Path.GetExtension(imagen.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { mensaje = "Formato no soportado. Usa JPG, PNG o WebP." });

        // Eliminar imagen anterior si era local
        if (!string.IsNullOrEmpty(producto.ImagenUrl) && producto.ImagenUrl.StartsWith("/uploads/"))
        {
            var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                producto.ImagenUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldPath))
                System.IO.File.Delete(oldPath);
        }

        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "productos");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{id}_{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = System.IO.File.Create(filePath))
            await imagen.CopyToAsync(stream);

        producto.ImagenUrl = $"/uploads/productos/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { imagenUrl = producto.ImagenUrl });
    }

    private static ProductoDto MapDto(Producto p) => new(
        p.Id, p.Nombre, p.Descripcion, p.Precio, p.Stock,
        p.ImagenUrl, p.Activo, p.NivelStock,
        p.CategoriaId, p.Categoria.Nombre, p.Categoria.Emoji);
}
