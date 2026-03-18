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
        var uploadsDir = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "productos"));
        Directory.CreateDirectory(uploadsDir);

        // Solo usar el nombre de fichero puro; eliminar cualquier componente de directorio
        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            _logger.LogWarning("Nombre de fichero inválido o vacío tras sanitizar: '{FileName}'.", fileName);
            throw new InvalidOperationException("Nombre de fichero inválido.");
        }

        var candidatePath = Path.GetFullPath(Path.Combine(uploadsDir, safeFileName));
        var relative = Path.GetRelativePath(uploadsDir, candidatePath);

        // Si la ruta relativa sube hacia el padre (..) o es absoluta → path traversal
        if (relative.StartsWith("..") || Path.IsPathRooted(relative))
        {
            _logger.LogWarning("Intento de path traversal detectado al subir fichero: '{FileName}'.", fileName);
            throw new InvalidOperationException("Ruta de fichero no permitida.");
        }

        using var file = File.Create(candidatePath);
        await stream.CopyToAsync(file);

        return $"/uploads/productos/{safeFileName}";
    }

    public Task EliminarAsync(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/")) return Task.CompletedTask;

        try
        {
            var uploadsRoot = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"));
            var candidatePath = Path.GetFullPath(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            var relative = Path.GetRelativePath(uploadsRoot, candidatePath);

            // Prevenir path traversal: la ruta resultante no debe subir al directorio padre
            if (relative.StartsWith("..") || Path.IsPathRooted(relative))
            {
                _logger.LogWarning("Intento de path traversal detectado al eliminar: '{Url}'.", url);
                return Task.CompletedTask;
            }

            if (File.Exists(candidatePath))
                File.Delete(candidatePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar la imagen local {Url}.", url);
        }

        return Task.CompletedTask;
    }
}
