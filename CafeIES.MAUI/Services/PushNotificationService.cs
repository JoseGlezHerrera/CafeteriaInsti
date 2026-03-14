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
#if ANDROID || IOS
        try
        {
            await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current
                .CheckIfValidAsync();

            var token = await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current
                .GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("Token FCM vacío — Firebase probablemente no configurado.");
                return;
            }

#if ANDROID
            var plataforma = "android";
#else
            var plataforma = "ios";
#endif
            await _api.RegistrarTokenPushAsync(token, plataforma);
            _logger.LogInformation("Token FCM registrado en la API (...{Suffix}).",
                token.Length > 8 ? token[^8..] : token);
        }
        catch (Exception ex)
        {
            // No bloquear el login aunque falle el registro de push
            _logger.LogWarning(ex, "No se pudo registrar el token FCM.");
        }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Elimina el token FCM de la API al cerrar sesión para evitar notificaciones huérfanas.
    /// </summary>
    public async Task EliminarAsync()
    {
#if ANDROID || IOS
        try
        {
            var token = await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current
                .GetTokenAsync();

            if (!string.IsNullOrEmpty(token))
                await _api.EliminarTokenPushAsync(token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo eliminar el token FCM.");
        }
#else
        await Task.CompletedTask;
#endif
    }
}
