using CafeIES.API.Services;
using CafeIES.Shared.Models;
using CafeIES.Tests.TestHelpers;

namespace CafeIES.Tests.Services;

/// <summary>
/// D-6: Tests de integración del servicio de desayuno gratuito usando EF InMemory.
/// Cubren la lógica de carga/creación de ConsumoDesayuno y la aplicación del descuento
/// de primera unidad — el mismo camino que recorre el webhook de Stripe.
/// </summary>
public class DesayunoServiceTests
{
    // ── ObtenerOCrearConsumoHoyAsync ──────────────────────────────────────────

    [Fact]
    public async Task ObtenerOCrearConsumoHoy_CreadoNuevo_CuandoNoExiste()
    {
        await using var db = DbContextFactory.Create();
        var svc = new DesayunoService(db);

        var consumo = await svc.ObtenerOCrearConsumoHoyAsync(userId: 1, esBeneficiario: true);

        Assert.NotNull(consumo);
        Assert.Equal(1, consumo.UsuarioId);
        Assert.Equal(DesayunoService.HoyEspaña(), consumo.Fecha);
        Assert.False(consumo.ZumoConsumido);
        Assert.False(consumo.BocataConsumido);
    }

    [Fact]
    public async Task ObtenerOCrearConsumoHoy_ReutilizaExistente_CuandoYaHayRegistroHoy()
    {
        await using var db = DbContextFactory.Create();
        var hoy = DesayunoService.HoyEspaña();
        var existing = new ConsumoDesayuno { UsuarioId = 1, Fecha = hoy, ZumoConsumido = true };
        db.ConsumoDesayunos.Add(existing);
        await db.SaveChangesAsync();

        var svc = new DesayunoService(db);
        var consumo = await svc.ObtenerOCrearConsumoHoyAsync(userId: 1, esBeneficiario: true);

        Assert.NotNull(consumo);
        Assert.True(consumo.ZumoConsumido);   // el estado persiste
        Assert.False(consumo.BocataConsumido);
    }

    [Fact]
    public async Task ObtenerOCrearConsumoHoy_RetornaNull_SiNoEsBeneficiario()
    {
        await using var db = DbContextFactory.Create();
        var svc = new DesayunoService(db);

        var consumo = await svc.ObtenerOCrearConsumoHoyAsync(userId: 99, esBeneficiario: false);

        Assert.Null(consumo);
    }

    [Fact]
    public async Task ObtenerOCrearConsumoHoy_NoCreaRegistro_SiNoEsBeneficiario()
    {
        await using var db = DbContextFactory.Create();
        var svc = new DesayunoService(db);

        await svc.ObtenerOCrearConsumoHoyAsync(userId: 5, esBeneficiario: false);

        Assert.Empty(db.ConsumoDesayunos);
    }

    // ── AplicarDescuentoPrimeraUnidad ─────────────────────────────────────────

    [Fact]
    public void AplicarDescuento_Zumo_MarcaConsumoYDevuelveTrue_PrimeraVez()
    {
        var consumo = new ConsumoDesayuno { UsuarioId = 1, Fecha = DateOnly.FromDateTime(DateTime.Today) };

        var aplicado = DesayunoService.AplicarDescuentoPrimeraUnidad(ComponenteDesayuno.Zumo, consumo);

        Assert.True(aplicado);
        Assert.True(consumo.ZumoConsumido);
    }

    [Fact]
    public void AplicarDescuento_Zumo_RetornaFalse_SiYaConsumido()
    {
        var consumo = new ConsumoDesayuno
        {
            UsuarioId = 1,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            ZumoConsumido = true
        };

        var aplicado = DesayunoService.AplicarDescuentoPrimeraUnidad(ComponenteDesayuno.Zumo, consumo);

        Assert.False(aplicado);
    }

    [Fact]
    public void AplicarDescuento_Bocata_MarcaConsumoYDevuelveTrue_PrimeraVez()
    {
        var consumo = new ConsumoDesayuno { UsuarioId = 1, Fecha = DateOnly.FromDateTime(DateTime.Today) };

        var aplicado = DesayunoService.AplicarDescuentoPrimeraUnidad(ComponenteDesayuno.Bocata, consumo);

        Assert.True(aplicado);
        Assert.True(consumo.BocataConsumido);
    }

    [Fact]
    public void AplicarDescuento_Bocata_RetornaFalse_SiYaConsumido()
    {
        var consumo = new ConsumoDesayuno
        {
            UsuarioId = 1,
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            BocataConsumido = true
        };

        var aplicado = DesayunoService.AplicarDescuentoPrimeraUnidad(ComponenteDesayuno.Bocata, consumo);

        Assert.False(aplicado);
    }

    [Fact]
    public void AplicarDescuento_Ninguno_RetornaFalse()
    {
        var consumo = new ConsumoDesayuno { UsuarioId = 1, Fecha = DateOnly.FromDateTime(DateTime.Today) };

        var aplicado = DesayunoService.AplicarDescuentoPrimeraUnidad(ComponenteDesayuno.Ninguno, consumo);

        Assert.False(aplicado);
        Assert.False(consumo.ZumoConsumido);
        Assert.False(consumo.BocataConsumido);
    }

    // ── MarcarConsumoForzado ──────────────────────────────────────────────────

    [Fact]
    public void MarcarConsumoForzado_Zumo_MarcaZumoConsumido()
    {
        var consumo = new ConsumoDesayuno { UsuarioId = 1, Fecha = DateOnly.FromDateTime(DateTime.Today) };

        DesayunoService.MarcarConsumoForzado(ComponenteDesayuno.Zumo, consumo);

        Assert.True(consumo.ZumoConsumido);
        Assert.False(consumo.BocataConsumido);
    }

    [Fact]
    public void MarcarConsumoForzado_Bocata_MarcaBocataConsumido()
    {
        var consumo = new ConsumoDesayuno { UsuarioId = 1, Fecha = DateOnly.FromDateTime(DateTime.Today) };

        DesayunoService.MarcarConsumoForzado(ComponenteDesayuno.Bocata, consumo);

        Assert.False(consumo.ZumoConsumido);
        Assert.True(consumo.BocataConsumido);
    }

    [Fact]
    public void MarcarConsumoForzado_ZumoYBocata_MarcaAmbos()
    {
        var consumo = new ConsumoDesayuno { UsuarioId = 1, Fecha = DateOnly.FromDateTime(DateTime.Today) };

        DesayunoService.MarcarConsumoForzado(ComponenteDesayuno.Zumo, consumo);
        DesayunoService.MarcarConsumoForzado(ComponenteDesayuno.Bocata, consumo);

        Assert.True(consumo.ZumoConsumido);
        Assert.True(consumo.BocataConsumido);
    }
}
