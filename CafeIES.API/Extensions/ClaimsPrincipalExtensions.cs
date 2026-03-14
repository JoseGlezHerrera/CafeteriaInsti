using System.Security.Claims;

namespace CafeIES.API.Extensions;

/// <summary>
/// Extensiones seguras para extraer claims del usuario autenticado.
/// Evitan NullReferenceException si el claim no está presente en el token.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Devuelve el userId del token JWT, o null si el claim no existe o no es un entero válido.
    /// </summary>
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }
}
