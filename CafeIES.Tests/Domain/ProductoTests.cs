using CafeIES.Shared.Models;

namespace CafeIES.Tests.Domain;

/// <summary>
/// Tests del stock: lógica de negocio en Producto y validación de stock
/// en el flujo de creación de pedidos (misma lógica que PedidosController).
/// </summary>
public class ProductoTests
{
    // ── NivelStock ────────────────────────────────────────────────────────────

    [Fact]
    public void NivelStock_StockMenos1_EsOk()
        => Assert.Equal("ok", Producto(-1).NivelStock);

    [Fact]
    public void NivelStock_StockCero_EsAgotado()
        => Assert.Equal("agotado", Producto(0).NivelStock);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void NivelStock_StockHasta5_EsBajo(int stock)
        => Assert.Equal("bajo", Producto(stock).NivelStock);

    [Theory]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(100)]
    public void NivelStock_StockMayorDe5_EsOk(int stock)
        => Assert.Equal("ok", Producto(stock).NivelStock);

    // ── Validación de stock (lógica de PedidosController) ────────────────────

    [Fact]
    public void Stock_SinControl_SiempreAceptaCualquierCantidad()
    {
        var p = Producto(-1);
        // Stock -1 = sin control; la condición es: stock != -1 && stock < cantidad
        Assert.False(p.Stock != -1 && p.Stock < 100);
    }

    [Fact]
    public void Stock_SuficienteStock_ValidacionPasa()
    {
        var p = Producto(10);
        int cantidad = 5;
        Assert.False(p.Stock != -1 && p.Stock < cantidad);
    }

    [Fact]
    public void Stock_StockExacto_ValidacionPasa()
    {
        var p = Producto(5);
        int cantidad = 5;
        Assert.False(p.Stock != -1 && p.Stock < cantidad);
    }

    [Fact]
    public void Stock_InsuficienteStock_ValidacionFalla()
    {
        var p = Producto(3);
        int cantidad = 5;
        Assert.True(p.Stock != -1 && p.Stock < cantidad);
    }

    [Fact]
    public void Stock_StockCero_ValidacionFalla()
    {
        var p = Producto(0);
        int cantidad = 1;
        Assert.True(p.Stock != -1 && p.Stock < cantidad);
    }

    // ── Decrementar stock (lógica de PedidosController) ──────────────────────

    [Fact]
    public void Stock_AlPedirDecrementaCorrectamente()
    {
        var p = Producto(10);
        int cantidad = 3;
        if (p.Stock != -1) p.Stock -= cantidad;
        Assert.Equal(7, p.Stock);
    }

    [Fact]
    public void Stock_SinControl_NoCambia()
    {
        var p = Producto(-1);
        int cantidad = 100;
        if (p.Stock != -1) p.Stock -= cantidad;
        Assert.Equal(-1, p.Stock);
    }

    // ── Restaurar stock al cancelar (lógica de PedidosController) ────────────

    [Fact]
    public void Stock_AlCancelarSeRestauraCorrecto()
    {
        var p = Producto(7);
        int cantidadLinea = 3;
        if (p.Stock != -1) p.Stock += cantidadLinea;
        Assert.Equal(10, p.Stock);
    }

    [Fact]
    public void Stock_SinControlAlCancelar_NoCambia()
    {
        var p = Producto(-1);
        int cantidadLinea = 3;
        if (p.Stock != -1) p.Stock += cantidadLinea;
        Assert.Equal(-1, p.Stock);
    }

    // ── LineaPedido.Subtotal ──────────────────────────────────────────────────

    [Fact]
    public void Subtotal_EsPrecioUnitarioPorCantidad()
    {
        var linea = new LineaPedido { Cantidad = 3, PrecioUnitario = 2.50m };
        Assert.Equal(7.50m, linea.Subtotal);
    }

    [Fact]
    public void Subtotal_CantidadUno_EsIgualAlPrecioUnitario()
    {
        var linea = new LineaPedido { Cantidad = 1, PrecioUnitario = 3.75m };
        Assert.Equal(3.75m, linea.Subtotal);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Producto Producto(int stock) => new()
    {
        Id          = 1,
        Nombre      = "Test",
        Descripcion = "",
        Precio      = 1.00m,
        Stock       = stock,
        Activo      = true,
        CategoriaId = 1
    };
}
