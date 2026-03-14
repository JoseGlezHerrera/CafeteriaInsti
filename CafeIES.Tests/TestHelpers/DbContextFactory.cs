using CafeIES.API.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeIES.Tests.TestHelpers;

/// <summary>
/// Crea instancias de AppDbContext usando la base de datos en memoria de EF Core.
/// Cada llamada genera un nombre único para aislar los tests entre sí.
/// </summary>
public static class DbContextFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
