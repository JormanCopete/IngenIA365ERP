using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 011 (T076, research R13 y R16): dos límites de las rutas de adjuntos que ninguna prueba de
/// unidad ve romperse.
///
/// <list type="bullet">
/// <item>Las rutas <c>local-blob</c> son <b>anónimas</b>: imitan a las URLs de S3 en desarrollo. Sólo
/// pueden existir con <c>AttachmentStorage:Provider = Local</c> y fuera de Production, y sólo las
/// registra <c>LocalBlobEndpoints</c> detrás de <c>DebenExistir</c>. Una ruta anónima que se cuele en
/// producción no falla en ningún lado: simplemente queda abierta.</item>
/// <item>Todo lo que firma o sirve un archivo lleva el limitador de concurrencia <c>adjuntos</c> (32 +
/// 64 en cola por réplica). Sin él, veinte descargas del formato anterior —que sí pasan por la memoria
/// del servidor— se reparten los pods de 1 GiB con el resto del ERP.</item>
/// </list>
///
/// <para>Son pruebas sobre el fuente porque la API no es una referencia de este proyecto.</para>
/// </summary>
public class LasRutasDeAdjuntosEstanAcotadas
{
    private const string Limitador = "RequireRateLimiting(LimiteDeAdjuntos.Politica)";

    private static string Endpoints =>
        Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints");

    private static string LocalBlob => Path.Combine(Endpoints, "Attachments", "LocalBlobEndpoints.cs");

    /// <summary>Una llamada Map* con su ruta: <c>group.MapPost("/{id:guid}/download-link"</c>.</summary>
    private static readonly Regex Mapeo = new(
        @"\.Map(Get|Post|Put|Delete|Patch|Group)\s*\(\s*""(?<ruta>[^""]*)""",
        RegexOptions.Compiled);

    [Fact]
    public void Las_rutas_locales_solo_se_registran_con_el_proveedor_local_y_fuera_de_produccion()
    {
        var fuente = File.ReadAllText(LocalBlob);

        var condicion = Regex.Match(fuente,
            @"public\s+static\s+bool\s+DebenExistir\s*\([^)]*\)\s*=>(?<cuerpo>[^;]*);", RegexOptions.Singleline);
        Assert.True(condicion.Success, "LocalBlobEndpoints.DebenExistir ya no es la condición de una línea que esta prueba sabe leer.");
        var cuerpo = condicion.Groups["cuerpo"].Value;
        Assert.Contains("\"AttachmentStorage:Provider\"", cuerpo);
        Assert.Contains("\"Local\"", cuerpo);
        Assert.Matches(@"!\s*ambiente\.IsProduction\(\)", cuerpo);
        Assert.Contains("&&", cuerpo);

        // La salida temprana va ANTES de cualquier Map: si no, algo queda registrado igual.
        var salida = Regex.Match(fuente, @"if\s*\(\s*!\s*DebenExistir\s*\([^)]*\)\s*\)\s*return\s*;");
        Assert.True(salida.Success, "AddRoutes tiene que empezar con «if (!DebenExistir(…)) return;».");
        var primerMapeo = Mapeo.Match(fuente);
        Assert.True(primerMapeo.Success && salida.Index < primerMapeo.Index,
            "En LocalBlobEndpoints hay un Map* antes de «if (!DebenExistir(…)) return;».");
    }

    [Fact]
    public void Nadie_mas_registra_rutas_bajo_local_blob()
    {
        var infractores = Directory.EnumerateFiles(Endpoints, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Equals(LocalBlob, StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                var fuente = File.ReadAllText(f);
                return fuente.Contains("LocalBlobEndpoints.Prefijo", StringComparison.Ordinal)
                    || Mapeo.Matches(fuente).Any(m => m.Groups["ruta"].Value.Contains("local-blob", StringComparison.OrdinalIgnoreCase));
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(infractores.Count == 0,
            "Rutas bajo /api/attachments/local-blob fuera de LocalBlobEndpoints (anónimas, sólo para desarrollo): " +
            string.Join(", ", infractores));
    }

    [Theory]
    [InlineData("AttachmentsModule.cs")]
    [InlineData("Attachments/LocalBlobEndpoints.cs")]
    public void Los_grupos_de_adjuntos_llevan_el_limitador(string archivo)
    {
        var fuente = File.ReadAllText(Path.Combine(Endpoints, archivo.Replace('/', Path.DirectorySeparatorChar)));

        // El limitador va en la cadena del MapGroup: hasta el primer «;» después de él.
        var grupo = Regex.Match(fuente, @"\.MapGroup\s*\([^;]*;", RegexOptions.Singleline);
        Assert.True(grupo.Success, $"{archivo}: no se encontró el MapGroup.");
        Assert.Contains(Limitador, grupo.Value);
    }

    /// <summary>
    /// Las rutas de descarga de un módulo (contracts/api.md §9) y cualquier otra que emita un enlace
    /// firmado o sirva el archivo: toda ruta <c>…/download-link</c> de la API, y las <c>…/file</c> de PILA y
    /// dispersión, que bajan el formato anterior pasando por la memoria del servidor.
    /// </summary>
    [Fact]
    public void Las_rutas_que_firman_o_sirven_un_adjunto_llevan_el_limitador()
    {
        var revisadas = 0;
        var infractores = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(Endpoints, "*.cs", SearchOption.AllDirectories))
        {
            var fuente = File.ReadAllText(archivo);
            var nombre = Path.GetFileName(archivo);
            var mapeos = Mapeo.Matches(fuente).Cast<Match>().ToList();
            for (var i = 0; i < mapeos.Count; i++)
            {
                var ruta = mapeos[i].Groups["ruta"].Value;
                var descargaDeModulo = ruta.EndsWith("/file", StringComparison.Ordinal)
                    && (nombre == "PilaEndpoints.cs" || nombre == "DisbursementsEndpoints.cs");
                if (!ruta.EndsWith("/download-link", StringComparison.Ordinal) && !descargaDeModulo) continue;

                // La cadena de esta ruta llega hasta el siguiente Map* (o el final del archivo).
                var fin = i + 1 < mapeos.Count ? mapeos[i + 1].Index : fuente.Length;
                var cadena = fuente[mapeos[i].Index..fin];
                revisadas++;

                // En AttachmentsModule lo pone el grupo, que prueba la teoría de arriba.
                if (nombre == "AttachmentsModule.cs") continue;
                if (!cadena.Contains(Limitador, StringComparison.Ordinal))
                    infractores.Add($"{nombre}: {ruta}");
            }
        }

        // PILA y dispersión, cada una con /file y /download-link, y el enlace de AttachmentsModule.
        Assert.True(revisadas >= 5, $"Se esperaban al menos 5 rutas de descarga y se encontraron {revisadas}: ¿cambió la forma de mapearlas?");
        Assert.True(infractores.Count == 0,
            "Rutas que firman o sirven un adjunto sin el limitador «adjuntos» (research R13): " + string.Join(", ", infractores));
    }
}
