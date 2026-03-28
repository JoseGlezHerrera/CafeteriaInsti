using CafeIES.Shared.Models;

namespace CafeIES.API.Data;

/// <summary>
/// Crea la cuenta de administrador inicial si no existe ninguna.
/// Se ejecuta en el arranque de la API una sola vez.
/// Las credenciales se leen de appsettings / variables de entorno.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAdminAsync(AppDbContext db, IConfiguration config, IWebHostEnvironment? env = null)
    {
        // Si ya hay algún admin, no hacemos nada
        if (db.Usuarios.Any(u => u.Rol == RolUsuario.Admin)) return;

        var adminEmail    = config["Admin:Email"]    ?? "admin@cafeies.local";
        var adminPassword = config["Admin:Password"] ?? "Admin1234!";
        var adminNombre   = config["Admin:Nombre"]   ?? "Administrador";

        // FIX-22: En producción, no arrancar con credenciales por defecto
        if (env?.IsProduction() == true &&
            (string.IsNullOrEmpty(config["Admin:Email"]) || string.IsNullOrEmpty(config["Admin:Password"])))
        {
            throw new InvalidOperationException(
                "SEC-06: No se puede arrancar en producción sin configurar Admin:Email y Admin:Password. " +
                "Configúralos como variables de entorno o en appsettings.Production.json.");
        }

        var admin = new Usuario
        {
            NombreCompleto = adminNombre,
            Email          = adminEmail,
            PasswordHash   = BCrypt.Net.BCrypt.HashPassword(adminPassword),
            Rol            = RolUsuario.Admin,
            Turno          = null,  // Sin restricción horaria
            Estado         = EstadoCuenta.Activa,
            FechaRegistro  = DateTime.UtcNow,
            FechaValidacion = DateTime.UtcNow
        };

        db.Usuarios.Add(admin);
        await db.SaveChangesAsync();

        Console.WriteLine("──────────────────────────────────────────");
        Console.WriteLine("  ✅ Admin creado. Usa las credenciales de Admin:Email / Admin:Password.");
        Console.WriteLine("──────────────────────────────────────────");
    }
}
