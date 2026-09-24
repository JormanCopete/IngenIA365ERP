namespace IngenIA365ERP.Shared.Services.Adjuntos;

/// <summary>
/// Feature 011: los enlaces que firma el servidor. S3 firma URLs absolutas; el almacén local de
/// desarrollo firma rutas relativas a la API (<c>/api/attachments/local-blob/…</c>). El navegador necesita
/// la absoluta, y la base es la de la API, no la de la página: en desarrollo son orígenes distintos.
/// </summary>
public static class EnlacesFirmados
{
    public static string Absoluta(HttpClient http, string url) =>
        // En Windows, Uri toma «/api/…» como un archivo absoluto: por eso se exige http o https.
        Uri.TryCreate(url, UriKind.Absolute, out var absoluta) && absoluta.Scheme is "http" or "https" || http.BaseAddress is null
            ? url
            : new Uri(http.BaseAddress, url).ToString();
}
