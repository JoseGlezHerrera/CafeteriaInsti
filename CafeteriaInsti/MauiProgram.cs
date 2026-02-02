// MauiProgram.cs
using CafeteriaInsti.Models;
using CafeteriaInsti.Services;
using CafeteriaInsti.ViewModels;
using CafeteriaInsti.Views;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace CafeteriaInsti;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // ✅ CONFIGURAR CULTURA ESPAÑOLA (Euro €)
        var cultureInfo = new CultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        
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
        System.Diagnostics.Debug.WriteLine("[MAUI] Registrando servicios...");
        builder.Services.AddSingleton<ProductoService>();
        builder.Services.AddSingleton<CarritoService>();
        builder.Services.AddSingleton<FavoritosService>();
        builder.Services.AddSingleton<PedidoService>(); // ✅ NUEVO
        System.Diagnostics.Debug.WriteLine("[MAUI] Servicios registrados");

        // --- REGISTRO DE VIEWMODELS ---
        // Registramos los ViewModels como Transient para que se cree una nueva instancia cada vez que navegamos a la página
        System.Diagnostics.Debug.WriteLine("[MAUI] Registrando ViewModels...");
        builder.Services.AddTransient<ListaProductosViewModel>();
        builder.Services.AddTransient<DetalleProductoViewModel>();
        builder.Services.AddTransient<CarritoViewModel>();
        builder.Services.AddTransient<ConfirmacionPedidoViewModel>();
        builder.Services.AddTransient<HistorialPedidosViewModel>(); // ✅ NUEVO
        builder.Services.AddTransient<FavoritosViewModel>(); // ✅ NUEVO
        builder.Services.AddTransient<InformacionViewModel>(); // ✅ NUEVO
        System.Diagnostics.Debug.WriteLine("[MAUI] ViewModels registrados");

        // --- REGISTRO DE PÁGINAS (VIEWS) ---
        // Registramos las páginas para que la inyección de dependencias pueda inyectar los ViewModels en sus constructores
        System.Diagnostics.Debug.WriteLine("[MAUI] Registrando páginas...");
        builder.Services.AddTransient<ListaProductosPage>();
        builder.Services.AddTransient<DetalleProductoPage>();
        builder.Services.AddTransient<CarritoPage>();
        builder.Services.AddTransient<ConfirmacionPedidoPage>();
        builder.Services.AddTransient<HistorialPedidosPage>(); // ✅ NUEVO
        builder.Services.AddTransient<FavoritosPage>(); // ✅ NUEVO
        builder.Services.AddTransient<InformacionPage>(); // ✅ NUEVO
        System.Diagnostics.Debug.WriteLine("[MAUI] Páginas registradas");

        return builder.Build();
    }
}