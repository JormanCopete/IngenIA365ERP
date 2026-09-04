using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// El <c>BaseAddress</c> del HttpClient del navegador tiene que resolverse
/// comprobando el ESQUEMA, nunca sólo <c>UriKind.Absolute</c>.
///
/// <para>
/// <b>Por qué existe esta prueba.</b> Cuando la configuración trae una base
/// relativa —hoy <c>"/"</c>, para que el mismo wwwroot sirva en cualquier
/// dominio— la pregunta «¿es absoluta?» tiene respuestas distintas según el
/// sistema: en Windows <c>Uri.TryCreate("/", UriKind.Absolute, out _)</c>
/// devuelve <b>false</b>, pero el runtime del navegador es de tipo Unix y ahí
/// una barra inicial es una <b>ruta absoluta de archivo</b>, así que devuelve
/// <b>true</b> con <c>file:///</c>.
/// </para>
///
/// <para>
/// El resultado fue un HttpClient con <c>BaseAddress = file:///</c>, y el
/// navegador rechazando cada llamada con «Not allowed to load local resource:
/// file:///api/auth/login». En pantalla eso se ve como
/// <c>TypeError: Failed to fetch</c>, que no se parece en nada a la causa: no
/// aparece nada en los registros del servidor —la petición nunca sale— y
/// pierde la tarde de quien lo busque en la red, en CORS o en el proxy.
/// </para>
///
/// <para>
/// Y no lo caza ninguna prueba corriente: en el runner, que es Windows, el
/// código defectuoso se comporta bien. Sólo falla en el navegador. Por eso la
/// defensa es esta comprobación sobre el fuente y no un caso de prueba.
/// </para>
/// </summary>
public class LaBaseDeApiDelNavegadorNoPuedeSerUnArchivo
{
    private const string Archivo = "Program.cs";
    private const string ProyectoCliente = "IngenIA365ERP.Web.Client";

    [Fact]
    public void La_resolucion_de_la_base_comprueba_el_esquema()
    {
        var programa = RepoPath.ProductionCSharpFiles()
            .FirstOrDefault(f =>
                f.Contains(ProyectoCliente, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(f) == Archivo);

        Assert.True(programa is not null,
            $"No se encontró {ProyectoCliente}/{Archivo}. Si el proyecto se renombró, " +
            "actualizá esta prueba en vez de borrarla: el defecto que cubre sigue siendo posible.");

        var codigo = File.ReadAllText(programa!);

        // Sólo aplica si el archivo sigue resolviendo la base con Uri.TryCreate.
        // Si algún día lo hace de otra forma, esta prueba no estorba.
        if (!codigo.Contains("Uri.TryCreate", StringComparison.Ordinal))
            return;

        Assert.True(
            codigo.Contains("UriSchemeHttp", StringComparison.Ordinal),
            $"{ProyectoCliente}/{Archivo} decide si la base de la API es absoluta con " +
            "Uri.TryCreate pero NO comprueba el esquema.\n\n" +
            "En el navegador, Uri.TryCreate(\"/\", UriKind.Absolute, out _) devuelve TRUE " +
            "con file:///, y el HttpClient queda apuntando al sistema de archivos. El " +
            "síntoma es «Failed to fetch» sin una sola línea en los registros del servidor.\n\n" +
            "Comprobá que el esquema sea Uri.UriSchemeHttp o Uri.UriSchemeHttps.");
    }
}
