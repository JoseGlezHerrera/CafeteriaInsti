namespace CafeIES.API.Services;

public class LocalBlobStorageService : IBlobStorageService
{
    private readonly string _baseDir;
    private readonly string _baseUrl;
    private readonly ILogger<LocalBlobStorageService> _logger;

    public LocalBlobStorageService(IConfiguration config, ILogger<LocalBlobStorageService> logger)
    {
        _logger = logger;
        _baseDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "productos");
        _baseUrl = config["LocalStorage:BaseUrl"] ?? "http://proyectos2dam.duckdns.org:5000";
        Directory.CreateDirectory(_baseDir);
    }

    public async Task<string> SubirAsync(Stream stream, string fileName, string contentType)
    {
        var filePath = Path.Combine(_baseDir, fileName);
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fs);
        return $"{_baseUrl}/productos/{fileName}";
    }

    public Task EliminarAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return Task.CompletedTask;
        try
        {
            var fileName = Path.GetFileName(new Uri(url).LocalPath);
            var filePath = Path.Combine(_baseDir, fileName);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar {Url}.", url);
        }
        return Task.CompletedTask;
    }
}
