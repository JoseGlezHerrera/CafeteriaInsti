namespace CafeIES.API.Services;

/// <summary>
/// Abstracción para almacenamiento de imágenes de productos.
/// Tiene dos implementaciones:
///  • LocalBlobStorageService  — guarda en wwwroot/uploads/ (desarrollo)
///  • AzureBlobStorageService  — sube a Azure Blob Storage (producción)
/// Se registra automáticamente según si AzureStorage:ConnectionString está configurado.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>Sube una imagen y devuelve la URL pública.</summary>
    Task<string> SubirAsync(Stream stream, string fileName, string contentType);

    /// <summary>
    /// Elimina la imagen anterior. No lanza excepciones (operación no crítica).
    /// </summary>
    Task EliminarAsync(string? url);
}
