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

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public DateTime? FechaValidacion { get; set; }

    /// <summary>Instituto al que pertenece. Null para Admin (gestiona todos).</summary>
    public int? InstitutoId { get; set; }
    public Instituto? Instituto { get; set; }

    /// <summary>Token de refresco JWT activo.</summary>
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    /// <summary>
    /// Si true, el usuario pertenece a una familia desfavorecida y tiene derecho
    /// a 1 zumo + 1 bocata gratuitos por día (programa de desayuno escolar).
    /// </summary>
    public bool DesayunoGratuito { get; set; } = false;

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

    /// <summary>Si true, los pedidos están BLOQUEADOS durante esta franja (en clase). Si false, son PERMITIDOS.</summary>
    public bool EsBloqueada { get; set; } = false;

    /// <summary>
    /// Comprueba si DateTime.Now cae dentro de esta franja.
    /// Soporta franjas que cruzan medianoche (ej: 21:00-03:00).
    /// </summary>
    [NotMapped]
    public bool EstaActiva
    {
        get
        {
            if (!Activa) return false;
            var spainTz = TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Madrid");
            var ahora = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spainTz));
            return EstaActivaEn(ahora);
        }
    }

    /// <summary>
    /// Comprueba si la franja está activa en el momento indicado.
    /// Permite inyectar la hora en tests unitarios sin depender del reloj del sistema.
    /// </summary>
    public bool EstaActivaEn(TimeOnly ahora)
    {
        if (!Activa) return false;
        if (!TimeOnly.TryParse(HoraInicio, out var inicio) ||
            !TimeOnly.TryParse(HoraFin,    out var fin))
            return false; // Formato de hora inválido — franja inactiva por seguridad
        // Soporte franjas que cruzan medianoche (ej: 21:00-03:00)
        if (inicio > fin)
            return ahora >= inicio || ahora <= fin;
        return ahora >= inicio && ahora <= fin;
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

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public DateTime FechaExpiracion { get; set; } = DateTime.UtcNow.AddDays(7);

    /// <summary>Cuántas veces se puede usar. Null = ilimitado mientras esté activa.</summary>
    public int? UsosMaximos { get; set; }

    /// <summary>ConcurrencyCheck evita que dos registros simultáneos superen UsosMaximos.</summary>
    [ConcurrencyCheck]
    public int UsosActuales { get; set; } = 0;

    [NotMapped]
    public bool EsValida => Activa
                         && DateTime.UtcNow <= FechaExpiracion
                         && (UsosMaximos == null || UsosActuales < UsosMaximos);

    /// <summary>
    /// Ruta relativa del enlace de invitación — el cliente debe anteponer la BaseUrl de la API.
    /// Ejemplo completo: https://cafeies-api.azurewebsites.net/registro/invitacion/{Token}
    /// </summary>
    [NotMapped]
    public string UrlInvitacion => $"/registro/invitacion/{Token}";
}


// ─────────────────────────────────────────────
//  CATÁLOGO
// ─────────────────────────────────────────────

/// <summary>Alérgeno según el Reglamento (UE) 1169/2011 (14 alérgenos de declaración obligatoria).</summary>
public class Alergeno
{
    public int Id { get; set; }
    [Required, MaxLength(60)]
    public string Nombre { get; set; } = string.Empty;
    [MaxLength(10)]
    public string Emoji { get; set; } = string.Empty;
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

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
    [ConcurrencyCheck]
    public int Stock { get; set; } = -1;

    /// <summary>Ruta o URL de la imagen del producto.</summary>
    [MaxLength(500)]
    public string? ImagenUrl { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Indica si este producto forma parte del desayuno gratuito (zumo o bocata).
    /// Usado para aplicar precio 0 a beneficiarios del programa de desayuno escolar.
    /// </summary>
    public ComponenteDesayuno ComponenteDesayuno { get; set; } = ComponenteDesayuno.Ninguno;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    // Navegación
    public ICollection<LineaPedido>         Lineas               { get; set; } = new List<LineaPedido>();
    public ICollection<Alergeno>            Alergenos            { get; set; } = new List<Alergeno>();
    public ICollection<ProductoIngrediente> ProductoIngredientes { get; set; } = new List<ProductoIngrediente>();

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

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

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

    public int? ProductoId { get; set; }
    public Producto? Producto { get; set; }

    public int Cantidad { get; set; }

    /// <summary>Precio en el momento de la compra (no varía aunque cambie el producto).</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal PrecioUnitario { get; set; }

    /// <summary>Nota opcional del usuario para esta línea (ej. "sin queso", "extra picante").</summary>
    [MaxLength(200)]
    public string? Notas { get; set; }

    // Navegación
    public ICollection<LineaPedidoIngrediente> Ingredientes { get; set; } = new List<LineaPedidoIngrediente>();

    [NotMapped]
    public decimal Subtotal => Cantidad * PrecioUnitario;
}


// ─────────────────────────────────────────────
//  INGREDIENTES PERSONALIZABLES
// ─────────────────────────────────────────────

/// <summary>
/// Ingrediente del catálogo de la cafetería.
/// Puede ser un componente base de un producto (jamón, queso…) o un extra que
/// el cliente puede añadir al pedido (doble jamón, picante, etc.).
/// </summary>
public class Ingrediente
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Emoji representativo (opcional).</summary>
    [MaxLength(10)]
    public string Emoji { get; set; } = string.Empty;

    /// <summary>
    /// Suplemento de precio que se añade al total de la línea cuando el cliente
    /// elige este ingrediente como extra. 0 para ingredientes sin coste adicional.
    /// </summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal PrecioExtra { get; set; } = 0;

    /// <summary>Unidades disponibles. -1 = sin control de stock.</summary>
    [ConcurrencyCheck]
    public int Stock { get; set; } = -1;

    public bool Activo { get; set; } = true;

    // Navegación
    public ICollection<ProductoIngrediente>    ProductoIngredientes    { get; set; } = new List<ProductoIngrediente>();
    public ICollection<LineaPedidoIngrediente> LineaPedidoIngredientes { get; set; } = new List<LineaPedidoIngrediente>();

    /// <summary>Nivel de stock: "ok" / "bajo" / "agotado"</summary>
    [NotMapped]
    public string NivelStock => Stock switch
    {
        -1   => "ok",
        0    => "agotado",
        <= 5 => "bajo",
        _    => "ok"
    };
}

/// <summary>
/// Asociación entre un producto y un ingrediente, con la configuración de cómo
/// ese ingrediente aparece en la pantalla de personalización del cliente.
/// </summary>
public class ProductoIngrediente
{
    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int IngredienteId { get; set; }
    public Ingrediente Ingrediente { get; set; } = null!;

    /// <summary>
    /// true  → el ingrediente viene incluido por defecto en el producto (ej. jamón en bocata de jamón).
    /// false → es un extra opcional que el cliente puede añadir pagando el suplemento.
    /// </summary>
    public bool EsBase { get; set; }

    /// <summary>
    /// Solo aplica cuando EsBase=true.
    /// true  → el cliente puede quitar este ingrediente sin coste (ej. sin tomate).
    /// false → el ingrediente es parte indivisible del producto y no puede eliminarse (ej. el pan).
    /// </summary>
    public bool EsQuitable { get; set; }

    /// <summary>Orden de visualización en la UI del cliente.</summary>
    public int Orden { get; set; }
}

/// <summary>
/// Modificación de ingrediente dentro de una línea de pedido concreta.
/// Registra si el cliente añadió un extra o quitó un componente base,
/// junto con el precio aplicado en el momento del pedido (snapshot inmutable).
/// </summary>
public class LineaPedidoIngrediente
{
    public int Id { get; set; }

    public int LineaPedidoId { get; set; }
    public LineaPedido LineaPedido { get; set; } = null!;

    /// <summary>Nullable: se pone a null si el ingrediente del catálogo se elimina (SetNull).</summary>
    public int? IngredienteId { get; set; }
    public Ingrediente? Ingrediente { get; set; }

    /// <summary>Quitar (0) = quita un ingrediente base sin coste. Añadir (1) = añade un extra con suplemento.</summary>
    public AccionIngrediente Accion { get; set; }

    /// <summary>
    /// Precio del suplemento en el momento del pedido.
    /// 0 para acciones Quitar. Igual a Ingrediente.PrecioExtra al crear el pedido.
    /// </summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal PrecioAplicado { get; set; }
}


// ─────────────────────────────────────────────
//  TOKEN DE NOTIFICACIONES PUSH
// ─────────────────────────────────────────────

// ─────────────────────────────────────────────
//  DESAYUNO GRATUITO
// ─────────────────────────────────────────────

/// <summary>
/// Registra el consumo diario del desayuno gratuito por beneficiario.
/// Un registro por usuario por día. ZumoConsumido/BocataConsumido se fijan
/// en la transacción del pedido para evitar doble uso.
/// </summary>
public class ConsumoDesayuno
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    /// <summary>Fecha del día en hora española (Europe/Madrid).</summary>
    public DateOnly Fecha { get; set; }

    public bool ZumoConsumido  { get; set; } = false;
    public bool BocataConsumido { get; set; } = false;
}


// ─────────────────────────────────────────────
//  TOKEN DE NOTIFICACIONES PUSH
// ─────────────────────────────────────────────

/// <summary>Plataforma de dispositivo para notificaciones push.</summary>
public enum PlataformaDispositivo { Android, iOS, Web }

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

    /// <summary>
    /// Plataforma del dispositivo. Almacenada como string para compatibilidad con la BD existente.
    /// Valores válidos: "android" | "ios". Ver enum <see cref="PlataformaDispositivo"/>.
    /// NOTA: Cambiar la propiedad a enum requeriría una migración de BD (nvarchar → int).
    /// </summary>
    [MaxLength(10)]
    public string Plataforma { get; set; } = string.Empty;

    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
}
