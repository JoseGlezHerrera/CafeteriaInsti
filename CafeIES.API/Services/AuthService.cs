using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CafeIES.Shared.Models;
using Microsoft.IdentityModel.Tokens;

namespace CafeIES.API.Services;

public class AuthService
{
    private readonly IConfiguration _config;

    public AuthService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>Genera un JWT de acceso (corta duración: 1h)</summary>
    public string GenerarAccessToken(Usuario usuario)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email,          usuario.Email),
            new Claim(ClaimTypes.Name,           usuario.NombreCompleto),
            new Claim(ClaimTypes.Role,           usuario.Rol.ToString()),
            new Claim("turno",                   usuario.Turno?.ToString() ?? ""),
            new Claim("estado",                  usuario.Estado.ToString())
        };

        var token = new JwtSecurityToken(
            issuer:   _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims:   claims,
            expires:  DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Genera un Refresh Token seguro (larga duración: 30 días)</summary>
    public string GenerarRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Verifica contraseña contra el hash almacenado</summary>
    public bool VerificarPassword(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);

    /// <summary>Genera hash bcrypt de una contraseña</summary>
    public string HashPassword(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
}
