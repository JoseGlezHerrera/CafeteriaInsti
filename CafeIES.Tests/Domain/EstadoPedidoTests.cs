using CafeIES.Shared.Models;

namespace CafeIES.Tests.Domain;

/// <summary>
/// Verifica la máquina de estados de pedidos.
/// La tabla de transiciones válidas está definida en PedidosController._transicionesValidas.
/// Estos tests documentan y protegen ese comportamiento.
/// </summary>
public class EstadoPedidoTests
{
    // Espejo de _transicionesValidas de PedidosController
    private static readonly Dictionary<EstadoPedido, EstadoPedido[]> TransicionesValidas = new()
    {
        [EstadoPedido.Pendiente]     = [EstadoPedido.EnPreparacion, EstadoPedido.Cancelado],
        [EstadoPedido.EnPreparacion] = [EstadoPedido.Listo, EstadoPedido.Cancelado],
        [EstadoPedido.Listo]         = [EstadoPedido.Entregado],
        [EstadoPedido.Entregado]     = [],
        [EstadoPedido.Cancelado]     = []
    };

    // ── Transiciones permitidas ───────────────────────────────────────────────

    [Theory]
    [InlineData(EstadoPedido.Pendiente,     EstadoPedido.EnPreparacion)]
    [InlineData(EstadoPedido.Pendiente,     EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.EnPreparacion, EstadoPedido.Listo)]
    [InlineData(EstadoPedido.EnPreparacion, EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Listo,         EstadoPedido.Entregado)]
    public void Transicion_Valida_EsPermitida(EstadoPedido desde, EstadoPedido hasta)
    {
        Assert.True(EsTransicionValida(desde, hasta));
    }

    // ── Transiciones rechazadas ───────────────────────────────────────────────

    [Theory]
    [InlineData(EstadoPedido.Entregado,     EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Entregado,     EstadoPedido.Pendiente)]
    [InlineData(EstadoPedido.Cancelado,     EstadoPedido.Pendiente)]
    [InlineData(EstadoPedido.Cancelado,     EstadoPedido.EnPreparacion)]
    [InlineData(EstadoPedido.Listo,         EstadoPedido.Pendiente)]
    [InlineData(EstadoPedido.Listo,         EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Pendiente,     EstadoPedido.Listo)]
    [InlineData(EstadoPedido.Pendiente,     EstadoPedido.Entregado)]
    [InlineData(EstadoPedido.EnPreparacion, EstadoPedido.Pendiente)]
    [InlineData(EstadoPedido.EnPreparacion, EstadoPedido.Entregado)]
    public void Transicion_Invalida_EsRechazada(EstadoPedido desde, EstadoPedido hasta)
    {
        Assert.False(EsTransicionValida(desde, hasta));
    }

    // ── Estados terminales sin transiciones ──────────────────────────────────

    [Theory]
    [InlineData(EstadoPedido.Entregado)]
    [InlineData(EstadoPedido.Cancelado)]
    public void EstadoTerminal_NoTieneTransiciones(EstadoPedido estado)
    {
        var permitidos = TransicionesValidas[estado];
        Assert.Empty(permitidos);
    }

    // ── Flujo completo happy path ─────────────────────────────────────────────

    [Fact]
    public void FlujoCompleto_Pendiente_EnPreparacion_Listo_Entregado_EsValido()
    {
        var flujo = new[]
        {
            (EstadoPedido.Pendiente,     EstadoPedido.EnPreparacion),
            (EstadoPedido.EnPreparacion, EstadoPedido.Listo),
            (EstadoPedido.Listo,         EstadoPedido.Entregado)
        };

        foreach (var (desde, hasta) in flujo)
            Assert.True(EsTransicionValida(desde, hasta),
                $"Se esperaba que '{desde}' → '{hasta}' fuera válido");
    }

    [Fact]
    public void FlujoCompleto_PedidoCancelado_EsValido()
    {
        Assert.True(EsTransicionValida(EstadoPedido.Pendiente, EstadoPedido.Cancelado));
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static bool EsTransicionValida(EstadoPedido desde, EstadoPedido hasta)
        => TransicionesValidas.TryGetValue(desde, out var permitidos)
           && permitidos.Contains(hasta);
}
