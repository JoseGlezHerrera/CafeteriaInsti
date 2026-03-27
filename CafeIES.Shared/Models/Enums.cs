namespace CafeIES.Shared.Models;

/// <summary>
/// Turno horario al que pertenece el usuario.
/// La API valida si el turno tiene pedidos habilitados en el momento actual.
/// </summary>
public enum Turno
{
    Manana = 0,   // Turno de mañana
    Tarde  = 1,   // Turno de tarde
    Noche  = 2    // Turno de noche
}

/// <summary>
/// Rol del usuario dentro del sistema.
/// Los alumnos son asignados automáticamente; el resto requiere invitación.
/// </summary>
public enum RolUsuario
{
    Alumno      = 0,   // Se registra solo, requiere validación del admin
    Profesor    = 1,   // Se registra mediante enlace/QR generado por el admin
    Personal    = 2,   // Se registra mediante enlace/QR generado por el admin
    Empleado    = 3,   // Empleado de cafetería, requiere validación del admin
    Admin       = 99   // Creado directamente, acceso total
}

/// <summary>
/// Estado de la cuenta de un usuario.
/// </summary>
public enum EstadoCuenta
{
    PendienteValidacion = 0,  // Alumno recién registrado, esperando aprobación
    Activa              = 1,  // Cuenta operativa
    Suspendida          = 2,  // Bloqueada por el admin
    Rechazada           = 3   // El admin rechazó el registro
}

/// <summary>
/// Estado de un pedido a lo largo de su ciclo de vida.
/// </summary>
public enum EstadoPedido
{
    Pendiente     = 0,  // Pagado, esperando que la cafetería lo vea
    EnPreparacion = 1,  // La cafetería está preparando el pedido
    Listo         = 2,  // El pedido está en el mostrador para recoger
    Entregado     = 3,  // El alumno ha recogido el pedido
    Cancelado     = 4   // Cancelado (por el usuario o por el admin)
}

/// <summary>
/// Método de pago utilizado.
/// </summary>
public enum MetodoPago
{
    Tarjeta    = 0,
    GooglePay  = 1,
    ApplePay   = 2,
    Gratuito   = 3   // Desayuno gratuito (familias desfavorecidas) — sin pasarela de pago
}

/// <summary>
/// Rol del producto dentro del desayuno gratuito para familias desfavorecidas.
/// Un beneficiario puede obtener 1 Zumo + 1 Bocata al día sin coste.
/// </summary>
public enum ComponenteDesayuno
{
    Ninguno = 0,  // No forma parte del desayuno gratuito
    Zumo    = 1,  // Zumo o bebida del desayuno
    Bocata  = 2   // Bocadillo o sándwich del desayuno
}

/// <summary>
/// Tipo de invitación para registro de no-alumnos.
/// </summary>
public enum TipoInvitacion
{
    Profesor = 1,
    Personal = 2,
    Empleado = 3   // Empleado de cafetería — acceso a gestión de pedidos y productos
}
