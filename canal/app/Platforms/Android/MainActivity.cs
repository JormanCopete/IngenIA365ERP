using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace IngenIA365Canal.App;

/// <summary>
/// Feature 021, US11 (T138): la actividad principal. Atiende los App Links del canal (https://{host}/cooperativa…, /perfil…) verificados por
/// assetlinks.json (autoVerify); el retorno de la pasarela (/pagos/…) lo atiende <see cref="RetornoDeLaPasarelaActivity"/>.
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout
                           | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], AutoVerify = true,
    DataScheme = "https", DataHost = ConfiguracionDelCanal.Host, DataPathPrefix = "/cooperativa")]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], AutoVerify = true,
    DataScheme = "https", DataHost = ConfiguracionDelCanal.Host, DataPathPrefix = "/perfil")]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Enlazar(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Enlazar(intent);
    }

    private static void Enlazar(Intent? intent)
    {
        if (intent?.Action == Intent.ActionView && intent.DataString is { } dato && Uri.TryCreate(dato, UriKind.Absolute, out var uri))
            Microsoft.Maui.Controls.Application.Current?.SendOnAppLinkRequestReceived(uri);
    }
}

/// <summary>El retorno de la pasarela (/pagos/{id}/retorno): lo recibe WebAuthenticator y la app sigue donde estaba.</summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], AutoVerify = true,
    DataScheme = "https", DataHost = ConfiguracionDelCanal.Host, DataPathPrefix = "/pagos")]
public sealed class RetornoDeLaPasarelaActivity : WebAuthenticatorCallbackActivity;
