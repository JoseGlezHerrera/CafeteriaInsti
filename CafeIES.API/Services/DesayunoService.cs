using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Services;

/// <summary>
/// Centraliza la lógica del desayuno gratuito: zona horaria, carga/creación del consumo diario
/// y aplicación del descuento de primera unidad.
/// </summary>
public class DesayunoService
{
    private readonly AppDbContext _db;
    private static readonly TimeZoneInfo SpainTz =
        TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    public DesayunoService(AppDbContext db) => _db = db;

    /// <summary>Fecha de hoy en la zona horaria de España.</summary>
    public static DateOnly HoyEspaña() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SpainTz));

    /// <summary>
    /// Devuelve el <see cref="ConsumoDesayuno"/> de hoy para <paramref name="userId"/>,
    /// creando el registro si no existe. Devuelve <c>null</c> si el usuario no es beneficiario.
    /// El registro se añade al contexto pero <strong>no</strong> se llama a SaveChanges.
    /// </summary>
    public async Task<ConsumoDesayuno?> ObtenerOCrearConsumoHoyAsync(int userId, bool esBeneficiario)
    {
        if (!esBeneficiario) return null;

        var hoy = HoyEspaña();
        var consumo = await _db.ConsumoDesayunos
            .FirstOrDefaultAsync(c => c.UsuarioId == userId && c.Fecha == hoy);
        if (consumo is null)
        {
            consumo = new ConsumoDesayuno { UsuarioId = userId, Fecha = hoy };
            _db.ConsumoDesayunos.Add(consumo);
        }
        return consumo;
    }

    /// <summary>
    /// Comprueba si <paramref name="componente"/> puede beneficiarse del descuento de primera
    /// unidad y, en caso afirmativo, marca el consumo y devuelve <c>true</c>.
    /// Solo aplica una vez por componente por día.
    /// </summary>
    public static bool AplicarDescuentoPrimeraUnidad(ComponenteDesayuno componente, ConsumoDesayuno consumo)
    {
        if (componente == ComponenteDesayuno.Zumo && !consumo.ZumoConsumido)
        {
            consumo.ZumoConsumido = true;
            return true;
        }
        if (componente == ComponenteDesayuno.Bocata && !consumo.BocataConsumido)
        {
            consumo.BocataConsumido = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Marca el componente como consumido sin comprobar si ya se había aplicado.
    /// Usado en el webhook de Stripe para forzar el estado tras confirmación del pago.
    /// </summary>
    public static void MarcarConsumoForzado(ComponenteDesayuno componente, ConsumoDesayuno consumo)
    {
        if (componente == ComponenteDesayuno.Zumo)
            consumo.ZumoConsumido = true;
        else if (componente == ComponenteDesayuno.Bocata)
            consumo.BocataConsumido = true;
    }
}
