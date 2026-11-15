namespace IngenIA365Canal.App;

/// <summary>La aplicación: una sola ventana con la página del HybridWebView.</summary>
public sealed class App(PaginaPrincipal pagina) : Application
{
    protected override Window CreateWindow(IActivationState? activationState) => new(pagina) { Title = "Canal del asociado" };

    /// <summary>App Link o Universal Link entrante (el retorno de la pasarela llega por WebAuthenticator; los demás, por aquí).</summary>
    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        pagina.AbrirEnlace(uri);
    }
}
