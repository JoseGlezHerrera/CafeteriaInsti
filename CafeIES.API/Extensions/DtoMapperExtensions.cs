using CafeIES.Shared.Models;

namespace CafeIES.API.Extensions;

/// <summary>
/// Métodos de extensión para convertir entidades de dominio a DTOs.
/// Centralizan el mapeo y eliminan la duplicación entre controllers.
/// </summary>
public static class DtoMapperExtensions
{
    public static UsuarioDto ToDto(this Usuario u) => new(
        u.Id, u.NombreCompleto, u.Email, u.Rol, u.Turno, u.Estado,
        u.InstitutoId, u.Instituto?.Nombre);

    public static FranjaHorariaDto ToDto(this FranjaHoraria f) => new(
        f.Id, f.Turno, f.Descripcion, f.HoraInicio, f.HoraFin, f.Activa);

    public static PedidoDto ToDto(this Pedido p) => new(
        p.Id, p.NumeroPedido,
        p.Usuario?.NombreCompleto ?? "Desconocido",
        p.Usuario?.Email          ?? "",
        p.FechaCreacion, p.Estado, p.MetodoPago, p.Total, p.Notas,
        p.Lineas.Select(l => new LineaPedidoDto(
            l.ProductoId, l.Producto?.Nombre ?? "Producto eliminado",
            l.Cantidad, l.PrecioUnitario, l.Subtotal
        )).ToList(),
        p.Usuario?.Instituto?.Nombre);
}
