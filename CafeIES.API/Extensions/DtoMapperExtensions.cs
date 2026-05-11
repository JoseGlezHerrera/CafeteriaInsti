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
        u.InstitutoId, u.Instituto?.Nombre, u.DesayunoGratuito);

    public static FranjaHorariaDto ToDto(this FranjaHoraria f) => new(
        f.Id, f.Turno, f.Descripcion, f.HoraInicio, f.HoraFin, f.Activa, f.EsBloqueada);

    public static AlergenoDto ToDto(this Alergeno a) => new(a.Id, a.Nombre, a.Emoji);

    public static IngredienteDto ToDto(this Ingrediente i) => new(
        i.Id, i.Nombre, i.Emoji, i.PrecioExtra, i.Stock, i.NivelStock, i.Activo);

    public static ProductoIngredienteDto ToDto(this ProductoIngrediente pi) => new(
        pi.IngredienteId,
        pi.Ingrediente?.Nombre ?? string.Empty,
        pi.Ingrediente?.Emoji  ?? string.Empty,
        pi.Ingrediente?.PrecioExtra ?? 0,
        pi.EsBase, pi.EsQuitable, pi.Orden, pi.CantidadMaxima);

    public static ProductoDto ToDto(this Producto p) => new(
        p.Id, p.Nombre, p.Descripcion, p.Precio, p.Stock,
        p.ImagenUrl, p.Activo, p.NivelStock,
        p.CategoriaId, p.Categoria?.Nombre ?? string.Empty, p.Categoria?.Emoji ?? string.Empty,
        p.Alergenos.Select(a => a.ToDto()).ToList(),
        p.ComponenteDesayuno,
        p.ProductoIngredientes.Count > 0
            ? p.ProductoIngredientes.OrderBy(pi => pi.Ingrediente?.Nombre).Select(pi => pi.ToDto()).ToList()
            : null);

    public static LineaPedidoIngredienteDto ToDto(this LineaPedidoIngrediente li) => new(
        li.IngredienteId ?? 0,
        li.Ingrediente?.Nombre ?? "Ingrediente eliminado",
        li.Ingrediente?.Emoji  ?? string.Empty,
        li.Accion,
        li.PrecioAplicado,
        li.Cantidad);

    public static PedidoDto ToDto(this Pedido p) => new(
        p.Id, p.NumeroPedido,
        p.Usuario?.NombreCompleto ?? "Desconocido",
        p.Usuario?.Email          ?? "",
        DateTime.SpecifyKind(p.FechaCreacion, DateTimeKind.Utc), p.Estado, p.MetodoPago, p.Total, p.Notas,
        p.Lineas.Select(l => new LineaPedidoDto(
            l.ProductoId ?? 0, l.Producto?.Nombre ?? "Producto eliminado",
            (l.Producto?.Alergenos.Count ?? 0) > 0
                ? l.Producto!.Alergenos.Select(a => a.ToDto()).ToList()
                : null,
            l.Cantidad, l.PrecioUnitario, l.Subtotal, l.Notas,
            l.Ingredientes.Count > 0
                ? l.Ingredientes.Select(li => li.ToDto()).ToList()
                : null
        )).ToList(),
        p.Usuario?.Instituto?.Nombre);
}
