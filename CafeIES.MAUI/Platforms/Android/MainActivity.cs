using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace CafeIES.MAUI;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Edge-to-edge: la app ocupa toda la pantalla, incluyendo bajo la barra de estado
        WindowCompat.SetDecorFitsSystemWindows(Window!, false);

        if (Window != null)
        {
            // Barra de estado y de navegación transparentes para que el fondo de la app se vea
            Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
            Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);

            // Iconos de la barra de estado en claro (adecuado para fondo oscuro de CaféIES)
            var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
            insetsController.AppearanceLightStatusBars = false;
        }
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
    }
}
