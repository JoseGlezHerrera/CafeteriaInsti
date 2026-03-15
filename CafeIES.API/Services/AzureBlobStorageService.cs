using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace CafeIES.API.Services;

/// <summary>
/// Almacena imágenes en Azure Blob Storage (contenedor "productos").
/// Usada en producción cuando AzureStorage:ConnectionString está configurado.
/// El contenedor debe tener acceso público de lectura (blob) para servir las imágenes.
/// </summary>
public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;
    private readonly ILogger<AzureBlobStorageService> _logger;

    private const string ContainerName = "productos";

    public AzureBlobStorageService(IConfiguration config, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var connStr = config["AzureStorage:ConnectionString"]!;
        var serviceClient = new BlobServiceClient(connStr);
        _container = serviceClient.GetBlobContainerClient(ContainerName);
    }

    public async Task<string> SubirAsync(Stream stream, string fileName, string contentType)
    {
        // Crea el contenedor si no existe y lo hace público de lectura (blobs)
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = _container.GetBlobClient(fileName);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        };
        await blobClient.UploadAsync(stream, uploadOptions);

        return blobClient.Uri.ToString();
    }

    public async Task EliminarAsync(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            // La URL tiene el formato: https://{account}.blob.core.windows.net/productos/{fileName}
            var uri = new Uri(url);
            var fileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrEmpty(fileName))
                await _container.DeleteBlobIfExistsAsync(fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el blob {Url}.", url);
        }
    }
}
