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
            .Include(p => p.ProductoIngredientes).ThenInclude(pi => pi.Ingrediente)
            .AsQueryable();

        if (soloActivos == true)
            query = query.Where(p => p.Activo);

        if (categoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == categoriaId.Value);

        var productos = await query
            .OrderBy(p => p.Categoria.Orden)
            .ThenBy(p => p.Nombre)
            .ThenBy(p => p.Id)   // PERF-012: orden estable cuando dos productos comparten nombre
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
            .Include(p => p.ProductoIngredientes).ThenInclude(pi => pi.Ingrediente)
            .FirstOrDefaultAsync(p => p.Id == id);
        return p is null ? NotFound() : Ok(p.ToDto());
    }

    // ── POST /api/productos  (Admin / Empleado) ──────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin,Empleado")]
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

        // Vincular ingredientes personalizables
        if (req.Ingredientes is { Count: > 0 })
        {
            var ingredienteIds = req.Ingredientes.Select(i => i.IngredienteId).ToHashSet();
            var ingredientesExistentes = await _db.Ingredientes
                .Where(i => ingredienteIds.Contains(i.Id) && i.Activo)
                .Select(i => i.Id)
                .ToHashSetAsync();

            foreach (var ri in req.Ingredientes)
            {
                if (!ingredientesExistentes.Contains(ri.IngredienteId)) continue;
                producto.ProductoIngredientes.Add(new ProductoIngrediente
                {
                    IngredienteId  = ri.IngredienteId,
                    EsBase         = ri.EsBase,
                    EsQuitable     = ri.EsQuitable,
                    Orden          = ri.Orden,
                    CantidadMaxima = Math.Max(1, ri.CantidadMaxima)
                });
            }
        }

        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();

        // Recargar con todas las relaciones para el DTO
        var created = await _db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Alergenos)
            .Include(p => p.ProductoIngredientes).ThenInclude(pi => pi.Ingrediente)
            .FirstAsync(p => p.Id == producto.Id);
        return CreatedAtAction(nameof(GetById), new { id = producto.Id }, created.ToDto());
    }

    // ── PUT /api/productos/{id}  (Admin) ──────────────────────────────────────
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, [FromBody] CrearProductoRequest req)
    {
        var producto = await _db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Alergenos)
            .Include(p => p.ProductoIngredientes)
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

        // Reemplazar ingredientes personalizables
        _db.ProductoIngredientes.RemoveRange(producto.ProductoIngredientes);
        if (req.Ingredientes is { Count: > 0 })
        {
            var ingredienteIds = req.Ingredientes.Select(i => i.IngredienteId).ToHashSet();
            var ingredientesExistentes = await _db.Ingredientes
                .Where(i => ingredienteIds.Contains(i.Id) && i.Activo)
                .Select(i => i.Id)
                .ToHashSetAsync();

            foreach (var ri in req.Ingredientes)
            {
                if (!ingredientesExistentes.Contains(ri.IngredienteId)) continue;
                producto.ProductoIngredientes.Add(new ProductoIngrediente
                {
                    IngredienteId  = ri.IngredienteId,
                    EsBase         = ri.EsBase,
                    EsQuitable     = ri.EsQuitable,
                    Orden          = ri.Orden,
                    CantidadMaxima = Math.Max(1, ri.CantidadMaxima)
                });
            }
        }

        await _db.SaveChangesAsync();

        // Recargar con ingredientes para el DTO
        await _db.Entry(producto).Collection(p => p.ProductoIngredientes)
            .Query().Include(pi => pi.Ingrediente).LoadAsync();
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

        // SEC-018: un único stream para validar magic bytes y subir el archivo.
        // Leer los 12 bytes de cabecera y luego hacer Seek(0) para reutilizar el stream
        // desde el principio en la subida — evita abrir un segundo stream que podría estar
        // ya al final del fichero según el host/buffer subyacente.
        using var stream = imagen.OpenReadStream();
        var header = new byte[12];
        var bytesLeidos = await stream.ReadAsync(header.AsMemory(0, 12));
        bool magicOk = ext is ".jpg" or ".jpeg"
            ? bytesLeidos >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF
            : ext == ".png"
            ? bytesLeidos >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            : ext == ".webp"
            ? bytesLeidos >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                                && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P'
            : false;
        if (!magicOk)
            return BadRequest(new { mensaje = "El contenido del archivo no coincide con el formato declarado." });

        stream.Seek(0, SeekOrigin.Begin); // reposicionar al inicio para la subida

        // Eliminar imagen anterior (local o Blob)
        await _blobs.EliminarAsync(producto.ImagenUrl);

        var fileName = $"{id}_{Guid.NewGuid():N}{ext}";
        var url = await _blobs.SubirAsync(stream, fileName, imagen.ContentType);

        producto.ImagenUrl = url;
        await _db.SaveChangesAsync();

        return Ok(new { imagenUrl = url });
    }

}
