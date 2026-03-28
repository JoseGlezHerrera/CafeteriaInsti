using System.Security.Claims;
using CafeIES.API.Data;
using CafeIES.API.Extensions;
using CafeIES.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("general")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AdminController> _logger;
    private readonly IMemoryCache _cache;

    public AdminController(AppDbContext db, ILogger<AdminController> logger, IMemoryCache cache)
    {
        _db     = db;
        _logger = logger;
        _cache  = cache;
    }

    /// <summary>
    /// Devuelve el institutoId del admin autenticado extraído del JWT.
    /// Si el admin no tiene instituto asignado (admin global), devuelve null → puede ver todo.
    /// Si tiene instituto, ese es el único que puede ver.
    /// </summary>
    // FIX-08: Usa extensión centralizada en lugar de método duplicado
    private int? GetAdminInstitutoId() => User.GetInstitutoId();

    // ── GET /api/admin/dashboard ─────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard([FromQuery] int? institutoId)
    {
        var hoy = DateTime.UtcNow.Date;

        // El instituto del admin en JWT tiene prioridad — un admin de instituto A no puede ver el B
        var institutoEfectivo = GetAdminInstitutoId() ?? institutoId;

        var pedidosQuery = _db.Pedidos.AsQueryable();
        var usuariosQuery = _db.Usuarios.AsQueryable();
        if (institutoEfectivo.HasValue)
        {
            pedidosQuery = pedidosQuery.Where(p => p.Usuario.InstitutoId == institutoEfectivo);
            usuariosQuery = usuariosQuery.Where(u => u.InstitutoId == institutoEfectivo);
        }

        // FIX-04: SARGable date comparison
        var manana = hoy.AddDays(1);
        var pedidosHoy    = await pedidosQuery.CountAsync(p => p.FechaCreacion >= hoy && p.FechaCreacion < manana && p.Estado != EstadoPedido.Cancelado);
        var ingresosHoy   = await pedidosQuery
            .Where(p => p.FechaCreacion >= hoy && p.FechaCreacion < manana && p.Estado != EstadoPedido.Cancelado)
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
        if (institutoEfectivo.HasValue)
            enCursoQuery = enCursoQuery.Where(p => p.Usuario.InstitutoId == institutoEfectivo);

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
    // FIX-20: Soporte de paginación opcional (retrocompatible)
    [HttpGet("usuarios")]
    public async Task<ActionResult> GetUsuarios(
        [FromQuery] EstadoCuenta? estado,
        [FromQuery] RolUsuario?   rol,
        [FromQuery] string?       busqueda,
        [FromQuery] int?          institutoId,
        [FromQuery] int?          page = null,
        [FromQuery] int           pageSize = 50)
    {
        var institutoEfectivo = GetAdminInstitutoId() ?? institutoId;

        var query = _db.Usuarios.Include(u => u.Instituto).AsQueryable();
        if (estado.HasValue)             query = query.Where(u => u.Estado == estado);
        if (rol.HasValue)                query = query.Where(u => u.Rol    == rol);
        if (institutoEfectivo.HasValue)  query = query.Where(u => u.InstitutoId == institutoEfectivo);
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(u => u.NombreCompleto.Contains(busqueda) || u.Email.Contains(busqueda));

        // Si page está presente, devolver respuesta paginada; si no, devolver lista completa (retrocompatible)
        if (page.HasValue)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            var p = Math.Max(1, page.Value);
            var totalCount = await query.CountAsync();
            var users = await query.OrderBy(u => u.NombreCompleto)
                .Skip((p - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new PaginatedResponse<UsuarioDto>(
                users.Select(u => u.ToDto()).ToList(), totalCount, p, pageSize));
        }

        var allUsers = await query.OrderBy(u => u.NombreCompleto).ToListAsync();
        return Ok(allUsers.Select(u => u.ToDto()).ToList());
    }

    // ── PATCH /api/admin/usuarios/{id}/validar ────────────────────────────────
    [HttpPatch("usuarios/{id}/validar")]
    public async Task<ActionResult> ValidarAlumno(int id, [FromQuery] bool aprobar)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        // BUG-009: admin de instituto A no puede gestionar usuarios de instituto B
        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();

        var accion = aprobar ? "aprobó" : "rechazó";
        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";

        user.Estado          = aprobar ? EstadoCuenta.Activa : EstadoCuenta.Rechazada;
        user.FechaValidacion = DateTime.UtcNow;
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

        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();

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

        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();

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

        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        var turnoAnterior = user.Turno;
        user.Turno = req.Turno;
        await _db.SaveChangesAsync();

        // INC-016: invalidar caché de HorarioService para que el nuevo turno surta efecto de inmediato
        _cache.Remove($"usuario-horario:{id}");

        _logger.LogInformation("[AUDIT] {Admin} cambió el turno del usuario {UserId} ({Email}) de {Anterior} a {Nuevo}",
            adminEmail, id, user.Email, turnoAnterior, req.Turno);

        return NoContent();
    }

    // ── PATCH /api/admin/usuarios/{id}/desayuno-gratuito ─────────────────────
    /// <summary>Activa o desactiva el desayuno gratuito de un usuario beneficiario.</summary>
    [HttpPatch("usuarios/{id}/desayuno-gratuito")]
    public async Task<ActionResult> SetDesayunoGratuito(int id, [FromQuery] bool activo)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        user.DesayunoGratuito = activo;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] {Admin} {Accion} desayuno gratuito al usuario {UserId} ({Email})",
            adminEmail, activo ? "activó" : "desactivó", id, user.Email);

        return NoContent();
    }

    // ── GET /api/admin/desayunos/consumos ─────────────────────────────────────
    /// <summary>
    /// Devuelve los consumos de desayuno gratuito de hoy (o de la fecha indicada).
    /// </summary>
    [HttpGet("desayunos/consumos")]
    public async Task<ActionResult> GetConsumosDesayuno([FromQuery] DateOnly? fecha, [FromQuery] int? institutoId)
    {
        var spainTz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Madrid");
        var dia = fecha ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spainTz));

        var institutoEfectivo = GetAdminInstitutoId() ?? institutoId;

        var query = _db.ConsumoDesayunos
            .Include(c => c.Usuario).ThenInclude(u => u.Instituto)
            .Where(c => c.Fecha == dia);

        if (institutoEfectivo.HasValue)
            query = query.Where(c => c.Usuario.InstitutoId == institutoEfectivo);

        var consumos = await query
            .OrderBy(c => c.Usuario.NombreCompleto)
            .Select(c => new
            {
                UsuarioId     = c.UsuarioId,
                Nombre        = c.Usuario.NombreCompleto,
                Email         = c.Usuario.Email,
                Instituto     = c.Usuario.Instituto != null ? c.Usuario.Instituto.Nombre : "—",
                ZumoConsumido = c.ZumoConsumido,
                BocataConsumido = c.BocataConsumido,
                Fecha         = c.Fecha
            })
            .ToListAsync();

        var totalBeneficiarios = await _db.Usuarios
            .Where(u => u.DesayunoGratuito && u.Estado == EstadoCuenta.Activa &&
                        (institutoEfectivo == null || u.InstitutoId == institutoEfectivo))
            .CountAsync();

        return Ok(new { Fecha = dia, TotalBeneficiarios = totalBeneficiarios, Consumos = consumos });
    }

    // ── PATCH /api/admin/usuarios/{id}/instituto ──────────────────────────────
    [HttpPatch("usuarios/{id}/instituto")]
    public async Task<ActionResult> CambiarInstituto(int id, [FromBody] CambiarInstitutoRequest req)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        // Admin de instituto solo puede gestionar sus propios usuarios y moverlos dentro de su instituto
        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();
        if (adminInstitutoId.HasValue && req.InstitutoId.HasValue && req.InstitutoId != adminInstitutoId)
            return Forbid();

        if (req.InstitutoId.HasValue && !await _db.Institutos.AnyAsync(i => i.Id == req.InstitutoId))
            return BadRequest(new { mensaje = "Instituto no encontrado." });

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        user.InstitutoId = req.InstitutoId;
        await _db.SaveChangesAsync();

        _logger.LogInformation("[AUDIT] {Admin} cambió el instituto del usuario {UserId} ({Email}) a {InstitutoId}",
            adminEmail, id, user.Email, req.InstitutoId?.ToString() ?? "ninguno");

        return NoContent();
    }

    // ── PATCH /api/admin/usuarios/{id}/rol ────────────────────────────────────
    [HttpPatch("usuarios/{id}/rol")]
    public async Task<ActionResult> CambiarRol(int id, [FromBody] CambiarRolRequest req)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();

        // FIX-07: No se puede cambiar el rol de otro admin
        if (user.Rol == RolUsuario.Admin)
            return BadRequest(new { mensaje = "No se puede cambiar el rol de un administrador." });

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        var rolAnterior = user.Rol;
        user.Rol = req.Rol;
        await _db.SaveChangesAsync();

        // INC-016: invalidar caché de HorarioService para que la nueva restricción (o ausencia de ella) sea inmediata
        _cache.Remove($"usuario-horario:{id}");

        _logger.LogInformation("[AUDIT] {Admin} cambió el rol del usuario {UserId} ({Email}) de {Anterior} a {Nuevo}",
            adminEmail, id, user.Email, rolAnterior, req.Rol);

        return NoContent();
    }

    // ── DELETE /api/admin/usuarios/{id} ───────────────────────────────────────
    [HttpDelete("usuarios/{id}")]
    public async Task<ActionResult> EliminarUsuario(int id)
    {
        var user = await _db.Usuarios.FindAsync(id);
        if (user is null) return NotFound();

        var adminInstitutoId = GetAdminInstitutoId();
        if (adminInstitutoId.HasValue && user.InstitutoId != adminInstitutoId) return Forbid();

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
            Activa      = req.Activa,
            EsBloqueada = req.EsBloqueada
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
        franja.EsBloqueada = req.EsBloqueada;
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

        var institutoEfectivo = GetAdminInstitutoId() ?? institutoId;

        var query = _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .AsQueryable();

        if (desde.HasValue)            query = query.Where(p => p.FechaCreacion >= desde.Value.Date);
        if (hasta.HasValue)            query = query.Where(p => p.FechaCreacion < hasta.Value.Date.AddDays(1));
        if (estado.HasValue)           query = query.Where(p => p.Estado == estado);
        if (institutoEfectivo.HasValue) query = query.Where(p => p.Usuario.InstitutoId == institutoEfectivo);

        var totalCount = await query.CountAsync();
        var pedidos = await query
            .OrderByDescending(p => p.FechaCreacion).ThenByDescending(p => p.Id)
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
            .Select(i => new InstitutoDto(i.Id, i.Nombre, i.CodigoCorto, i.Activo, i.Direccion))
            .ToListAsync();
        return Ok(institutos);
    }

    // ── POST /api/admin/institutos ──────────────────────────────────────────────
    // FIX-24: CRUD de institutos
    [HttpPost("institutos")]
    public async Task<ActionResult<InstitutoDto>> CrearInstituto([FromBody] CrearInstitutoRequest req)
    {
        if (await _db.Institutos.AnyAsync(i => i.CodigoCorto == req.CodigoCorto))
            return Conflict(new { mensaje = "Ya existe un instituto con ese código corto." });

        var instituto = new Instituto
        {
            Nombre      = req.Nombre,
            CodigoCorto = req.CodigoCorto,
            Direccion   = req.Direccion ?? string.Empty,
            Activo      = true
        };
        _db.Institutos.Add(instituto);
        await _db.SaveChangesAsync();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} creó el instituto {Id} ({Nombre})",
            adminEmail, instituto.Id, instituto.Nombre);

        return CreatedAtAction(nameof(GetInstitutos),
            new InstitutoDto(instituto.Id, instituto.Nombre, instituto.CodigoCorto, instituto.Activo, instituto.Direccion));
    }

    // ── PUT /api/admin/institutos/{id} ──────────────────────────────────────
    [HttpPut("institutos/{id}")]
    public async Task<ActionResult<InstitutoDto>> ActualizarInstituto(int id, [FromBody] CrearInstitutoRequest req)
    {
        var instituto = await _db.Institutos.FindAsync(id);
        if (instituto is null) return NotFound();

        // Verificar unicidad del código corto si cambió
        if (instituto.CodigoCorto != req.CodigoCorto &&
            await _db.Institutos.AnyAsync(i => i.CodigoCorto == req.CodigoCorto && i.Id != id))
            return Conflict(new { mensaje = "Ya existe otro instituto con ese código corto." });

        instituto.Nombre      = req.Nombre;
        instituto.CodigoCorto = req.CodigoCorto;
        instituto.Direccion   = req.Direccion ?? string.Empty;
        await _db.SaveChangesAsync();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} actualizó el instituto {Id} ({Nombre})",
            adminEmail, id, instituto.Nombre);

        return Ok(new InstitutoDto(instituto.Id, instituto.Nombre, instituto.CodigoCorto, instituto.Activo, instituto.Direccion));
    }

    // ── PATCH /api/admin/institutos/{id}/toggle ─────────────────────────────
    [HttpPatch("institutos/{id}/toggle")]
    public async Task<ActionResult> ToggleInstituto(int id)
    {
        var instituto = await _db.Institutos.FindAsync(id);
        if (instituto is null) return NotFound();

        instituto.Activo = !instituto.Activo;
        await _db.SaveChangesAsync();

        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "admin";
        _logger.LogInformation("[AUDIT] {Admin} {Accion} el instituto {Id} ({Nombre})",
            adminEmail, instituto.Activo ? "activó" : "desactivó", id, instituto.Nombre);

        return Ok(new { activo = instituto.Activo });
    }

    // ── GET /api/admin/alergenos ───────────────────────────────────────────────
    [HttpGet("alergenos")]
    public async Task<ActionResult<List<AlergenoDto>>> GetAlergenos()
    {
        var alergenos = await _db.Alergenos
            .OrderBy(a => a.Id)
            .ToListAsync();
        return Ok(alergenos.Select(a => a.ToDto()).ToList());
    }

    // ── GET /api/admin/diagnostics ────────────────────────────────────────────
    /// <summary>
    /// Devuelve el estado de migraciones y tablas clave. Solo Admin.
    /// Útil para verificar en producción si las migraciones se aplicaron correctamente.
    /// </summary>
    [HttpGet("diagnostics")]
    public async Task<IActionResult> Diagnostics()
    {
        // SqlQueryRaw<T> para tipos primitivos requiere que la columna se llame "Value"
        var migraciones = await _db.Database
            .SqlQueryRaw<string>("SELECT MigrationId AS Value FROM [__EFMigrationsHistory] ORDER BY MigrationId")
            .ToListAsync();

        var tablaDispositivoTokens = await _db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DispositivoTokens'")
            .FirstOrDefaultAsync();

        return Ok(new
        {
            migracionesAplicadas = migraciones,
            tablaDispositivoTokensExiste = tablaDispositivoTokens > 0
        });
    }

}
