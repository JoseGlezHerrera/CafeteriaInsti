using Android.App;
using Android.Runtime;

namespace CafeIES.MAUI;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override void OnCreate()
    {
        base.OnCreate();
        // Firebase se inicializa automáticamente mediante google-services.json
        // al usar Plugin.Firebase.CloudMessaging
    }
}
