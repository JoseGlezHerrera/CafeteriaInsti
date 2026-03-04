using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.API.Services;

/// <summary>
/// Servicio central de lógica de negocio para restricciones horarias.
/// Determina si un usuario puede realizar un pedido en este momento.
/// </summary>
public class HorarioService
{
    private readonly AppDbContext _db;

    public HorarioService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Comprueba si el turno indicado tiene pedidos habilitados ahora mismo.
    /// Los roles Admin, Profesor y Personal no tienen restricción: siempre pueden pedir.
    /// </summary>
    public async Task<HorarioResult> PuedePedirAhoraAsync(int usuarioId)
    {
        var usuario = await _db.Usuarios.FindAsync(usuarioId);
        if (usuario is null)
            return HorarioResult.Error("Usuario no encontrado.");

        // Admin, Profesor y Personal: sin restricción
        if (usuario.Rol != RolUsuario.Alumno)
            return HorarioResult.Permitido("Sin restricción horaria.");

        // Alumno sin turno asignado (no debería ocurrir)
        if (usuario.Turno is null)
            return HorarioResult.Denegado("Tu cuenta no tiene turno asignado. Contacta con el administrador.");

        var franjas = await _db.FranjasHorarias
            .Where(f => f.Turno == usuario.Turno && f.Activa)
            .ToListAsync();

        if (!franjas.Any())
            return HorarioResult.Denegado("No hay franjas horarias configuradas para tu turno.");

        // ¿Alguna franja está activa ahora mismo?
        var franjaActiva = franjas.FirstOrDefault(f => f.EstaActiva);

        if (franjaActiva is not null)
        {
            // Calcular cuánto queda de la franja
            var fin = TimeOnly.Parse(franjaActiva.HoraFin);
            var ahora = TimeOnly.FromDateTime(DateTime.Now);
            var minutosRestantes = (int)(fin - ahora).TotalMinutes;

            return HorarioResult.Permitido(
                $"Pedidos disponibles hasta las {franjaActiva.HoraFin} ({minutosRestantes} min restantes).",
                franjaActiva);
        }

        // No hay franja activa → calcular la próxima
        var proxima = ObtenerProximaFranja(franjas);
        if (proxima is null)
            return HorarioResult.Denegado("No hay más franjas horarias hoy para tu turno.");

        return HorarioResult.Denegado(
            $"Pedidos no disponibles ahora. Próxima ventana: {proxima.Descripcion} a las {proxima.HoraInicio}.",
            proxima);
    }

    private static FranjaHoraria? ObtenerProximaFranja(IEnumerable<FranjaHoraria> franjas)
    {
        var ahora = TimeOnly.FromDateTime(DateTime.Now);
        return franjas
            .Where(f => TimeOnly.Parse(f.HoraInicio) > ahora)
            .OrderBy(f => TimeOnly.Parse(f.HoraInicio))
            .FirstOrDefault();
    }
}

// ── Resultado tipado ─────────────────────────────────────────────────────────

public class HorarioResult
{
    public bool Puede          { get; init; }
    public string Mensaje      { get; init; } = string.Empty;
    public bool EsError        { get; init; }
    public FranjaHoraria? FranjaActual  { get; init; }
    public FranjaHoraria? ProximaFranja { get; init; }

    public static HorarioResult Permitido(string msg, FranjaHoraria? franja = null)
        => new() { Puede = true,  Mensaje = msg, FranjaActual  = franja };

    public static HorarioResult Denegado(string msg, FranjaHoraria? proxima = null)
        => new() { Puede = false, Mensaje = msg, ProximaFranja = proxima };

    public static HorarioResult Error(string msg)
        => new() { Puede = false, Mensaje = msg, EsError = true };
}
