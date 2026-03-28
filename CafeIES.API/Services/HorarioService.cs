using CafeIES.API.Data;
using CafeIES.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CafeIES.API.Services;

/// <summary>
/// Servicio central de lógica de negocio para restricciones horarias.
/// Determina si un usuario puede realizar un pedido en este momento.
/// Las franjas horarias se cachean 2 minutos para evitar una query SQL en cada petición.
/// </summary>
public class HorarioService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public HorarioService(AppDbContext db, IMemoryCache cache)
    {
        _db    = db;
        _cache = cache;
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

        // Cachear franjas por turno (cambian raramente; 2 min es seguro)
        var cacheKey = $"franjas:{(int)usuario.Turno}";
        var franjas = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _db.FranjasHorarias
                .Where(f => f.Turno == usuario.Turno && f.Activa)
                .ToListAsync();
        });

        if (franjas is null || !franjas.Any())
            return HorarioResult.Permitido("Pedidos disponibles.");

        // ¿Hay alguna franja bloqueada activa ahora mismo?
        var franjaBloquedaActiva = franjas.FirstOrDefault(f => f.EsBloqueada && f.EstaActiva);

        if (franjaBloquedaActiva is not null)
        {
            return HorarioResult.Denegado(
                $"Ahora estás en clase ({franjaBloquedaActiva.HoraInicio}–{franjaBloquedaActiva.HoraFin}). " +
                $"Puedes pedir fuera de ese horario.",
                franjaBloquedaActiva);
        }

        // No hay franja de bloqueo activa → pedidos permitidos
        return HorarioResult.Permitido("Pedidos disponibles.");
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
