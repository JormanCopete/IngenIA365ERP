using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-014 (DT §2.25, T9): ningún valor legal de crédito vive en el programa; usura, índices, SMMLV,
/// topes y porcentajes son valores nacionales o parámetros con vigencia. Molde: <c>LaNominaNoTieneValoresLegalesFijos</c>.
///
/// <para>Admite los literales de calendario y proporción de Nómina más <c>365</c>; <c>7</c> y <c>10</c> sólo como valor
/// del enum de periodicidad (no son decimales). Recorre el motor nuevo desde ya; la fase Fundacional B suma
/// <c>Application/Lending</c> cuando la cartera heredada se retira. La lista de exenciones por archivo nace vacía.</para>
/// </summary>
public class LaCarteraNoTieneValoresLegalesFijos
{
    /// <summary>Carpetas recorridas (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Domain/Lending"];

    /// <summary>Archivos exentos (rutas relativas a la raíz). Nace vacía.</summary>
    private static readonly string[] Exentos = [];

    private static readonly Regex LiteralDecimal = new(@"(?<![\w.])\d+(?:\.\d+)?[mM]\b", RegexOptions.Compiled);

    private static readonly HashSet<string> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "0m", "1m", "2m", "0.5m", "12m", "15m", "30m", "100m", "360m", "365m", "0.0m", "1.0m",
    };

    [Fact]
    public void El_motor_de_Cartera_no_lleva_valores_legales_fijos()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosEn(Carpetas))
        {
            var relativo = ReglaSobreFuente.Relativo(root, archivo);
            if (Exentos.Contains(relativo) || relativo.Contains("Seeder", StringComparison.OrdinalIgnoreCase)) continue;
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();
                if (ReglaSobreFuente.EsComentario(linea)) continue;
                foreach (Match m in LiteralDecimal.Matches(linea))
                    if (!Permitidos.Contains(m.Value))
                        infracciones.Add($"{relativo}:{i + 1}: literal decimal '{m.Value}'");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Valores legales fijos en el código de Cartera (FR-014): cada uno es un valor nacional o un parámetro");
    }
}
