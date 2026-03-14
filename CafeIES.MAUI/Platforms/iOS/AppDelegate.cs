using Foundation;
using Plugin.Firebase.CloudMessaging;
using UIKit;
using UserNotifications;

namespace CafeIES.MAUI;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        // Solicitar permiso de notificaciones al sistema iOS
        UNUserNotificationCenter.Current.RequestAuthorization(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound,
            (granted, error) =>
            {
                if (granted)
                    MainThread.BeginInvokeOnMainThread(() =>
                        UIApplication.SharedApplication.RegisterForRemoteNotifications());
            });

        return base.FinishedLaunching(application, launchOptions);
    }

    public override void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
        => FirebaseCloudMessagingImplementation.OnRegistered(deviceToken);

    public override void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
        => FirebaseCloudMessagingImplementation.OnFailedToRegister(error);

    public override void DidReceiveRemoteNotification(UIApplication application,
        NSDictionary userInfo, Action<UIBackgroundFetchResult> completionHandler)
    {
        FirebaseCloudMessagingImplementation.OnNotificationReceived(userInfo);
        completionHandler(UIBackgroundFetchResult.NewData);
    }
}
