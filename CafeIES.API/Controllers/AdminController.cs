using System.Security.Claims;
using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    // ── GET /api/admin/dashboard ─────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard()
    {
        var hoy = DateTime.UtcNow.Date;

        var pedidosHoy    = await _db.Pedidos.CountAsync(p => p.FechaCreacion.Date == hoy);
        var ingresosHoy   = await _db.Pedidos
            .Where(p => p.FechaCreacion.Date == hoy && p.Estado != EstadoPedido.Cancelado)
            .SumAsync(p => (decimal?)p.Total) ?? 0;
        var productosActivos   = await _db.Productos.CountAsync(p => p.Activo);
        var productosStockBajo = await _db.Productos
            .CountAsync(p => p.Activo && p.Stock >= 0 && p.Stock <= 5);
        var alumnosPendientes  = await _db.Usuarios
            .CountAsync(u => u.Estado == EstadoCuenta.PendienteValidacion);

        var pedidosEnCurso = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario)
            .Where(p => p.Estado == EstadoPedido.Pendiente || p.Estado == EstadoPedido.EnPreparacion)
            .OrderBy(p => p.FechaCreacion)
            .Take(10)
            .ToListAsync();

        return Ok(new DashboardDto(
            pedidosHoy, ingresosHoy,
            productosActivos, productosStockBajo,
            alumnosPendientes,
            pedidosEnCurso.Select(MapPedidoDto).ToList()
        ));
    }

    // ── GET /api/admin/usuarios ───────────────────────────────────────────────
    [HttpGet("usuarios")]
    public async Task<ActionResult<List<UsuarioDto>>> GetUsuarios(
        [FromQuery] EstadoCuenta? estado,
        [FromQuery] RolUsuario?   rol)
    {
        var query = _db.Usuarios.AsQueryable();
        if (estado.HasValue) query = query.Where(u => u.Estado == estado);
        if (rol.HasValue)    query = query.Where(u => u.Rol    == rol);

        var users = await query.OrderBy(u => u.NombreCompleto).ToListAsync();
        return Ok(users.Select(MapUsuarioDto).ToList());
    }

    // ── PATCH /api/admin/usuarios/{id}/validar ────────────────────────────────
    [HttpPatch("usuarios/{id}/validar")]
    public async Task<ActionResult> ValidarAlumno(int id, [FromQuery] bool aprobar)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        user.Estado          = aprobar ? EstadoCuenta.Activa : EstadoCuenta.Rechazada;
        user.FechaValidacion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { mensaje = aprobar ? "Cuenta aprobada." : "Cuenta rechazada." });
    }

    // ── PATCH /api/admin/usuarios/{id}/suspender ──────────────────────────────
    [HttpPatch("usuarios/{id}/suspender")]
    public async Task<ActionResult> Suspender(int id)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Rol == RolUsuario.Admin) return BadRequest(new { mensaje = "No se puede suspender al admin." });

        user.Estado = EstadoCuenta.Suspendida;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── PATCH /api/admin/usuarios/{id}/reactivar ──────────────────────────────
    [HttpPatch("usuarios/{id}/reactivar")]
    public async Task<ActionResult> Reactivar(int id)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Estado != EstadoCuenta.Suspendida && user.Estado != EstadoCuenta.Rechazada)
            return BadRequest(new { mensaje = "La cuenta no está suspendida ni rechazada." });

        user.Estado = EstadoCuenta.Activa;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── PATCH /api/admin/usuarios/{id}/turno ──────────────────────────────────
    [HttpPatch("usuarios/{id}/turno")]
    public async Task<ActionResult> CambiarTurno(int id, [FromBody] CambiarTurnoRequest req)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();
        user.Turno = req.Turno;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/admin/horarios ───────────────────────────────────────────────
    [HttpGet("horarios")]
    public async Task<ActionResult<List<FranjaHorariaDto>>> GetHorarios()
    {
        var franjas = await _db.FranjasHorarias
            .OrderBy(f => f.Turno).ThenBy(f => f.HoraInicio)
            .ToListAsync();
        return Ok(franjas.Select(MapFranjaDto).ToList());
    }

    // ── POST /api/admin/horarios ──────────────────────────────────────────────
    [HttpPost("horarios")]
    public async Task<ActionResult<FranjaHorariaDto>> CrearFranja([FromBody] UpsertFranjaRequest req)
    {
        var franja = new FranjaHoraria
        {
            Turno       = req.Turno,
            Descripcion = req.Descripcion,
            HoraInicio  = req.HoraInicio,
            HoraFin     = req.HoraFin,
            Activa      = req.Activa
        };
        _db.FranjasHorarias.Add(franja);
        await _db.SaveChangesAsync();
        return Ok(MapFranjaDto(franja));
    }

    // ── PUT /api/admin/horarios/{id} ──────────────────────────────────────────
    [HttpPut("horarios/{id}")]
    public async Task<ActionResult<FranjaHorariaDto>> ActualizarFranja(int id, [FromBody] UpsertFranjaRequest req)
    {
        var franja = await _db.FranjasHorarias.FindAsync(id);
        if (franja is null) return NotFound();

        franja.Turno       = req.Turno;
        franja.Descripcion = req.Descripcion;
        franja.HoraInicio  = req.HoraInicio;
        franja.HoraFin     = req.HoraFin;
        franja.Activa      = req.Activa;
        await _db.SaveChangesAsync();
        return Ok(MapFranjaDto(franja));
    }

    // ── DELETE /api/admin/horarios/{id} ───────────────────────────────────────
    [HttpDelete("horarios/{id}")]
    public async Task<ActionResult> EliminarFranja(int id)
    {
        var franja = await _db.FranjasHorarias.FindAsync(id);
        if (franja is null) return NotFound();
        _db.FranjasHorarias.Remove(franja);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/admin/pedidos  (histórico completo) ──────────────────────────
    [HttpGet("pedidos")]
    public async Task<ActionResult<List<PedidoDto>>> GetPedidos(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] EstadoPedido? estado)
    {
        var query = _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario)
            .AsQueryable();

        if (desde.HasValue)  query = query.Where(p => p.FechaCreacion >= desde);
        if (hasta.HasValue)  query = query.Where(p => p.FechaCreacion <= hasta);
        if (estado.HasValue) query = query.Where(p => p.Estado == estado);

        var pedidos = await query.OrderByDescending(p => p.FechaCreacion).Take(200).ToListAsync();
        return Ok(pedidos.Select(MapPedidoDto).ToList());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static UsuarioDto MapUsuarioDto(Usuario u) =>
        new(u.Id, u.NombreCompleto, u.Email, u.Rol, u.Turno, u.Estado);

    private static FranjaHorariaDto MapFranjaDto(FranjaHoraria f) =>
        new(f.Id, f.Turno, f.Descripcion, f.HoraInicio, f.HoraFin, f.Activa);

    private static PedidoDto MapPedidoDto(Pedido p) => new(
        p.Id, p.NumeroPedido, p.Usuario.NombreCompleto, p.Usuario.Email,
        p.FechaCreacion, p.Estado, p.MetodoPago, p.Total, p.Notas,
        p.Lineas.Select(l => new LineaPedidoDto(
            l.ProductoId, l.Producto.Nombre, l.Cantidad, l.PrecioUnitario, l.Subtotal
        )).ToList());
}

// DTO extra solo para este endpoint
public record CambiarTurnoRequest(Turno? Turno);
