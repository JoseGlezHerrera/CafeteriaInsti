using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.Services;

/// <summary>
/// Gestiona el ciclo de vida del token FCM en el dispositivo:
///  • Al iniciar sesión → obtiene el token y lo registra en la API.
///  • Al cerrar sesión  → elimina el token de la API.
///
/// Requisitos para activar las notificaciones push:
///   Android — añadir google-services.json en Platforms/Android/ (build action: GoogleServicesJson)
///   iOS     — añadir GoogleService-Info.plist en Platforms/iOS/ (build action: BundleResource)
///             + configurar APNs en el Apple Developer Portal y en Firebase Console.
/// </summary>
public class PushNotificationService
{
    private readonly ApiService            _api;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(ApiService api, ILogger<PushNotificationService> logger)
    {
        _api    = api;
        _logger = logger;
    }

    /// <summary>
    /// Solicita permiso de notificaciones, obtiene el token FCM y lo registra en la API.
    /// Llamar tras un login o registro exitoso.
    /// </summary>
    public async Task RegistrarAsync()
    {
        // Push notifications pendientes de configurar:
        // 1. Crear proyecto en Firebase Console y descargar google-services.json
        // 2. Sustituir Platforms/Android/google-services.json con el archivo real
        // 3. Descomentar Plugin.Firebase.CloudMessaging en CafeIES.MAUI.csproj
        // 4. Restaurar las llamadas a CrossFirebaseCloudMessaging en este servicio
        _logger.LogDebug("Push notifications no disponibles — Firebase sin configurar.");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Elimina el token FCM de la API al cerrar sesión para evitar notificaciones huérfanas.
    /// </summary>
    public async Task EliminarAsync()
    {
        await Task.CompletedTask;
    }
}
