using Foundation;
using UIKit;
using UserNotifications;

namespace CafeIES.MAUI;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // TODO: Habilitar push notifications cuando se integre Firebase/APNs
        // UNUserNotificationCenter.Current.RequestAuthorization(...)

        return base.FinishedLaunching(application, launchOptions);
    }
}
