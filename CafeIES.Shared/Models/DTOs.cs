using System.ComponentModel.DataAnnotations;

namespace CafeIES.Shared.Models;

// ── Auth ─────────────────────────────────────────────────────────────────────

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    UsuarioDto Usuario
);

public record RefreshRequest(
    [Required] string RefreshToken
);

// Registro de alumno (autoregistro)
public record RegistroAlumnoRequest(
    [Required, MaxLength(100)] string NombreCompleto,
    [Required, EmailAddress]   string Email,
    [Required, MinLength(8)]   string Password,
    [Required]                 Turno  Turno,
    [Required]                 int    InstitutoId
);

// Registro mediante invitación (profe/personal)
public record RegistroInvitadoRequest(
    [Required]                 string TokenInvitacion,
    [Required, MaxLength(100)] string NombreCompleto,
    [Required, EmailAddress]   string Email,
    [Required, MinLength(8)]   string Password,
    [Required]                 int    InstitutoId
);

// ── Usuario ───────────────────────────────────────────────────────────────────

public record UsuarioDto(
    int         Id,
    string      NombreCompleto,
    string      Email,
    RolUsuario  Rol,
    Turno?      Turno,
    EstadoCuenta Estado,
    int?        InstitutoId,
    string?     InstitutoNombre
);

// ── Instituto ─────────────────────────────────────────────────────────────────

public record InstitutoDto(int Id, string Nombre, string CodigoCorto);

// ── Catálogo ──────────────────────────────────────────────────────────────────

public record CategoriaDto(int Id, string Nombre, string Emoji);

public record ProductoDto(
    int      Id,
    string   Nombre,
    string   Descripcion,
    decimal  Precio,
    int      Stock,
    string?  ImagenUrl,
    bool     Activo,
    string   NivelStock,
    int      CategoriaId,
    string   CategoriaNombre,
    string   CategoriaEmoji
);

public record CrearProductoRequest(
    [Required, MaxLength(120)] string  Nombre,
    [MaxLength(300)]           string  Descripcion,
    [Required]                 decimal Precio,
                               int     Stock,
    [Required]                 int     CategoriaId,
                               string? ImagenUrl
);

public record ActualizarStockRequest([Required] int NuevoStock);

// ── Pedidos ───────────────────────────────────────────────────────────────────

public record CrearPedidoRequest(
    [Required, MinLength(1)] List<LineaPedidoRequest> Lineas,
    MetodoPago MetodoPago,
    string? Notas
);

public record LineaPedidoRequest(
    [Required] int ProductoId,
    [Required, Range(1, 20)] int Cantidad
);

public record PedidoDto(
    int           Id,
    int           NumeroPedido,
    string        UsuarioNombre,
    string        UsuarioEmail,
    DateTime      FechaCreacion,
    EstadoPedido  Estado,
    MetodoPago    MetodoPago,
    decimal       Total,
    string?       Notas,
    List<LineaPedidoDto> Lineas,
    string?       InstitutoNombre
);

public record LineaPedidoDto(
    int     ProductoId,
    string  ProductoNombre,
    int     Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal
);

public record CambiarEstadoRequest([Required] EstadoPedido NuevoEstado);

public record CambiarTurnoRequest(Turno? Turno);

// ── Invitaciones ──────────────────────────────────────────────────────────────

public record CrearInvitacionRequest(
    [Required] TipoInvitacion Tipo,
    int? UsosMaximos,
    int DiasValidez = 7
);

public record InvitacionDto(
    int             Id,
    string          Token,
    TipoInvitacion  Tipo,
    bool            Activa,
    DateTime        FechaExpiracion,
    int?            UsosMaximos,
    int             UsosActuales,
    string          UrlInvitacion,
    bool            EsValida
);

// ── Horarios ──────────────────────────────────────────────────────────────────

public record HorarioStatusDto(
    bool   PuedePedir,
    string Mensaje,
    string? ProximaFranja,
    string? ProximaHora
);

public record FranjaHorariaDto(
    int    Id,
    Turno  Turno,
    string Descripcion,
    string HoraInicio,
    string HoraFin,
    bool   Activa
);

public record UpsertFranjaRequest(
    [Required] Turno  Turno,
    [Required] string Descripcion,
    [Required, RegularExpression(@"^\d{2}:\d{2}$")] string HoraInicio,
    [Required, RegularExpression(@"^\d{2}:\d{2}$")] string HoraFin,
    bool Activa = true
);

// ── Cambiar contraseña ────────────────────────────────────────────────────────

public record CambiarPasswordRequest(
    [Required] string PasswordActual,
    [Required, MinLength(8)] string NuevaPassword
);

// ── Estadísticas del usuario ─────────────────────────────────────────────────

public record UsuarioStatsDto(int TotalPedidos, decimal TotalGastado);

// ── Paginación ───────────────────────────────────────────────────────────────

public record PaginatedResponse<T>(
    List<T> Items,
    int     TotalCount,
    int     Page,
    int     PageSize
);

// ── Dashboard Admin ───────────────────────────────────────────────────────────

public record DashboardDto(
    int     PedidosHoy,
    decimal IngresosHoy,
    int     ProductosActivos,
    int     ProductosStockBajo,
    int     AlumnosPendientes,
    List<PedidoDto> PedidosEnCurso
);
