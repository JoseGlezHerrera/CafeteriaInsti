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
[EnableRateLimiting("general")]
public class ProductosController : ControllerBase
{
    private readonly AppDbContext        _db;
    private readonly IBlobStorageService _blobs;

    public ProductosController(AppDbContext db, IBlobStorageService blobs)
    {
        _db    = db;
        _blobs = blobs;
    }

    // ── GET /api/productos  (público para usuarios autenticados) ─────────────
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<ProductoDto>>> GetAll(
        [FromQuery] int? categoriaId,
        [FromQuery] bool? soloActivos = true)
    {
        var query = _db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Alergenos)
            .AsQueryable();

        if (soloActivos == true)
            query = query.Where(p => p.Activo);

        if (categoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == categoriaId.Value);

        var productos = await query
            .OrderBy(p => p.Categoria.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync();

        return Ok(productos.Select(p => p.ToDto()).ToList());
    }

    // ── GET /api/productos/{id} ───────────────────────────────────────────────
    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<ProductoDto>> GetById(int id)
    {
        var p = await _db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Alergenos)
            .FirstOrDefaultAsync(p => p.Id == id);
        return p is null ? NotFound() : Ok(p.ToDto());
    }

    // ── POST /api/productos  (solo Admin) ────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductoDto>> Crear([FromBody] CrearProductoRequest req)
    {
        if (!await _db.Categorias.AnyAsync(c => c.Id == req.CategoriaId))
            return BadRequest(new { mensaje = "Categoría no válida." });

        if (!string.IsNullOrEmpty(req.ImagenUrl) &&
            (!Uri.TryCreate(req.ImagenUrl, UriKind.Absolute, out var uriResult) ||
             (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)))
            return BadRequest(new { mensaje = "La URL de imagen debe ser una URL HTTP/HTTPS válida." });

        var producto = new Producto
        {
            Nombre             = req.Nombre,
            Descripcion        = req.Descripcion,
            Precio             = req.Precio,
            Stock              = req.Stock,
            CategoriaId        = req.CategoriaId,
            ImagenUrl          = req.ImagenUrl,
            Activo             = true,
            ComponenteDesayuno = req.ComponenteDesayuno
        };

        // Vincular alérgenos
        if (req.AlergenoIds is { Count: > 0 })
        {
            var alergenos = await _db.Alergenos
                .Where(a => req.AlergenoIds.Contains(a.Id))
                .ToListAsync();
            foreach (var a in alergenos) producto.Alergenos.Add(a);
        }

        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();

        await _db.Entry(producto).Reference(p => p.Categoria).LoadAsync();
        await _db.Entry(producto).Collection(p => p.Alergenos).LoadAsync();
        return CreatedAtAction(nameof(GetById), new { id = producto.Id }, producto.ToDto());
    }

    // ── PUT /api/productos/{id}  (Admin) ──────────────────────────────────────
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, [FromBody] CrearProductoRequest req)
    {
        var producto = await _db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Alergenos)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (producto is null) return NotFound();

        if (!await _db.Categorias.AnyAsync(c => c.Id == req.CategoriaId))
            return BadRequest(new { mensaje = "Categoría no válida." });

        if (!string.IsNullOrEmpty(req.ImagenUrl) &&
            (!Uri.TryCreate(req.ImagenUrl, UriKind.Absolute, out var uriResult2) ||
             (uriResult2.Scheme != Uri.UriSchemeHttp && uriResult2.Scheme != Uri.UriSchemeHttps)))
            return BadRequest(new { mensaje = "La URL de imagen debe ser una URL HTTP/HTTPS válida." });

        producto.Nombre             = req.Nombre;
        producto.Descripcion        = req.Descripcion;
        producto.Precio             = req.Precio;
        producto.Stock              = req.Stock;
        producto.CategoriaId        = req.CategoriaId;
        producto.ImagenUrl          = req.ImagenUrl;
        producto.ComponenteDesayuno = req.ComponenteDesayuno;

        // Reemplazar alérgenos
        producto.Alergenos.Clear();
        if (req.AlergenoIds is { Count: > 0 })
        {
            var alergenos = await _db.Alergenos
                .Where(a => req.AlergenoIds.Contains(a.Id))
                .ToListAsync();
            foreach (var a in alergenos) producto.Alergenos.Add(a);
        }

        await _db.SaveChangesAsync();
        await _db.Entry(producto).Reference(p => p.Categoria).LoadAsync();
        return Ok(producto.ToDto());
    }

    // ── PATCH /api/productos/{id}/stock  (Admin / Empleado) ──────────────────
    [HttpPatch("{id}/stock")]
    [Authorize(Roles = "Admin,Empleado")]
    public async Task<ActionResult> ActualizarStock(int id, [FromBody] ActualizarStockRequest req)
    {
        if (req.NuevoStock < -1)
            return BadRequest(new { mensaje = "El stock no puede ser menor que -1 (ilimitado)." });

        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        producto.Stock = req.NuevoStock;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── PATCH /api/productos/{id}/toggle  (Admin / Empleado) ─────────────────
    [HttpPatch("{id}/toggle")]
    [Authorize(Roles = "Admin,Empleado")]
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

        // Eliminar imagen del blob si existe
        await _blobs.EliminarAsync(producto.ImagenUrl);

        // Las LineaPedido que referencian este producto pasarán a ProductoId = null (DeleteBehavior.SetNull)
        _db.Productos.Remove(producto);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── POST /api/productos/{id}/imagen  (Admin) ──────────────────────────────
    [HttpPost("{id}/imagen")]
    [Authorize(Roles = "Admin")]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]  // 5 MB
    [RequestSizeLimit(5_242_880)]
    public async Task<ActionResult> SubirImagen(int id, IFormFile imagen)
    {
        var producto = await _db.Productos.FindAsync(id);
        if (producto is null) return NotFound();

        if (imagen.Length > 5 * 1024 * 1024)
            return BadRequest(new { mensaje = "La imagen no puede superar 5 MB." });

        var ext = Path.GetExtension(imagen.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return BadRequest(new { mensaje = "Formato no soportado. Usa JPG, PNG o WebP." });

        // Eliminar imagen anterior (local o Blob)
        await _blobs.EliminarAsync(producto.ImagenUrl);

        var fileName = $"{id}_{Guid.NewGuid():N}{ext}";
        using var stream = imagen.OpenReadStream();
        var url = await _blobs.SubirAsync(stream, fileName, imagen.ContentType);

        producto.ImagenUrl = url;
        await _db.SaveChangesAsync();

        return Ok(new { imagenUrl = url });
    }

}
