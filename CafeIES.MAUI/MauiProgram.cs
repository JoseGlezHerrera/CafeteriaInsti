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
#if ANDROID || IOS
            .UseFirebase(firebase => firebase.UseCloudMessaging())
#endif
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Syne-Bold.ttf", "SyneBold");
                fonts.AddFont("Syne-Regular.ttf", "Syne");
                fonts.AddFont("DMSans-Regular.ttf", "DMSans");
                fonts.AddFont("DMSans-Medium.ttf", "DMSansMedium");
            });

        // ── HTTP Client ───────────────────────────────────────────────────────
#if DEBUG
        // Desarrollo: 10.0.2.2 es localhost visto desde el emulador Android
#if ANDROID
        var apiBase = "https://10.0.2.2:50658/";
#else
        var apiBase = "https://localhost:50658/";
#endif
#else
        // Producción: URL de la API desplegada en Azure App Service
        // REEMPLAZAR con la URL real tras el despliegue
        var apiBase = "https://cafeies-api.azurewebsites.net/";
#endif
        builder.Services.AddSingleton(sp =>
        {
            var handler = new HttpClientHandler();
#if DEBUG
            // Solo en desarrollo: aceptar certificados autofirmados de localhost.
            // ELIMINAR esta línea antes de publicar en producción.
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

        // ── Servicios ─────────────────────────────────────────────────────────
        builder.Services.AddSingleton<TokenService>();
        builder.Services.AddSingleton<CarritoViewModel>();
        builder.Services.AddSingleton<PushNotificationService>();

        // ── ViewModels ────────────────────────────────────────────────────────
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

        // ── Páginas ───────────────────────────────────────────────────────────
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
