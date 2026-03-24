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
[Route("api/reportes")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("general")]
public class ReportesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReportesController> _logger;

    public ReportesController(AppDbContext db, ILogger<ReportesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// Admin con instituto asignado en JWT solo puede ver su propio instituto.
    /// Admin global (sin institutoId en JWT) puede ver todos.
    private int? GetAdminInstitutoId() =>
        int.TryParse(User.FindFirst("institutoId")?.Value, out var id) && id > 0 ? id : null;

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
        var institutoId = GetAdminInstitutoId();

        var query = _db.Pedidos
            .Include(p => p.Usuario).ThenInclude(u => u!.Instituto)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .AsQueryable();

        // Scoping por instituto: admins de instituto solo ven sus pedidos
        if (institutoId.HasValue)
            query = query.Where(p => p.Usuario!.InstitutoId == institutoId);

        if (desde.HasValue)
            query = query.Where(p => p.FechaCreacion.Date >= desde.Value.Date);
        if (hasta.HasValue)
            query = query.Where(p => p.FechaCreacion.Date <= hasta.Value.Date);

        // Contar antes de aplicar el límite para poder advertir
        var total = await query.CountAsync();
        const int MaxPedidos = 1000;
        if (total > MaxPedidos)
            _logger.LogWarning("Reporte solicitado con {Total} pedidos; se truncará a {Max} en la query.", total, MaxPedidos);

        return await query.OrderBy(p => p.FechaCreacion).Take(MaxPedidos).ToListAsync();
    }
}
