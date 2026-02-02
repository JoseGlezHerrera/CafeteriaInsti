// MauiProgram.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CafeteriaInsti.ViewModels;
using CafeteriaInsti.Views;
using Microsoft.Extensions.Logging;

namespace CafeteriaInsti;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // --- REGISTRO DE SERVICIOS ---
        // Registramos los servicios como Singleton para que solo haya una instancia en toda la app
        builder.Services.AddSingleton<ProductoService>();
        builder.Services.AddSingleton<CarritoService>();
        builder.Services.AddSingleton<FavoritosService>();
        builder.Services.AddSingleton<UserService>();
        builder.Services.AddSingleton<SessionService>();

        // AppShell como Singleton para acceso global
        builder.Services.AddSingleton<AppShell>();

        // --- REGISTRO DE VIEWMODELS ---
        // Registramos los ViewModels como Transient para que se cree una nueva instancia cada vez que navegamos a la página
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<EditProfileViewModel>();
        builder.Services.AddTransient<ListaProductosViewModel>();
        builder.Services.AddTransient<DetalleProductoViewModel>();
        builder.Services.AddTransient<CarritoViewModel>();
        builder.Services.AddTransient<ConfirmacionPedidoViewModel>();

        // --- REGISTRO DE PÁGINAS (VIEWS) ---
        // Registramos las páginas para que la inyección de dependencias pueda inyectar los ViewModels en sus constructores
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<EditProfilePage>();
        builder.Services.AddTransient<ListaProductosPage>();
        builder.Services.AddTransient<DetalleProductoPage>();
        builder.Services.AddTransient<CarritoPage>();
        builder.Services.AddTransient<ConfirmacionPedidoPage>();

        return builder.Build();
    }
}