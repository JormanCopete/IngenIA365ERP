using System.Text.Json;
using IngenIA365Canal.App.Puente;

namespace IngenIA365Canal.App;

/// <summary>
/// Feature 021, US11 (T135, T136): la PWA del canal empaquetada (Resources/Raw/wwwroot) en un HybridWebView, con el puente como objetivo de
/// <c>window.HybridWebView.InvokeDotNet</c>. Los avisos de C# a la página (un enlace entrante, una notificación) van a <c>window.canal</c>, que
/// los guarda en cola si React todavía no se suscribió.
/// </summary>
public sealed class PaginaPrincipal : ContentPage
{
    private readonly HybridWebView _vista;

    public PaginaPrincipal(PuenteDelCanal puente)
    {
        _vista = new HybridWebView { HybridRoot = "wwwroot", DefaultFile = "index.html" };
        _vista.SetInvokeJavaScriptTarget(puente);
        Content = _vista;
        Notificaciones.AlRecibir += datos => _ = AvisarAsync("alNotificacion", datos);
    }

    /// <summary>Lleva a la página un enlace del canal (sólo del host del canal; lo demás se ignora).</summary>
    public void AbrirEnlace(Uri uri)
    {
        if (!string.Equals(uri.Host, ConfiguracionDelCanal.Host, StringComparison.OrdinalIgnoreCase)) return;
        _ = AvisarAsync("alAbrirEnlace", uri.ToString());
    }

    private Task AvisarAsync(string gancho, object datos) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        var json = datos is string texto ? JsonSerializer.Serialize(texto, ContextoDelPuente.Default.String) : JsonSerializer.Serialize(datos, ContextoDelPuente.Default.DictionaryStringString);
        await _vista.EvaluateJavaScriptAsync($"window.canal && window.canal.{gancho}({json})");
    });
}
