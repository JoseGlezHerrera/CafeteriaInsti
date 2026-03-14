using System.ComponentModel.DataAnnotations;
using CafeIES.Shared.Validation;

namespace CafeIES.Tests.Validation;

public class PasswordComplexityAttributeTests
{
    private readonly PasswordComplexityAttribute _sut = new();

    // ── Contraseñas válidas ───────────────────────────────────────────────────

    [Theory]
    [InlineData("Password1!")]
    [InlineData("Segura#99")]
    [InlineData("C0ntraseña$")]
    [InlineData("A1!bbbbbbb")]
    public void Password_Valida_RetornaSuccess(string password)
    {
        var result = Validar(password);
        Assert.Equal(ValidationResult.Success, result);
    }

    // ── Sin mayúscula ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("password1!")]
    [InlineData("contraseña1!")]
    public void Password_SinMayuscula_RetornaError(string password)
    {
        var result = Validar(password);
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("mayúscula", result!.ErrorMessage);
    }

    // ── Sin dígito ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Password!")]
    [InlineData("Contraseña!")]
    public void Password_SinDigito_RetornaError(string password)
    {
        var result = Validar(password);
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("número", result!.ErrorMessage);
    }

    // ── Sin símbolo ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Password1")]
    [InlineData("Contraseña1")]
    public void Password_SinSimbolo_RetornaError(string password)
    {
        var result = Validar(password);
        Assert.NotEqual(ValidationResult.Success, result);
        Assert.Contains("símbolo", result!.ErrorMessage);
    }

    // ── Faltan múltiples requisitos ───────────────────────────────────────────

    [Fact]
    public void Password_SoloMinusculas_ErrorMencionaLosTreeRequisitos()
    {
        var result = Validar("solominusculas");
        Assert.NotNull(result);
        Assert.Contains("mayúscula", result!.ErrorMessage);
        Assert.Contains("número",   result.ErrorMessage);
        Assert.Contains("símbolo",  result.ErrorMessage);
    }

    [Fact]
    public void Password_SoloMayusculas_ErrorMencionaDigitoYSimbolo()
    {
        var result = Validar("SOLOMAYUSCULAS");
        Assert.NotNull(result);
        Assert.Contains("número",  result!.ErrorMessage);
        Assert.Contains("símbolo", result.ErrorMessage);
    }

    // ── Null / vacío — se delega a [Required], el atributo no falla ──────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Password_NullOVacia_RetornaSuccess(string? password)
    {
        // PasswordComplexityAttribute solo valida contenido; la presencia la valida [Required]
        var result = Validar(password);
        Assert.Equal(ValidationResult.Success, result);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private ValidationResult? Validar(string? value)
    {
        var ctx = new ValidationContext(new object());
        return _sut.GetValidationResult(value, ctx);
    }
}
