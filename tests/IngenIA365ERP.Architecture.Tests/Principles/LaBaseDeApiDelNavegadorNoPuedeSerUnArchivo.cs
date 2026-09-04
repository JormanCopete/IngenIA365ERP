using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// La base de la API se resuelve en <c>AppMode</c>, comprobando el ESQUEMA y sin
/// dejar barra final. Las dos condiciones vienen de dos defectos reales que se
/// vieron en QA el mismo día, y ninguno lo caza una prueba corriente.
///
/// <para>
/// <b>1. <c>file:///</c>.</b> Cuando la configuración trae una base relativa
/// —hoy <c>"/"</c>, para que el mismo wwwroot sirva en cualquier dominio— la
/// pregunta «¿es absoluta?» tiene respuestas distintas según el sistema: en
/// Windows <c>Uri.TryCreate("/", UriKind.Absolute, out _)</c> devuelve
/// <b>false</b>, pero el runtime del navegador es de tipo Unix y ahí una barra
/// inicial es una <b>ruta absoluta de archivo</b>, así que devuelve <b>true</b>
/// con <c>file:///</c>. El HttpClient quedaba con <c>BaseAddress = file:///</c> y
/// el navegador rechazaba cada llamada con «Not allowed to load local resource»,
/// que en pantalla se lee <c>TypeError: Failed to fetch</c>.
/// </para>
///
/// <para>
/// <b>2. <c>//api/…</c>.</b> Diecinueve pantallas arman sus URLs concatenando
/// <c>$"{AppSettings.GetApiBaseUrl()}/api/…"</c>. Si la base termina en barra
/// —o peor, ES una barra— sale <c>//api/…</c>, que el navegador interpreta como
/// URL <b>protocolo-relativa</b> y resuelve contra el host <c>api</c>:
/// <c>ERR_NAME_NOT_RESOLVED</c>. Por eso la base se guarda sin barra final.
/// </para>
///
/// <para>
/// <b>Por qué una prueba sobre el fuente.</b> En el runner, que es Windows, el
/// código defectuoso del punto 1 se comporta <b>bien</b>: sólo falla en el
/// navegador, y el repositorio no tiene pruebas de navegador. Y el punto 2 sólo
/// se manifiesta con una base relativa, que es un valor de despliegue. Ninguna
/// prueba de unidad los habría visto.
/// </para>
/// </summary>
public class LaBaseDeApiDelNavegadorNoPuedeSerUnArchivo
{
    private const string Archivo = "AppMode.cs";

    private static string CodigoDeAppMode()
    {
        var ruta = RepoPath.ProductionCSharpFiles()
            .FirstOrDefault(f => Path.GetFileName(f) == Archivo);

        Assert.True(ruta is not null,
            $"No se encontró {Archivo}. Si se renombró, actualizá esta prueba en vez de " +
            "borrarla: los dos defectos que cubre siguen siendo posibles.");

        return File.ReadAllText(ruta!);
    }

    [Fact]
    public void La_resolucion_comprueba_el_esquema_y_no_solo_que_sea_absoluta()
    {
        var codigo = CodigoDeAppMode();

        // Si algún día deja de resolverse con Uri.TryCreate, esta prueba no estorba.
        if (!codigo.Contains("Uri.TryCreate", StringComparison.Ordinal))
            return;

        Assert.True(
            codigo.Contains("UriSchemeHttp", StringComparison.Ordinal),
            $"{Archivo} decide si la base de la API es absoluta con Uri.TryCreate pero NO " +
            "comprueba el esquema.\n\n" +
            "En el navegador, Uri.TryCreate(\"/\", UriKind.Absolute, out _) devuelve TRUE con " +
            "file:///, y el HttpClient acaba apuntando al sistema de archivos. El síntoma es " +
            "«Failed to fetch» sin UNA SOLA línea en los registros del servidor, porque la " +
            "petición nunca sale a la red.\n\n" +
            "Comprobá Uri.UriSchemeHttp / Uri.UriSchemeHttps.");
    }

    [Fact]
    public void La_base_resuelta_no_termina_en_barra()
    {
        var codigo = CodigoDeAppMode();

        Assert.True(
            codigo.Contains("TrimEnd('/')", StringComparison.Ordinal),
            $"{Archivo} no recorta la barra final de la base de la API.\n\n" +
            "Diecinueve pantallas concatenan $\"{AppSettings.GetApiBaseUrl()}/api/…\". Con " +
            "barra final sale «//api/…», que el navegador lee como URL protocolo-relativa y " +
            "resuelve contra el host «api»: ERR_NAME_NOT_RESOLVED.\n\n" +
            "La base tiene que quedar absoluta y SIN barra final: así sirve para concatenar " +
            "y para HttpClient.BaseAddress a la vez.");
    }
}
