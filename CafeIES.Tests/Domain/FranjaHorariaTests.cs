using CafeIES.Shared.Models;

namespace CafeIES.Tests.Domain;

public class FranjaHorariaTests
{
    // ── EstaActiva — franja desactivada ───────────────────────────────────────

    [Fact]
    public void EstaActiva_FranjaDesactivada_RetornaFalse()
    {
        var franja = Franja(
            inicio: DateTime.Now.AddMinutes(-30),
            fin:    DateTime.Now.AddMinutes(30),
            activa: false);

        Assert.False(franja.EstaActiva);
    }

    // ── EstaActiva — ahora dentro del rango ───────────────────────────────────

    [Fact]
    public void EstaActiva_AhoraEntreMedio_RetornaTrue()
    {
        var franja = Franja(
            inicio: DateTime.Now.AddMinutes(-30),
            fin:    DateTime.Now.AddMinutes(30));

        Assert.True(franja.EstaActiva);
    }

    // ── EstaActiva — ahora en el límite exacto ────────────────────────────────

    [Fact]
    public void EstaActiva_ExactamenteEnElInicio_RetornaTrue()
    {
        // Redondear al minuto porque TimeOnly.Parse("HH:mm") pierde los segundos
        var ahora = DateTime.Now;
        var inicioMinuto = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0);
        var franja = Franja(
            inicio: inicioMinuto,
            fin:    inicioMinuto.AddMinutes(30));

        Assert.True(franja.EstaActiva);
    }

    // ── EstaActiva — ahora fuera del rango ────────────────────────────────────

    [Fact]
    public void EstaActiva_AntesDelInicio_RetornaFalse()
    {
        var franja = Franja(
            inicio: DateTime.Now.AddMinutes(30),
            fin:    DateTime.Now.AddMinutes(90));

        Assert.False(franja.EstaActiva);
    }

    [Fact]
    public void EstaActiva_DespuesDelFin_RetornaFalse()
    {
        var franja = Franja(
            inicio: DateTime.Now.AddMinutes(-90),
            fin:    DateTime.Now.AddMinutes(-30));

        Assert.False(franja.EstaActiva);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static FranjaHoraria Franja(DateTime inicio, DateTime fin, bool activa = true) =>
        new()
        {
            Id          = 1,
            Turno       = Turno.Manana,
            Descripcion = "Test",
            HoraInicio  = inicio.ToString("HH:mm"),
            HoraFin     = fin.ToString("HH:mm"),
            Activa      = activa
        };
}
