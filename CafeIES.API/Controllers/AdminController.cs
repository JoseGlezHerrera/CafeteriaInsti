using System.Security.Claims;
using CafeIES.API.Data;
using CafeIES.API.Extensions;
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
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppDbContext db, ILogger<AdminController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── GET /api/admin/dashboard ─────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard([FromQuery] int? institutoId)
    {
        var hoy = DateTime.Now.Date;

        var pedidosQuery = _db.Pedidos.AsQueryable();
        var usuariosQuery = _db.Usuarios.AsQueryable();
        if (institutoId.HasValue)
        {
            pedidosQuery = pedidosQuery.Where(p => p.Usuario.InstitutoId == institutoId);
            usuariosQuery = usuariosQuery.Where(u => u.InstitutoId == institutoId);
        }

        var pedidosHoy    = await pedidosQuery.CountAsync(p => p.FechaCreacion.Date == hoy);
        var ingresosHoy   = await pedidosQuery
            .Where(p => p.FechaCreacion.Date == hoy && p.Estado != EstadoPedido.Cancelado)
            .SumAsync(p => (decimal?)p.Total) ?? 0;
        var productosActivos   = await _db.Productos.CountAsync(p => p.Activo);
        var productosStockBajo = await _db.Productos
            .CountAsync(p => p.Activo && p.Stock >= 0 && p.Stock <= 5);
        var alumnosPendientes  = await usuariosQuery
            .CountAsync(u => u.Estado == EstadoCuenta.PendienteValidacion);

        var enCursoQuery = _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .Where(p => p.Estado == EstadoPedido.Pendiente || p.Estado == EstadoPedido.EnPreparacion);
        if (institutoId.HasValue)
            enCursoQuery = enCursoQuery.Where(p => p.Usuario.InstitutoId == institutoId);

        var pedidosEnCurso = await enCursoQuery
            .OrderBy(p => p.FechaCreacion)
            .Take(10)
            .ToListAsync();

        return Ok(new DashboardDto(
            pedidosHoy, ingresosHoy,
            productosActivos, productosStockBajo,
            alumnosPendientes,
            pedidosEnCurso.Select(p => p.ToDto()).ToList()
        ));
    }

    // ── GET /api/admin/usuarios ───────────────────────────────────────────────
    [HttpGet("usuarios")]
    public async Task<ActionResult<List<UsuarioDto>>> GetUsuarios(
        [FromQuery] EstadoCuenta? estado,
        [FromQuery] RolUsuario?   rol,
        [FromQuery] string?       busqueda,
        [FromQuery] int?          institutoId)
    {
        var query = _db.Usuarios.Include(u => u.Instituto).AsQueryable();
        if (estado.HasValue)      query = query.Where(u => u.Estado == estado);
        if (rol.HasValue)         query = query.Where(u => u.Rol    == rol);
        if (institutoId.HasValue) query = query.Where(u => u.InstitutoId == institutoId);
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(u => u.NombreCompleto.Contains(busqueda) || u.Email.Contains(busqueda));

        var users = await query.OrderBy(u => u.NombreCompleto).ToListAsync();
        return Ok(users.Select(u => u.ToDto()).ToList());
    }

    // ── PATCH /api/admin/usuarios/{id}/validar ────────────────────────────────
    [HttpPatch("usuarios/{id}/validar")]
    public async Task<ActionResult> ValidarAlumno(int id, [FromQuery] bool aprobar)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        var accion = aprobar ? "aprobó" : "rechazó";
        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";

        user.Estado          = aprobar ? EstadoCuenta.Activa : EstadoCuenta.Rechazada;
        user.FechaValidacion = DateTime.Now;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] {Admin} {Accion} la cuenta del usuario {UserId} ({Email})",
            adminEmail, accion, id, user.Email);

        return Ok(new { mensaje = aprobar ? "Cuenta aprobada." : "Cuenta rechazada." });
    }

    // ── PATCH /api/admin/usuarios/{id}/suspender ──────────────────────────────
    [HttpPatch("usuarios/{id}/suspender")]
    public async Task<ActionResult> Suspender(int id)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Rol == RolUsuario.Admin) return BadRequest(new { mensaje = "No se puede suspender al admin." });

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        user.Estado = EstadoCuenta.Suspendida;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] {Admin} suspendió al usuario {UserId} ({Email})",
            adminEmail, id, user.Email);

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

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        user.Estado = EstadoCuenta.Activa;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] {Admin} reactivó la cuenta del usuario {UserId} ({Email})",
            adminEmail, id, user.Email);

        return NoContent();
    }

    // ── PATCH /api/admin/usuarios/{id}/turno ──────────────────────────────────
    [HttpPatch("usuarios/{id}/turno")]
    public async Task<ActionResult> CambiarTurno(int id, [FromBody] CambiarTurnoRequest req)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        var turnoAnterior = user.Turno;
        user.Turno = req.Turno;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] {Admin} cambió el turno del usuario {UserId} ({Email}) de {Anterior} a {Nuevo}",
            adminEmail, id, user.Email, turnoAnterior, req.Turno);

        return NoContent();
    }

    // ── DELETE /api/admin/usuarios/{id} ───────────────────────────────────────
    [HttpDelete("usuarios/{id}")]
    public async Task<ActionResult> EliminarUsuario(int id)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        if (user.Rol == RolUsuario.Admin)
            return BadRequest(new { mensaje = "No se puede eliminar la cuenta de administrador." });

        var tienePedidos = await _db.Pedidos.AnyAsync(p => p.UsuarioId == id);
        if (tienePedidos)
            return BadRequest(new { mensaje = "No se puede eliminar un usuario con pedidos. Suspéndelo en su lugar." });

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        var emailEliminado = user.Email;
        _db.Usuarios.Remove(user);
        await _db.SaveChangesAsync();

        _logger.LogWarning("[AUDIT] {Admin} eliminó permanentemente al usuario {UserId} ({Email})",
            adminEmail, id, emailEliminado);

        return NoContent();
    }

    // ── GET /api/admin/horarios ───────────────────────────────────────────────
    [HttpGet("horarios")]
    public async Task<ActionResult<List<FranjaHorariaDto>>> GetHorarios()
    {
        var franjas = await _db.FranjasHorarias
            .OrderBy(f => f.Turno).ThenBy(f => f.HoraInicio)
            .ToListAsync();
        return Ok(franjas.Select(f => f.ToDto()).ToList());
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

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} creó la franja horaria {Id} ({Turno} {Inicio}-{Fin})",
            adminEmail, franja.Id, franja.Turno, franja.HoraInicio, franja.HoraFin);

        return Ok(franja.ToDto());
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

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} actualizó la franja horaria {Id} ({Turno} {Inicio}-{Fin})",
            adminEmail, id, franja.Turno, franja.HoraInicio, franja.HoraFin);

        return Ok(franja.ToDto());
    }

    // ── DELETE /api/admin/horarios/{id} ───────────────────────────────────────
    [HttpDelete("horarios/{id}")]
    public async Task<ActionResult> EliminarFranja(int id)
    {
        var franja = await _db.FranjasHorarias.FindAsync(id);
        if (franja is null) return NotFound();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _db.FranjasHorarias.Remove(franja);
        await _db.SaveChangesAsync();

        _logger.LogWarning("[AUDIT] {Admin} eliminó la franja horaria {Id} ({Turno} {Inicio}-{Fin})",
            adminEmail, id, franja.Turno, franja.HoraInicio, franja.HoraFin);

        return NoContent();
    }

    // ── GET /api/admin/pedidos  (histórico paginado) ──────────────────────────
    [HttpGet("pedidos")]
    public async Task<ActionResult<PaginatedResponse<PedidoDto>>> GetPedidos(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] EstadoPedido? estado,
        [FromQuery] int? institutoId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page = Math.Max(1, page);

        var query = _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .AsQueryable();

        if (desde.HasValue)       query = query.Where(p => p.FechaCreacion >= desde.Value.Date);
        if (hasta.HasValue)       query = query.Where(p => p.FechaCreacion < hasta.Value.Date.AddDays(1));
        if (estado.HasValue)      query = query.Where(p => p.Estado == estado);
        if (institutoId.HasValue) query = query.Where(p => p.Usuario.InstitutoId == institutoId);

        var totalCount = await query.CountAsync();
        var pedidos = await query
            .OrderByDescending(p => p.FechaCreacion)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PaginatedResponse<PedidoDto>(
            pedidos.Select(p => p.ToDto()).ToList(),
            totalCount, page, pageSize));
    }

    // ── GET /api/admin/institutos ──────────────────────────────────────────────
    [HttpGet("institutos")]
    public async Task<ActionResult<List<InstitutoDto>>> GetInstitutos()
    {
        var institutos = await _db.Institutos
            .OrderBy(i => i.Nombre)
            .Select(i => new InstitutoDto(i.Id, i.Nombre, i.CodigoCorto))
            .ToListAsync();
        return Ok(institutos);
    }
}
