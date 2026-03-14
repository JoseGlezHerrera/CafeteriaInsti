using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CafeIES.API.Services;
using CafeIES.Shared.Models;
using Microsoft.Extensions.Configuration;

namespace CafeIES.Tests.Services;

public class AuthServiceTests
{
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]      = "clave-super-secreta-de-al-menos-32-caracteres-para-test",
                ["Jwt:Issuer"]   = "CafeIES.Test",
                ["Jwt:Audience"] = "CafeIES.Test"
            })
            .Build();

        _sut = new AuthService(config);
    }

    // ── GenerarAccessToken ────────────────────────────────────────────────────

    [Fact]
    public void GenerarAccessToken_RetornaTokenNoVacio()
    {
        var usuario = BuildUsuario();
        var token = _sut.GenerarAccessToken(usuario);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerarAccessToken_TokenContieneClaims()
    {
        var usuario = BuildUsuario(id: 42, email: "test@ies.es", rol: RolUsuario.Alumno);
        var tokenStr = _sut.GenerarAccessToken(usuario);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        Assert.Equal("42",            jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("test@ies.es",   jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Alumno",        jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("CafeIES.Test",  jwt.Issuer);
    }

    [Fact]
    public void GenerarAccessToken_ExpiraEn1Hora()
    {
        var usuario = BuildUsuario();
        var tokenStr = _sut.GenerarAccessToken(usuario);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        var margen = TimeSpan.FromSeconds(10);
        var esperado = DateTime.UtcNow.AddHours(1);
        Assert.InRange(jwt.ValidTo, esperado - margen, esperado + margen);
    }

    [Theory]
    [InlineData(RolUsuario.Admin)]
    [InlineData(RolUsuario.Alumno)]
    [InlineData(RolUsuario.Profesor)]
    [InlineData(RolUsuario.Personal)]
    public void GenerarAccessToken_InclueyeRolCorrecto(RolUsuario rol)
    {
        var usuario = BuildUsuario(rol: rol);
        var tokenStr = _sut.GenerarAccessToken(usuario);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        Assert.Equal(rol.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerarAccessToken_SinInstituto_ClaimInstitutoEsVacia()
    {
        var usuario = BuildUsuario(institutoId: null);
        var tokenStr = _sut.GenerarAccessToken(usuario);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        Assert.Equal("", jwt.Claims.First(c => c.Type == "institutoId").Value);
    }

    [Fact]
    public void GenerarAccessToken_ConInstituto_ClaimInstitutoEsCorrecta()
    {
        var usuario = BuildUsuario(institutoId: 3);
        var tokenStr = _sut.GenerarAccessToken(usuario);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenStr);

        Assert.Equal("3", jwt.Claims.First(c => c.Type == "institutoId").Value);
    }

    // ── GenerarRefreshToken ───────────────────────────────────────────────────

    [Fact]
    public void GenerarRefreshToken_RetornaStringNoVacio()
    {
        var token = _sut.GenerarRefreshToken();
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerarRefreshToken_Es88Caracteres()
    {
        // 64 bytes en Base64 = 88 caracteres (sin padding = exactamente 88)
        var token = _sut.GenerarRefreshToken();
        Assert.Equal(88, token.Length);
    }

    [Fact]
    public void GenerarRefreshToken_CadaTokenEsUnico()
    {
        var tokens = Enumerable.Range(0, 20).Select(_ => _sut.GenerarRefreshToken()).ToHashSet();
        Assert.Equal(20, tokens.Count);
    }

    // ── HashPassword / VerificarPassword ─────────────────────────────────────

    [Fact]
    public void HashPassword_GeneraHashDistintoAlOriginal()
    {
        var hash = _sut.HashPassword("Password1!");
        Assert.NotEqual("Password1!", hash);
    }

    [Fact]
    public void VerificarPassword_PasswordCorrecta_ReturnsTrue()
    {
        var hash = _sut.HashPassword("Password1!");
        Assert.True(_sut.VerificarPassword("Password1!", hash));
    }

    [Fact]
    public void VerificarPassword_PasswordIncorrecta_ReturnsFalse()
    {
        var hash = _sut.HashPassword("Password1!");
        Assert.False(_sut.VerificarPassword("WrongPassword!", hash));
    }

    [Fact]
    public void VerificarPassword_DiferenteMayusculas_ReturnsFalse()
    {
        var hash = _sut.HashPassword("Password1!");
        Assert.False(_sut.VerificarPassword("password1!", hash));
    }

    [Fact]
    public void HashPassword_MismaPasswordGeneraHashesDiferentes()
    {
        // BCrypt usa salt aleatorio — dos hashes del mismo input nunca son iguales
        var hash1 = _sut.HashPassword("Password1!");
        var hash2 = _sut.HashPassword("Password1!");
        Assert.NotEqual(hash1, hash2);
        // Pero ambos verifican correctamente
        Assert.True(_sut.VerificarPassword("Password1!", hash1));
        Assert.True(_sut.VerificarPassword("Password1!", hash2));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Usuario BuildUsuario(
        int id = 1,
        string email = "usuario@ies.es",
        RolUsuario rol = RolUsuario.Alumno,
        int? institutoId = 1) => new()
    {
        Id             = id,
        NombreCompleto = "Test Usuario",
        Email          = email,
        PasswordHash   = "x",
        Rol            = rol,
        Turno          = Turno.Manana,
        Estado         = EstadoCuenta.Activa,
        InstitutoId    = institutoId
    };
}
