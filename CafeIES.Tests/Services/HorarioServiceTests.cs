using CafeIES.API.Data;
using CafeIES.API.Services;
using CafeIES.Shared.Models;
using CafeIES.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;

namespace CafeIES.Tests.Services;

public class HorarioServiceTests
{
    // ── Usuario no encontrado ─────────────────────────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_UsuarioNoExiste_RetornaError()
    {
        using var db  = DbContextFactory.Create();
        var sut = CreateService(db);

        var result = await sut.PuedePedirAhoraAsync(9999);

        Assert.True(result.EsError);
        Assert.False(result.Puede);
    }

    // ── Roles sin restricción ─────────────────────────────────────────────────

    [Theory]
    [InlineData(RolUsuario.Admin)]
    [InlineData(RolUsuario.Profesor)]
    [InlineData(RolUsuario.Personal)]
    public async Task PuedePedirAhora_RolNoAlumno_SiemprePermitido(RolUsuario rol)
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(new Usuario
        {
            Id = 1, Email = "staff@ies.es", NombreCompleto = "Staff",
            PasswordHash = "x", Rol = rol, Estado = EstadoCuenta.Activa
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    // ── Alumno sin turno asignado ─────────────────────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_AlumnoSinTurno_RetornaDenegado()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(new Usuario
        {
            Id = 1, Email = "alumno@ies.es", NombreCompleto = "Alumno",
            PasswordHash = "x", Rol = RolUsuario.Alumno, Turno = null,
            Estado = EstadoCuenta.Activa
        });
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.False(result.Puede);
        Assert.False(result.EsError);
    }

    // ── Sin franjas configuradas ──────────────────────────────────────────────

    [Fact]
    // BUG-028: BUG-013 (Round 12) cambió el comportamiento: sin franjas activas → Permitido, no Denegado
    public async Task PuedePedirAhora_SinFranjasParaTurno_RetornaPermitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        // No se añade ninguna franja para el turno Mañana
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.True(result.Puede);   // sin franjas de bloqueo → permitido
        Assert.False(result.EsError);
        Assert.Contains("Sin franjas de bloqueo", result.Mensaje);
    }

    [Fact]
    // BUG-028: BUG-013 (Round 12) — franjas desactivadas equivalen a sin franjas → Permitido
    public async Task PuedePedirAhora_FranjasDesactivadas_RetornaPermitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaActiva(id: 1, activa: false));
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        // La franja existe pero está desactivada → sin franjas activas → permitido
        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    // ── Franja activa ahora mismo ─────────────────────────────────────────────

    // BUG-028: BUG-013 eliminó el modelo "ventana de permiso" (default-deny) por
    // "ventana de bloqueo" (default-allow). FranjaActiva ahora tiene EsBloqueada=false,
    // por lo que NO bloquea → el usuario puede pedir. FranjaActual ya no se establece.
    [Fact]
    public async Task PuedePedirAhora_FranjaNoBloqueoActiva_RetornaPermitido()
    {
        if (DateTime.Now.Hour is 0 or 23) return;

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaActiva(id: 1));   // EsBloqueada=false
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    [Fact]
    // BUG-028: mensaje ahora es "Pedidos disponibles." cuando hay franjas activas no bloqueantes
    public async Task PuedePedirAhora_FranjaNoBloqueoActiva_MensajePedidosDisponibles()
    {
        if (DateTime.Now.Hour is 0 or 23) return;

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaActiva(id: 1));
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.True(result.Puede);
        Assert.Contains("Pedidos disponibles", result.Mensaje);
    }

    // ── Franja de bloqueo activa → bloqueado ─────────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_FranjaBloqueoActiva_RetornaDenegado()
    {
        if (DateTime.Now.Hour is 0 or 23) return;

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaActiva(id: 1, esBloqueada: true));  // bloquea pedidos
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.False(result.Puede);
        Assert.False(result.EsError);
    }

    // ── Franja no bloqueante → no afecta sin importar cuándo empiece/termine ─

    [Fact]
    // BUG-028: con modelo default-allow, una franja de otro turno no tiene franjas Mañana
    // → sin restricciones → Permitido (antes retornaba Denegado con modelo default-deny)
    public async Task PuedePedirAhora_FranjaDeOtroTurno_SinRestriccionParaTurnoActual()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        // Solo hay franja para Tarde, no para Mañana → sin franjas de bloqueo para Mañana
        db.FranjasHorarias.Add(FranjaActiva(id: 1, turno: Turno.Tarde));
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        // Sin franjas Mañana → sin bloqueo → permitido
        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    [Fact]
    // BUG-028: FranjaFutura y FranjaPasada no bloqueantes → Permitido
    public async Task PuedePedirAhora_FranjaNoBloqueoFutura_RetornaPermitido()
    {
        if (DateTime.Now.Hour is 0 or 22 or 23) return;

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaFutura(id: 1));   // empieza en +90min, EsBloqueada=false
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.True(result.Puede);  // no bloquea aunque esté "fuera de ventana"
        Assert.False(result.EsError);
    }

    [Fact]
    public async Task PuedePedirAhora_FranjaNoBloqueoYaPasada_RetornaPermitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaPasada(id: 1));   // terminó hace 90 min, EsBloqueada=false
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HorarioService CreateService(AppDbContext db) =>
        new(db, new MemoryCache(new MemoryCacheOptions()));

    private static Usuario AlumnoManana(int id) => new()
    {
        Id = id, Email = $"alumno{id}@ies.es", NombreCompleto = "Alumno",
        PasswordHash = "x", Rol = RolUsuario.Alumno, Turno = Turno.Manana,
        Estado = EstadoCuenta.Activa
    };

    /// <summary>Franja horaria que abarca desde 60 minutos antes hasta 60 minutos después de ahora.</summary>
    private static FranjaHoraria FranjaActiva(int id, Turno turno = Turno.Manana, bool activa = true, bool esBloqueada = false)
    {
        var inicio = DateTime.Now.AddMinutes(-60);
        var fin    = DateTime.Now.AddMinutes(60);
        return new FranjaHoraria
        {
            Id          = id,
            Turno       = turno,
            Descripcion = "Test activa",
            HoraInicio  = inicio.ToString("HH:mm"),
            HoraFin     = fin.ToString("HH:mm"),
            Activa      = activa,
            EsBloqueada = esBloqueada
        };
    }

    /// <summary>Franja que empieza en 90 minutos (todavía no ha llegado).</summary>
    private static FranjaHoraria FranjaFutura(int id)
    {
        var inicio = DateTime.Now.AddMinutes(90);
        var fin    = DateTime.Now.AddMinutes(120);
        return new FranjaHoraria
        {
            Id          = id,
            Turno       = Turno.Manana,
            Descripcion = "Test futura",
            HoraInicio  = inicio.ToString("HH:mm"),
            HoraFin     = fin.ToString("HH:mm"),
            Activa      = true
        };
    }

    /// <summary>Franja que ya terminó (empezó y acabó hace 2 horas).</summary>
    private static FranjaHoraria FranjaPasada(int id)
    {
        var inicio = DateTime.Now.AddMinutes(-120);
        var fin    = DateTime.Now.AddMinutes(-90);
        return new FranjaHoraria
        {
            Id          = id,
            Turno       = Turno.Manana,
            Descripcion = "Test pasada",
            HoraInicio  = inicio.ToString("HH:mm"),
            HoraFin     = fin.ToString("HH:mm"),
            Activa      = true
        };
    }

    // ── Reglas de día de la semana ────────────────────────────────────────────

    // Viernes en hora España: 2026-04-10 10:00 UTC = 12:00 CEST (viernes)
    private static readonly DateTime ViernesUtc      = new(2026, 4, 10, 10, 0, 0, DateTimeKind.Utc);
    // Viernes noche: 2026-04-10 21:55 UTC = 23:55 CEST (viernes, 5 min antes de medianoche)
    private static readonly DateTime ViernesNocheUtc = new(2026, 4, 10, 21, 55, 0, DateTimeKind.Utc);
    // Sábado en hora España: 2026-04-11 10:00 UTC = 12:00 CEST (sábado)
    private static readonly DateTime SabadoUtc  = new(2026, 4, 11, 10, 0, 0, DateTimeKind.Utc);
    // Domingo en hora España: 2026-04-12 10:00 UTC = 12:00 CEST (domingo)
    private static readonly DateTime DomingoUtc = new(2026, 4, 12, 10, 0, 0, DateTimeKind.Utc);
    // Lunes en hora España:   2026-04-13 10:00 UTC = 12:00 CEST (lunes)
    private static readonly DateTime LunesUtc   = new(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PuedePedirAhora_Sabado_AlumnoDenegado()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        await db.SaveChangesAsync();

        var result = await CreateService(db).PuedePedirAhoraAsync(1, nowUtc: SabadoUtc);

        Assert.False(result.Puede);
        Assert.False(result.EsError);
        Assert.Contains("sábado", result.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PuedePedirAhora_Domingo_AlumnoPermitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        await db.SaveChangesAsync();

        var result = await CreateService(db).PuedePedirAhoraAsync(1, nowUtc: DomingoUtc);

        Assert.True(result.Puede);
        Assert.False(result.EsError);
        Assert.Contains("lunes", result.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PuedePedirAhora_Sabado_AdminSiemprePermitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(new Usuario
        {
            Id = 1, Email = "admin@ies.es", NombreCompleto = "Admin",
            PasswordHash = "x", Rol = RolUsuario.Admin, Estado = EstadoCuenta.Activa
        });
        await db.SaveChangesAsync();

        // Los admins no pasan por el check de día de la semana
        var result = await CreateService(db).PuedePedirAhoraAsync(1, nowUtc: SabadoUtc);

        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    [Fact]
    public async Task PuedePedirAhora_Lunes_AlumnoSinFranjas_Permitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        // Sin franjas → permisivo
        await db.SaveChangesAsync();

        var result = await CreateService(db).PuedePedirAhoraAsync(1, nowUtc: LunesUtc);

        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    // ── Viernes: día lectivo normal, no se bloquea como sábado ───────────────

    [Fact]
    // El viernes es un día lectivo normal → sigue la lógica de franjas.
    // Sin franjas configuradas → permisivo (igual que lunes–jueves).
    public async Task PuedePedirAhora_Viernes_AlumnoSinFranjas_Permitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        await db.SaveChangesAsync();

        var result = await CreateService(db).PuedePedirAhoraAsync(1, nowUtc: ViernesUtc);

        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    [Fact]
    // Viernes a las 23:55 hora España (5 min antes de que empiece el sábado bloqueado).
    // El código convierte a zona horaria España antes de comparar el día de la semana,
    // por lo que a las 23:55 CEST sigue siendo viernes → no debe bloquearse.
    public async Task PuedePedirAhora_ViernesNocheAntesDelSabado_AlumnoPermitido()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        await db.SaveChangesAsync();

        var result = await CreateService(db).PuedePedirAhoraAsync(1, nowUtc: ViernesNocheUtc);

        // Sigue siendo viernes en España → sigue lógica de franjas, sin franjas = permitido
        Assert.True(result.Puede);
        Assert.False(result.EsError);
    }

    [Fact]
    // Verifica la transición viernes→sábado: en cuanto pasa medianoche en España
    // el sábado queda bloqueado. SabadoUtc es 00:02 CEST del sábado 11/04/2026.
    public async Task PuedePedirAhora_SabadoMedianoche_AlumnoDenegado()
    {
        // 2026-04-10 22:02 UTC = 2026-04-11 00:02 CEST → ya es sábado en España
        var sabadoMedianoche = new DateTime(2026, 4, 10, 22, 2, 0, DateTimeKind.Utc);

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        await db.SaveChangesAsync();

        var result = await CreateService(db).PuedePedirAhoraAsync(1, nowUtc: sabadoMedianoche);

        Assert.False(result.Puede);
        Assert.False(result.EsError);
        Assert.Contains("sábado", result.Mensaje, StringComparison.OrdinalIgnoreCase);
    }
}
