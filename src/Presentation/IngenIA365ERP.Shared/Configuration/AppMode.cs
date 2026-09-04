using Microsoft.Extensions.Configuration;

namespace IngenIA365ERP.Shared.Configuration;

public enum AppEnvironment { Development, QA, Production }
public enum AppDataSource { Mock, Api }

public static class AppMode
{
    public static AppEnvironment Environment { get; private set; } = AppEnvironment.Development;
    public static AppDataSource DataSource { get; private set; } = AppDataSource.Mock;

    /// <summary>
    /// Base de la API, ya RESUELTA y absoluta. Nunca relativa: ver
    /// <see cref="ResolverBase"/> para por qué eso importa tanto.
    /// </summary>
    public static string ApiBaseUrl { get; private set; } = "http://localhost:5100";

    public static bool UseMock => DataSource == AppDataSource.Mock;
    public static string Tag => $"[{Environment}][{DataSource}]";

    /// <param name="origenDelNavegador">
    /// El origen desde el que se sirvió la aplicación, para poder resolver una
    /// base relativa. En WebAssembly es <c>HostEnvironment.BaseAddress</c>. En el
    /// host servidor y en MAUI no existe tal cosa, y ahí la configuración trae
    /// siempre una URL absoluta.
    /// </param>
    public static void Configure(IConfiguration configuration, string? origenDelNavegador = null)
    {
        var section = configuration.GetSection("AppMode");
        if (Enum.TryParse<AppEnvironment>(section["Environment"], true, out var env))
            Environment = env;
        if (Enum.TryParse<AppDataSource>(section["DataSource"], true, out var ds))
            DataSource = ds;

        var configurado = section["ApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configurado))
            ApiBaseUrl = ResolverBase(configurado, origenDelNavegador);

        // Sync legacy flag for code paths that still read AppSettings.UseMockServices
        AppSettings.UseMockServices = UseMock;
        AppSettings.ApiBaseUrl = ApiBaseUrl;
        AppSettings.LocalApiBaseUrl = ApiBaseUrl;
    }

    /// <summary>
    /// Convierte lo configurado en una base ABSOLUTA y SIN barra final.
    ///
    /// <para>
    /// Aquí se juntan dos trampas que costaron un ambiente entero, y las dos
    /// vienen de dejar el valor relativo <c>"/"</c> circulando por la aplicación:
    /// </para>
    ///
    /// <para>
    /// <b>1. El esquema, no la "absolutez".</b>
    /// <c>Uri.TryCreate("/", UriKind.Absolute, out _)</c> devuelve <b>false</b> en
    /// Windows pero <b>true</b> en el runtime del navegador, que es de tipo Unix y
    /// trata la barra inicial como una ruta absoluta de archivo: sale
    /// <c>file:///</c>. El <c>HttpClient</c> quedaba apuntando al sistema de
    /// archivos y el navegador rechazaba todo con «Not allowed to load local
    /// resource», que en pantalla se lee <c>TypeError: Failed to fetch</c>.
    /// </para>
    ///
    /// <para>
    /// <b>2. La barra final y la concatenación.</b> Diecinueve pantallas arman sus
    /// URLs como <c>$"{AppSettings.GetApiBaseUrl()}/api/…"</c>. Con la base en
    /// <c>"/"</c> eso produce <c>//api/…</c>, que es una URL <b>protocolo-relativa</b>:
    /// el navegador la resuelve como <c>https://api/…</c> y falla con
    /// <c>ERR_NAME_NOT_RESOLVED</c>. Por eso se devuelve absoluta y sin barra final:
    /// es la única forma que sirve a la vez para concatenar y para
    /// <c>HttpClient.BaseAddress</c>.
    /// </para>
    ///
    /// <para>
    /// La lección de fondo: el valor relativo es cómodo en el archivo de
    /// configuración —permite que el mismo wwwroot sirva en cualquier dominio— pero
    /// no puede salir de aquí. Se resuelve una vez, en un solo sitio, y lo que
    /// circula por la aplicación ya es absoluto.
    /// </para>
    /// </summary>
    internal static string ResolverBase(string configurado, string? origenDelNavegador)
    {
        if (EsUrlAbsolutaDeRed(configurado))
            return configurado.TrimEnd('/');

        // Relativa y sin origen conocido (host servidor, MAUI): se devuelve tal
        // cual. Esos hosts reciben siempre una URL absoluta por configuración, así
        // que llegar aquí es un error de despliegue, y falla de forma visible en
        // el primer uso en vez de inventarse un destino.
        if (string.IsNullOrWhiteSpace(origenDelNavegador))
            return configurado;

        return new Uri(new Uri(origenDelNavegador), configurado)
            .ToString()
            .TrimEnd('/');
    }

    /// <summary>
    /// <c>true</c> sólo para http y https. Preguntar por <c>UriKind.Absolute</c> a
    /// secas es el error descrito arriba: <c>file:///</c> también es absoluta.
    /// </summary>
    internal static bool EsUrlAbsolutaDeRed(string valor) =>
        Uri.TryCreate(valor, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
