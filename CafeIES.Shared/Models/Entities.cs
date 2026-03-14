using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeIES.Shared.Models;

// ─────────────────────────────────────────────
//  INSTITUTO
// ─────────────────────────────────────────────

/// <summary>
/// Centro educativo que utiliza la plataforma.
/// Cada usuario pertenece a un instituto; el admin gestiona todos.
/// </summary>
public class Instituto
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Direccion { get; set; } = string.Empty;

    /// <summary>Código corto para identificación rápida (ej: "IES-NORTE").</summary>
    [Required, MaxLength(20)]
    public string CodigoCorto { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    // Navegación
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}


// ─────────────────────────────────────────────
//  USUARIO
// ─────────────────────────────────────────────

/// <summary>
/// Representa cualquier usuario del sistema (alumno, profesor, personal, admin).
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required, MaxLength(150), EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash bcrypt de la contraseña. Nunca almacenar en texto plano.</summary>
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public RolUsuario Rol { get; set; } = RolUsuario.Alumno;

    public Turno? Turno { get; set; }  // Null para Admin (sin restricción horaria)

    public EstadoCuenta Estado { get; set; } = EstadoCuenta.PendienteValidacion;

    public DateTime FechaRegistro { get; set; } = DateTime.Now;

    public DateTime? FechaValidacion { get; set; }

    /// <summary>Instituto al que pertenece. Null para Admin (gestiona todos).</summary>
    public int? InstitutoId { get; set; }
    public Instituto? Instituto { get; set; }

    /// <summary>Token de refresco JWT activo.</summary>
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Navegación
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}


// ─────────────────────────────────────────────
//  CONFIGURACIÓN DE HORARIOS
// ─────────────────────────────────────────────

/// <summary>
/// Define las franjas horarias en las que un turno puede realizar pedidos.
/// Múltiples franjas por turno (ej: antes de entrar + recreo).
/// Configurable por el admin sin tocar código.
/// </summary>
public class FranjaHoraria
{
    public int Id { get; set; }

    public Turno Turno { get; set; }

    [Required, MaxLength(60)]
    public string Descripcion { get; set; } = string.Empty;  // ej: "Recreo"

    /// <summary>Hora de inicio en formato HH:mm (ej: "11:00")</summary>
    [Required, MaxLength(5)]
    public string HoraInicio { get; set; } = string.Empty;

    /// <summary>Hora de fin en formato HH:mm (ej: "11:30")</summary>
    [Required, MaxLength(5)]
    public string HoraFin { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;

    /// <summary>
    /// Comprueba si DateTime.Now cae dentro de esta franja.
    /// </summary>
    [NotMapped]
    public bool EstaActiva
    {
        get
        {
            if (!Activa) return false;
            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            var inicio = TimeOnly.Parse(HoraInicio);
            var fin = TimeOnly.Parse(HoraFin);
            return ahora >= inicio && ahora <= fin;
        }
    }
}


// ─────────────────────────────────────────────
//  INVITACIÓN
// ─────────────────────────────────────────────

/// <summary>
/// Token de invitación generado por el admin para que profesores o personal
/// puedan registrarse con el rol correcto ya asignado.
/// </summary>
public class Invitacion
{
    public int Id { get; set; }

    /// <summary>Token único UUID que va en el enlace/QR.</summary>
    [Required]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public TipoInvitacion Tipo { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public DateTime FechaExpiracion { get; set; } = DateTime.Now.AddDays(7);

    /// <summary>Cuántas veces se puede usar. Null = ilimitado mientras esté activa.</summary>
    public int? UsosMaximos { get; set; }

    public int UsosActuales { get; set; } = 0;

    [NotMapped]
    public bool EsValida => Activa
                         && DateTime.Now <= FechaExpiracion
                         && (UsosMaximos == null || UsosActuales < UsosMaximos);

    /// <summary>URL completa que se mostrará en el QR.</summary>
    [NotMapped]
    public string UrlInvitacion => $"/registro/invitacion/{Token}";
}


// ─────────────────────────────────────────────
//  CATÁLOGO
// ─────────────────────────────────────────────

/// <summary>Categoría de producto (Bocadillos, Bebidas, Postres, etc.)</summary>
public class Categoria
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Emoji representativo, se usa en la app.</summary>
    [MaxLength(10)]
    public string Emoji { get; set; } = "🍽️";

    public int Orden { get; set; } = 0;

    public bool Activa { get; set; } = true;

    // Navegación
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

/// <summary>Producto de la cafetería.</summary>
public class Producto
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Descripcion { get; set; } = string.Empty;

    [Column(TypeName = "decimal(6,2)")]
    public decimal Precio { get; set; }

    /// <summary>Unidades disponibles. -1 = sin control de stock.</summary>
    public int Stock { get; set; } = -1;

    /// <summary>Ruta o URL de la imagen del producto.</summary>
    [MaxLength(500)]
    public string? ImagenUrl { get; set; }

    public bool Activo { get; set; } = true;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    // Navegación
    public ICollection<LineaPedido> Lineas { get; set; } = new List<LineaPedido>();

    /// <summary>Nivel de stock: "ok" / "bajo" / "agotado"</summary>
    [NotMapped]
    public string NivelStock => Stock switch
    {
        -1 => "ok",
        0  => "agotado",
        <= 5 => "bajo",
        _  => "ok"
    };
}


// ─────────────────────────────────────────────
//  PEDIDOS
// ─────────────────────────────────────────────

/// <summary>Pedido realizado por un usuario.</summary>
public class Pedido
{
    public int Id { get; set; }

    /// <summary>Número de pedido visible para el usuario y la cafetería (ej: #042).</summary>
    public int NumeroPedido { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public DateTime FechaCreacion { get; set; } = DateTime.Now;

    public EstadoPedido Estado { get; set; } = EstadoPedido.Pendiente;

    public MetodoPago MetodoPago { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal Total { get; set; }

    /// <summary>Notas del cliente (ej: "sin lechuga").</summary>
    [MaxLength(300)]
    public string? Notas { get; set; }

    /// <summary>Referencia del pago en la pasarela (Stripe, Redsys...).</summary>
    [MaxLength(200)]
    public string? ReferenciasPago { get; set; }

    // Navegación
    public ICollection<LineaPedido> Lineas { get; set; } = new List<LineaPedido>();
}

/// <summary>Línea de producto dentro de un pedido.</summary>
public class LineaPedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; }

    /// <summary>Precio en el momento de la compra (no varía aunque cambie el producto).</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal PrecioUnitario { get; set; }

    [NotMapped]
    public decimal Subtotal => Cantidad * PrecioUnitario;
}


// ─────────────────────────────────────────────
//  TOKEN DE NOTIFICACIONES PUSH
// ─────────────────────────────────────────────

/// <summary>
/// Token FCM registrado por el dispositivo móvil de un usuario.
/// Se almacena para enviar notificaciones push (p. ej. pedido listo).
/// Un usuario puede tener varios tokens (varios dispositivos).
/// </summary>
public class DispositivoToken
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Token de registro FCM (Android) / APNs vía FCM (iOS).</summary>
    [Required, MaxLength(512)]
    public string Token { get; set; } = string.Empty;

    /// <summary>"android" | "ios"</summary>
    [MaxLength(10)]
    public string Plataforma { get; set; } = string.Empty;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
