using CafeIES.API.Data;
using CafeIES.API.Services;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("general")]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ReportesController(AppDbContext db)
    {
        _db = db;
    }

    // ── GET /api/reportes/excel ───────────────────────────────────────────────
    [HttpGet("excel")]
    public async Task<IActionResult> Excel(
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        if (desde.HasValue && hasta.HasValue && desde > hasta)
            return BadRequest(new { mensaje = "La fecha 'desde' no puede ser posterior a 'hasta'." });

        var pedidos = await CargarPedidosAsync(desde, hasta);
        var bytes   = ReporteExcelService.Generar(pedidos, desde, hasta);

        var nombre = $"reporte-cafeies-{DateTime.UtcNow:yyyyMMdd}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            nombre);
    }

    // ── GET /api/reportes/pdf ─────────────────────────────────────────────────
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        if (desde.HasValue && hasta.HasValue && desde > hasta)
            return BadRequest(new { mensaje = "La fecha 'desde' no puede ser posterior a 'hasta'." });

        var pedidos = await CargarPedidosAsync(desde, hasta);
        var bytes   = ReportePdfService.Generar(pedidos, desde, hasta);

        var nombre = $"reporte-cafeies-{DateTime.UtcNow:yyyyMMdd}.pdf";
        return File(bytes, "application/pdf", nombre);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private async Task<List<Pedido>> CargarPedidosAsync(DateTime? desde, DateTime? hasta)
    {
        var query = _db.Pedidos
            .Include(p => p.Usuario).ThenInclude(u => u!.Instituto)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .AsQueryable();

        if (desde.HasValue)
            query = query.Where(p => p.FechaCreacion.Date >= desde.Value.Date);
        if (hasta.HasValue)
            query = query.Where(p => p.FechaCreacion.Date <= hasta.Value.Date);

        return await query.OrderBy(p => p.FechaCreacion).ToListAsync();
    }
}
