using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI.Services;

/// <summary>
/// FIX-26: Gestiona el ciclo de vida del token FCM en el dispositivo.
///
/// Para activar las notificaciones push:
///
/// 1. FIREBASE SETUP:
///    a) Crear proyecto en Firebase Console (https://console.firebase.google.com)
///    b) Registrar la app Android con el package name (com.cafeies.app)
///    c) Descargar google-services.json → Platforms/Android/ (build action: GoogleServicesJson)
///    d) Para iOS: descargar GoogleService-Info.plist → Platforms/iOS/ (build action: BundleResource)
///       + configurar APNs en Apple Developer Portal y en Firebase Console
///
/// 2. PAQUETE NUGET:
///    Descomentar en CafeIES.MAUI.csproj:
///    &lt;PackageReference Include="Plugin.Firebase.CloudMessaging" Version="3.0.0" /&gt;
///
/// 3. CÓDIGO:
///    Descomentar las líneas marcadas con "// FCM:" en RegistrarAsync() y EliminarAsync()
///
/// 4. API:
///    La API ya tiene los endpoints POST/DELETE en /api/notificaciones/token
///    y envía push en PedidosController al marcar pedido como Listo.
/// </summary>
public class PushNotificationService
{
    private readonly ApiService            _api;
    private readonly ILogger<PushNotificationService> _logger;
    private string? _currentToken;

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
        try
        {
            // FCM: Descomentar cuando google-services.json esté configurado:
            // await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
            // var token = await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            // _currentToken = token;
            // await _api.RegistrarTokenPushAsync(token, DeviceInfo.Platform == DevicePlatform.Android ? "android" : "ios");
            // _logger.LogInformation("Token FCM registrado: {Token}", token[..20] + "...");

            _logger.LogDebug("Push notifications no disponibles — Firebase sin configurar. " +
                "Consulta los comentarios en PushNotificationService.cs para activarlas.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al registrar token FCM.");
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Elimina el token FCM de la API al cerrar sesión para evitar notificaciones huérfanas.
    /// </summary>
    public async Task EliminarAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_currentToken))
            {
                // FCM: Descomentar cuando Firebase esté configurado:
                // await _api.EliminarTokenPushAsync(_currentToken);
                // _currentToken = null;
                _logger.LogDebug("Token FCM no eliminado — Firebase sin configurar.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al eliminar token FCM.");
        }
        await Task.CompletedTask;
    }
}
