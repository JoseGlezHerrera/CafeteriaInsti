using System.Security.Claims;
using CafeIES.API.Data;
using CafeIES.Shared.Models;
using CafeIES.API.Hubs;
using CafeIES.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PedidosController : ControllerBase
{
    private readonly AppDbContext    _db;
    private readonly HorarioService  _horario;
    private readonly StripeService   _stripe;
    private readonly IHubContext<CafeteriaHub> _hub;

    public PedidosController(AppDbContext db, HorarioService horario, StripeService stripe, IHubContext<CafeteriaHub> hub)
    {
        _db      = db;
        _horario = horario;
        _stripe  = stripe;
        _hub     = hub;
    }

    // ── GET /api/pedidos/puedo-pedir ─────────────────────────────────────────
    /// <summary>La app consulta esto al abrir la pantalla para mostrar/ocultar el banner.</summary>
    [HttpGet("puedo-pedir")]
    public async Task<ActionResult<HorarioStatusDto>> PuedoPedir()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var result = await _horario.PuedePedirAhoraAsync(userId);

        return Ok(new HorarioStatusDto(
            result.Puede,
            result.Mensaje,
            result.ProximaFranja?.Descripcion,
            result.ProximaFranja?.HoraInicio
        ));
    }

    // ── POST /api/pedidos ────────────────────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<PedidoDto>> Crear([FromBody] CrearPedidoRequest req)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        // 1. Comprobar horario
        var horario = await _horario.PuedePedirAhoraAsync(userId);
        if (!horario.Puede)
            return BadRequest(new { mensaje = horario.Mensaje });

        // Usar transacción para evitar race conditions de stock
        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 2. Calcular total y validar stock
            var lineas = new List<LineaPedido>();
            decimal total = 0;

            foreach (var l in req.Lineas)
            {
                var producto = await _db.Productos.FindAsync(l.ProductoId);
                if (producto is null || !producto.Activo)
                    return BadRequest(new { mensaje = $"Producto #{l.ProductoId} no disponible." });

                if (producto.Stock != -1 && producto.Stock < l.Cantidad)
                    return BadRequest(new { mensaje = $"Stock insuficiente para '{producto.Nombre}'. Disponibles: {producto.Stock}." });

                // Decrementar stock inmediatamente para evitar doble lectura
                if (producto.Stock != -1) producto.Stock -= l.Cantidad;

                lineas.Add(new LineaPedido
                {
                    ProductoId     = l.ProductoId,
                    Cantidad       = l.Cantidad,
                    PrecioUnitario = producto.Precio
                });
                total += producto.Precio * l.Cantidad;
            }

            // 3. Verificar pago con Stripe (si se proporcionó)
            string? referenciaPago = null;
            if (!string.IsNullOrEmpty(req.StripePaymentIntentId))
            {
                var (pagado, status) = await _stripe.VerificarPagoAsync(req.StripePaymentIntentId);
                if (!pagado)
                    return BadRequest(new { mensaje = $"El pago no se ha completado (estado: {status}). Inténtalo de nuevo." });
                referenciaPago = req.StripePaymentIntentId;
            }

            // 4. Número de pedido secuencial del día
            var hoy = DateTime.Now.Date;
            var ultimoNumero = await _db.Pedidos
                .Where(p => p.FechaCreacion.Date == hoy)
                .MaxAsync(p => (int?)p.NumeroPedido) ?? 0;

            var pedido = new Pedido
            {
                UsuarioId      = userId,
                NumeroPedido   = ultimoNumero + 1,
                MetodoPago     = req.MetodoPago,
                Total          = total,
                Notas          = req.Notas,
                Lineas         = lineas,
                ReferenciasPago = referenciaPago
            };

            _db.Pedidos.Add(pedido);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            // 4. Notificar a la cafetería en tiempo real vía SignalR
            var dto = await GetPedidoDtoAsync(pedido.Id);
            await _hub.Clients.Group("cafeteria").SendAsync("NuevoPedido", dto);

            return CreatedAtAction(nameof(GetById), new { id = pedido.Id }, dto);
        }
        catch
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { mensaje = "Error al procesar el pedido. Inténtalo de nuevo." });
        }
    }

    // ── GET /api/pedidos/mis-pedidos ─────────────────────────────────────────
    [HttpGet("mis-pedidos")]
    public async Task<ActionResult<List<PedidoDto>>> MisPedidos()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var pedidos = await _db.Pedidos
            .Where(p => p.UsuarioId == userId)
            .OrderByDescending(p => p.FechaCreacion)
            .Take(20)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .ToListAsync();

        return Ok(pedidos.Select(MapDto).ToList());
    }

    // ── GET /api/pedidos/mis-stats ───────────────────────────────────────────
    [HttpGet("mis-stats")]
    public async Task<ActionResult<UsuarioStatsDto>> MisEstadisticas()
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var query = _db.Pedidos.Where(p => p.UsuarioId == userId && p.Estado != EstadoPedido.Cancelado);
        var totalPedidos = await query.CountAsync();
        var totalGastado = await query.SumAsync(p => (decimal?)p.Total) ?? 0;
        return Ok(new UsuarioStatsDto(totalPedidos, totalGastado));
    }

    // ── GET /api/pedidos/{id} ────────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<ActionResult<PedidoDto>> GetById(int id)
    {
        var pedido = await _db.Pedidos.FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();

        // Verificar propiedad: solo el dueño o Admin/Personal pueden ver el pedido
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var esStaff = User.IsInRole("Admin") || User.IsInRole("Personal");
        if (pedido.UsuarioId != userId && !esStaff)
            return Forbid();

        var dto = await GetPedidoDtoAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    // ── Transiciones de estado válidas ────────────────────────────────────────
    private static readonly Dictionary<EstadoPedido, EstadoPedido[]> _transicionesValidas = new()
    {
        [EstadoPedido.Pendiente]     = [EstadoPedido.EnPreparacion, EstadoPedido.Cancelado],
        [EstadoPedido.EnPreparacion] = [EstadoPedido.Listo, EstadoPedido.Cancelado],
        [EstadoPedido.Listo]         = [EstadoPedido.Entregado],
        [EstadoPedido.Entregado]     = [],
        [EstadoPedido.Cancelado]     = []
    };

    // ── PATCH /api/pedidos/{id}/estado  (Admin / Cafetería) ──────────────────
    [HttpPatch("{id}/estado")]
    [Authorize(Roles = "Admin,Personal")]
    public async Task<ActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoRequest req)
    {
        var pedido = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pedido is null) return NotFound();

        // Validar transición de estado
        if (!_transicionesValidas.TryGetValue(pedido.Estado, out var permitidos) ||
            !permitidos.Contains(req.NuevoEstado))
        {
            return BadRequest(new { mensaje = $"No se puede cambiar de '{pedido.Estado}' a '{req.NuevoEstado}'." });
        }

        // Restaurar stock si se cancela un pedido
        if (req.NuevoEstado == EstadoPedido.Cancelado)
        {
            foreach (var linea in pedido.Lineas)
            {
                if (linea.Producto.Stock != -1)
                    linea.Producto.Stock += linea.Cantidad;
            }
        }

        pedido.Estado = req.NuevoEstado;
        await _db.SaveChangesAsync();

        // Notificar al usuario propietario del pedido
        await _hub.Clients.Group($"user-{pedido.UsuarioId}")
            .SendAsync("EstadoPedidoActualizado", new { pedido.Id, Estado = req.NuevoEstado.ToString() });

        return NoContent();
    }

    // ── GET /api/pedidos/en-curso  (Admin / Cafetería) ───────────────────────
    [HttpGet("en-curso")]
    [Authorize(Roles = "Admin,Personal")]
    public async Task<ActionResult<List<PedidoDto>>> EnCurso()
    {
        var pedidos = await _db.Pedidos
            .Where(p => p.Estado == EstadoPedido.Pendiente || p.Estado == EstadoPedido.EnPreparacion)
            .OrderBy(p => p.FechaCreacion)
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .ToListAsync();

        return Ok(pedidos.Select(MapDto).ToList());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private async Task<PedidoDto?> GetPedidoDtoAsync(int id)
    {
        var p = await _db.Pedidos
            .Include(p => p.Lineas).ThenInclude(l => l.Producto)
            .Include(p => p.Usuario).ThenInclude(u => u.Instituto)
            .FirstOrDefaultAsync(p => p.Id == id);

        return p is null ? null : MapDto(p);
    }

    private static PedidoDto MapDto(Pedido p) => new(
        p.Id,
        p.NumeroPedido,
        p.Usuario.NombreCompleto,
        p.Usuario.Email,
        p.FechaCreacion,
        p.Estado,
        p.MetodoPago,
        p.Total,
        p.Notas,
        p.Lineas.Select(l => new LineaPedidoDto(
            l.ProductoId,
            l.Producto.Nombre,
            l.Cantidad,
            l.PrecioUnitario,
            l.Subtotal)).ToList(),
        p.Usuario.Instituto?.Nombre
    );
}
