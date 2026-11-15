using Foundation;
using UIKit;

namespace IngenIA365Canal.App;

[Register("AppDelegate")]
public sealed class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    /// <summary>Universal Links del canal (apple-app-site-association): /cooperativa, /perfil y el retorno de la pasarela.</summary>
    public override bool ContinueUserActivity(UIApplication application, NSUserActivity userActivity, UIApplicationRestorationHandler completionHandler)
    {
        if (userActivity.ActivityType == NSUserActivityType.BrowsingWeb && userActivity.WebPageUrl?.AbsoluteString is { } url
            && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (Microsoft.Maui.Authentication.WebAuthenticator.Default is { } && uri.AbsolutePath.StartsWith("/pagos/", StringComparison.Ordinal)
                && Platform.ContinueUserActivity(application, userActivity, completionHandler))
                return true;
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(uri);
            return true;
        }
        return base.ContinueUserActivity(application, userActivity, completionHandler);
    }
}
