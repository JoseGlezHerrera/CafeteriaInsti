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
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Syne-Bold.ttf", "SyneBold");
                fonts.AddFont("Syne-Regular.ttf", "Syne");
                fonts.AddFont("DMSans-Regular.ttf", "DMSans");
                fonts.AddFont("DMSans-Medium.ttf", "DMSansMedium");
            });

        // ── HTTP Client ───────────────────────────────────────────────────────
        // 10.0.2.2 es el alias de localhost desde el emulador Android
#if ANDROID
        var apiBase = "https://10.0.2.2:50658/";
#else
        var apiBase = "https://localhost:50658/";
#endif
        builder.Services.AddHttpClient<ApiService>(client =>
        {
            client.BaseAddress = new Uri(apiBase);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Acepta el certificado de desarrollo local
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });

        // ── Servicios ─────────────────────────────────────────────────────────
        builder.Services.AddSingleton<TokenService>();
        builder.Services.AddSingleton<CarritoViewModel>();

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