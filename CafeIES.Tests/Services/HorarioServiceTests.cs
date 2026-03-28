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
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.False(result.Puede);
        Assert.False(result.EsError);
    }

    // ── Sin franjas configuradas ──────────────────────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_SinFranjasParaTurno_RetornaDenegado()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        // No se añade ninguna franja para el turno Mañana
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.False(result.Puede);
        Assert.Contains("No hay franjas horarias", result.Mensaje);
    }

    [Fact]
    public async Task PuedePedirAhora_FranjasDesactivadas_RetornaDenegado()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaActiva(id: 1, activa: false));
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        // La franja existe pero está desactivada → sin franjas activas
        Assert.False(result.Puede);
    }

    // ── Franja activa ahora mismo ─────────────────────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_FranjaActiva_RetornaPermitido()
    {
        // FranjaActiva abarca ±60 min; cruza medianoche en las horas 23 y 0.
        if (DateTime.Now.Hour is 0 or 23) return;

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaActiva(id: 1));   // abarca ±60min desde ahora
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.True(result.Puede);
        Assert.NotNull(result.FranjaActual);
    }

    [Fact]
    public async Task PuedePedirAhora_FranjaActiva_MensajeContieneHoraFin()
    {
        // FranjaActiva abarca ±60 min; cruza medianoche en las horas 23 y 0.
        if (DateTime.Now.Hour is 0 or 23) return;

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        var franja = FranjaActiva(id: 1);
        db.FranjasHorarias.Add(franja);
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.Contains(franja.HoraFin, result.Mensaje);
    }

    // ── Sin franja activa pero hay una futura ─────────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_SinFranjaActivaConFranjaFutura_RetornaDenegadoConProxima()
    {
        // La franja futura empieza en +90 min; cruza medianoche si Hour >= 22 o == 0.
        // HorarioService usa TimeOnly y no detectaría la franja como próxima → falso negativo.
        if (DateTime.Now.Hour is 0 or 22 or 23) return;

        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaFutura(id: 1));   // empieza en +90min
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.False(result.Puede);
        Assert.NotNull(result.ProximaFranja);
    }

    // ── Sin franja activa y sin franjas futuras ───────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_SinFranjaActivaYSinFuturas_RetornaDenegado()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        db.FranjasHorarias.Add(FranjaPasada(id: 1));   // terminó hace rato
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.False(result.Puede);
        Assert.Null(result.ProximaFranja);
        Assert.Contains("No hay más franjas", result.Mensaje);
    }

    // ── Franja de otro turno no interfiere ────────────────────────────────────

    [Fact]
    public async Task PuedePedirAhora_FranjaActivaDeOtroTurno_NoAfecta()
    {
        using var db = DbContextFactory.Create();
        db.Usuarios.Add(AlumnoManana(id: 1));
        // Solo hay franja activa para el turno Tarde, no para Mañana
        db.FranjasHorarias.Add(FranjaActiva(id: 1, turno: Turno.Tarde));
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var result = await sut.PuedePedirAhoraAsync(1);

        Assert.False(result.Puede);
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
    private static FranjaHoraria FranjaActiva(int id, Turno turno = Turno.Manana, bool activa = true)
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
            Activa      = activa
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
}
