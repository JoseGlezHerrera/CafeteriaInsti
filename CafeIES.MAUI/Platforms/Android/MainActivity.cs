using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

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
        // Firebase/FCM pendiente de configurar — ver Platforms/Android/google-services.json
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
    }
}
