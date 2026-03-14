using CafeIES.Shared.Models;

namespace CafeIES.Tests.Domain;

public class InvitacionTests
{
    // ── EsValida — casos inválidos ────────────────────────────────────────────

    [Fact]
    public void EsValida_Inactiva_RetornaFalse()
    {
        var inv = BuildInvitacion(activa: false);
        Assert.False(inv.EsValida);
    }

    [Fact]
    public void EsValida_Expirada_RetornaFalse()
    {
        var inv = BuildInvitacion(expiracion: DateTime.Now.AddDays(-1));
        Assert.False(inv.EsValida);
    }

    [Fact]
    public void EsValida_UsosAgotados_RetornaFalse()
    {
        var inv = BuildInvitacion(usosMaximos: 3, usosActuales: 3);
        Assert.False(inv.EsValida);
    }

    [Fact]
    public void EsValida_UsosExcedidos_RetornaFalse()
    {
        var inv = BuildInvitacion(usosMaximos: 3, usosActuales: 5);
        Assert.False(inv.EsValida);
    }

    // ── EsValida — casos válidos ──────────────────────────────────────────────

    [Fact]
    public void EsValida_ActivaNoExpiradaYUsosDisponibles_RetornaTrue()
    {
        var inv = BuildInvitacion(usosMaximos: 10, usosActuales: 5);
        Assert.True(inv.EsValida);
    }

    [Fact]
    public void EsValida_UsosMaximosNull_RetornaTrue()
    {
        // Sin límite de usos: siempre válida mientras esté activa y no expirada
        var inv = BuildInvitacion(usosMaximos: null, usosActuales: 999);
        Assert.True(inv.EsValida);
    }

    [Fact]
    public void EsValida_PrimerUso_RetornaTrue()
    {
        var inv = BuildInvitacion(usosMaximos: 1, usosActuales: 0);
        Assert.True(inv.EsValida);
    }

    [Fact]
    public void EsValida_ExpiraccionExactaAhora_RetornaTrue()
    {
        // La comparación es <=, así que el instante exacto todavía es válido
        var inv = BuildInvitacion(expiracion: DateTime.Now.AddSeconds(1));
        Assert.True(inv.EsValida);
    }

    // ── UrlInvitacion ─────────────────────────────────────────────────────────

    [Fact]
    public void UrlInvitacion_ContieneToken()
    {
        var inv = BuildInvitacion();
        Assert.Contains(inv.Token, inv.UrlInvitacion);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static Invitacion BuildInvitacion(
        bool activa = true,
        DateTime? expiracion = null,
        int? usosMaximos = null,
        int usosActuales = 0) => new()
    {
        Id              = 1,
        Token           = Guid.NewGuid().ToString("N"),
        Tipo            = TipoInvitacion.Profesor,
        Activa          = activa,
        FechaExpiracion = expiracion ?? DateTime.Now.AddDays(7),
        UsosMaximos     = usosMaximos,
        UsosActuales    = usosActuales
    };
}
