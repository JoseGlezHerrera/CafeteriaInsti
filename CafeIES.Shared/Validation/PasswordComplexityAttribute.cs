using System.ComponentModel.DataAnnotations;

namespace CafeIES.Shared.Validation;

/// <summary>
/// Valida que la contraseña contenga al menos una mayúscula, un número y un símbolo.
/// Úsalo junto a [MinLength(8)] en los DTOs de registro y cambio de contraseña.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class PasswordComplexityAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var pwd = value as string;
        if (string.IsNullOrEmpty(pwd)) return ValidationResult.Success; // [MinLength] lo cubre

        var faltan = new List<string>();
        if (!pwd.Any(char.IsUpper))                  faltan.Add("una mayúscula");
        if (!pwd.Any(char.IsDigit))                  faltan.Add("un número");
        if (!pwd.Any(c => !char.IsLetterOrDigit(c))) faltan.Add("un símbolo (!@#$%...)");

        return faltan.Count == 0
            ? ValidationResult.Success
            : new ValidationResult(
                $"La contraseña debe incluir al menos {string.Join(", ", faltan)}.");
    }
}
