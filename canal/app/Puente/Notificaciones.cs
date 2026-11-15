namespace IngenIA365Canal.App.Puente;

/// <summary>
/// Feature 021, US11 (T139): las notificaciones del teléfono por FCM (Android directo, iOS por APNs) con Plugin.Firebase. Sólo se compila con
/// Firebase cuando el ambiente trae su configuración (propiedad <c>CanalConFirebase</c>); sin ella, no hay token y los avisos llegan por correo.
/// Al tocar una notificación, su <c>url</c> va a la página como un enlace.
/// </summary>
public static partial class Notificaciones
{
    /// <summary>Una notificación recibida con la app abierta (sus datos: title, body, url, kind).</summary>
    public static event Action<Dictionary<string, string>>? AlRecibir;

    internal static void Avisar(Dictionary<string, string> datos) => AlRecibir?.Invoke(datos);

#if CANAL_FIREBASE
    public static void Registrar(MauiAppBuilder builder)
    {
        builder.ConfigureLifecycleEvents(eventos =>
        {
#if ANDROID
            eventos.AddAndroid(a => a.OnCreate((actividad, _) => Plugin.Firebase.Core.Platforms.Android.CrossFirebase.Initialize(actividad)));
#elif IOS
            eventos.AddiOS(i => i.WillFinishLaunching((_, _) =>
            {
                Plugin.Firebase.Core.Platforms.iOS.CrossFirebase.Initialize();
                return false;
            }));
#endif
        });
        Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.NotificationReceived += (_, e) =>
            Avisar(new Dictionary<string, string>(e.Notification.Data ?? new Dictionary<string, string>())
            {
                ["title"] = e.Notification.Title ?? string.Empty,
                ["body"] = e.Notification.Body ?? string.Empty,
            });
        Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.NotificationTapped += (_, e) =>
        {
            if (e.Notification.Data is { } datos && datos.TryGetValue("url", out var url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
                Application.Current?.SendOnAppLinkRequestReceived(uri);
        };
    }

    public static async Task<string?> TokenAsync()
    {
        try
        {
            await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
            return await Plugin.Firebase.CloudMessaging.CrossFirebaseCloudMessaging.Current.GetTokenAsync();
        }
        catch (Exception)
        {
            return null;
        }
    }
#else
    public static Task<string?> TokenAsync() => Task.FromResult<string?>(null);
#endif
}
