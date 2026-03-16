using CafeIES.MAUI.Services;
using CafeIES.MAUI.ViewModels;
using CafeIES.MAUI.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace CafeIES.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            // Firebase se inicializa automáticamente desde google-services.json (Android) /
            // GoogleService-Info.plist (iOS) — Plugin.Firebase.CloudMessaging 3.1.0 no
            // necesita llamada explícita a UseFirebase() en el builder de MAUI.
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Syne-Bold.ttf", "SyneBold");
                fonts.AddFont("Syne-Regular.ttf", "Syne");
                fonts.AddFont("DMSans-Regular.ttf", "DMSans");
                fonts.AddFont("DMSans-Medium.ttf", "DMSansMedium");
            });

        // â”€â”€ HTTP Client â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
#if DEBUG
        // Desarrollo: 10.0.2.2 es localhost visto desde el emulador Android
#if ANDROID
        var apiBase = "https://10.0.2.2:50658/";
#else
        var apiBase = "https://localhost:50658/";
#endif
#else
        // ProducciÃ³n: URL de la API desplegada en Azure App Service
        // REEMPLAZAR con la URL real tras el despliegue
        var apiBase = "https://cafeies-api.azurewebsites.net/";
#endif
        builder.Services.AddSingleton(sp =>
        {
            var handler = new HttpClientHandler();
#if DEBUG
            // Solo en desarrollo: aceptar certificados autofirmados de localhost.
            // ELIMINAR esta lÃ­nea antes de publicar en producciÃ³n.
            handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
#endif
            var http = new HttpClient(handler)
            {
                BaseAddress = new Uri(apiBase),
                Timeout     = TimeSpan.FromSeconds(15)
            };
            var logger = sp.GetRequiredService<ILogger<ApiService>>();
            return new ApiService(http, sp.GetRequiredService<TokenService>(), logger);
        });

        // â”€â”€ Servicios â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        builder.Services.AddSingleton<TokenService>();
        builder.Services.AddSingleton<CarritoViewModel>();
        builder.Services.AddSingleton<PushNotificationService>();

        // â”€â”€ ViewModels â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegistroViewModel>();
        builder.Services.AddTransient<RegistroInvitacionViewModel>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<PedidosViewModel>();
        builder.Services.AddTransient<DetallePedidoViewModel>();
        builder.Services.AddTransient<PerfilViewModel>();
        builder.Services.AddTransient<AdminPedidosViewModel>();
        builder.Services.AddTransient<AdminUsuariosViewModel>();
        builder.Services.AddTransient<AdminProductosViewModel>();
        builder.Services.AddTransient<AdminEditProductoViewModel>();

        // â”€â”€ PÃ¡ginas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegistroPage>();
        builder.Services.AddTransient<RegistroInvitacionPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<CarritoPage>();
        builder.Services.AddTransient<PedidosPage>();
        builder.Services.AddTransient<PerfilPage>();
        builder.Services.AddTransient<DetallePedidoPage>();
        builder.Services.AddTransient<ConfirmacionPedidoPage>();
        builder.Services.AddTransient<AdminPedidosPage>();
        builder.Services.AddTransient<AdminUsuariosPage>();
        builder.Services.AddTransient<AdminProductosPage>();
        builder.Services.AddTransient<AdminEditProductoPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

