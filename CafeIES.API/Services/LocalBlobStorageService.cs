namespace CafeIES.API.Services;

/// <summary>
/// Almacena imágenes en el sistema de ficheros local (wwwroot/uploads/productos/).
/// Usada en desarrollo. NO apta para producción en Azure App Service (disco efímero).
/// </summary>
public class LocalBlobStorageService : IBlobStorageService
{
    private readonly ILogger<LocalBlobStorageService> _logger;

    public LocalBlobStorageService(ILogger<LocalBlobStorageService> logger)
        => _logger = logger;

    public async Task<string> SubirAsync(Stream stream, string fileName, string contentType)
    {
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "productos");
        Directory.CreateDirectory(uploadsDir);

        var filePath = Path.Combine(uploadsDir, fileName);
        using var file = File.Create(filePath);
        await stream.CopyToAsync(file);

        return $"/uploads/productos/{fileName}";
    }

    public Task EliminarAsync(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/")) return Task.CompletedTask;

        try
        {
            var uploadsRoot = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));
            var oldPath = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            if (oldPath.StartsWith(uploadsRoot) && File.Exists(oldPath))
                File.Delete(oldPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la imagen local {Url}.", url);
        }

        return Task.CompletedTask;
    }
}
