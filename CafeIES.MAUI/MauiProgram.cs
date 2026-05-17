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

        // ── HTTP Client ─────────────────────────────────────────────────────────────
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
        var apiBase = "http://proyectos2dam.duckdns.org:5000/";
#endif
        builder.Services.AddSingleton(sp =>
        {
            var handler = new HttpClientHandler();
#if !DEBUG
            handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
#endif
#if DEBUG
            // Solo en desarrollo: aceptar certificados autofirmados de localhost.
            // ELIMINAR esta línea antes de publicar en producción.
            handler.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
#endif
            var http = new HttpClient(handler)
            {
                BaseAddress = new Uri(apiBase),
                Timeout     = TimeSpan.FromSeconds(45)
            };
            var logger = sp.GetRequiredService<ILogger<ApiService>>();
            return new ApiService(http, sp.GetRequiredService<TokenService>(), logger);
        });

        // ── Servicios ───────────────────────────────────────────────────────────────
        builder.Services.AddSingleton<TokenService>();
        builder.Services.AddSingleton<CarritoViewModel>();
        builder.Services.AddSingleton<PushNotificationService>();
#if ANDROID
        builder.Services.AddSingleton<IPrintService, CafeIES.MAUI.Platforms.Android.AndroidPrintService>();
#else
        builder.Services.AddSingleton<IPrintService, NoOpPrintService>();
#endif
        builder.Services.AddSingleton<EscPosPrinterService>();

        // ── ViewModels ──────────────────────────────────────────────────────────────
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
        builder.Services.AddTransient<ProductoDetalleViewModel>();
        builder.Services.AddTransient<AdminHorariosViewModel>();
        builder.Services.AddTransient<AdminInvitacionesViewModel>();
        builder.Services.AddTransient<AdminIngredientesViewModel>();
        builder.Services.AddTransient<AdminCategoriasViewModel>();
        builder.Services.AddTransient<AdminAlergenosViewModel>();
        builder.Services.AddTransient<EmpleadoPedidosViewModel>();
        builder.Services.AddTransient<EmpleadoProductosViewModel>();

        // ── Páginas ─────────────────────────────────────────────────────────────────
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
        builder.Services.AddTransient<ProductoDetallePage>();
        builder.Services.AddTransient<AdminHorariosPage>();
        builder.Services.AddTransient<AdminInvitacionesPage>();
        builder.Services.AddTransient<AdminIngredientesPage>();
        builder.Services.AddTransient<AdminCategoriasPage>();
        builder.Services.AddTransient<AdminAlergenosPage>();
        builder.Services.AddTransient<AdminPerfilPage>();
        builder.Services.AddTransient<PagamentoWebPage>();
        builder.Services.AddTransient<EmpleadoPedidosPage>();
        builder.Services.AddTransient<EmpleadoProductosPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

