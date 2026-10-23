using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0002 (R-37; FR-009; SC-020; data-model §23): ningún umbral SARLAFT, tarifa de GMF, plazo de cheques o de libranza, tope de
/// efectivo ni años de conservación vive en el programa. Todos son parámetros con vigencia y fuente (<c>COR_ParameterVersions</c>, módulos
/// <c>RCD</c> y <c>TES</c>), los no validados «pendientes de validar». Molde: <see cref="LaNominaNoTieneValoresLegalesFijos"/>, con los
/// mismos literales decimales admitidos (calendario y proporción).
///
/// <para>
/// Árbol que recorre: <see cref="RecaudoYTesoreria.CarpetasDeRecaudo"/> y <see cref="RecaudoYTesoreria.CarpetasDeTesoreria"/> de
/// <c>Domain</c> y <c>Application</c>, sin lo heredado de Tesorería (R-04). Las semillas quedan fuera a propósito: son datos, no reglas.
/// <see cref="SospechososLiterales"/> nace con los valores de data-model §23 que hoy rigen (5.000.000 y 30.000.000 de SARLAFT, la tarifa del
/// GMF) por si vuelven escritos en el código.
/// </para>
/// </summary>
public class RecaudoYTesoreriaNoTienenValoresLegalesFijos
{
    private static readonly Regex LiteralDecimal = new(@"(?<![\w.])\d+(?:\.\d+)?[mM]\b", RegexOptions.Compiled);

    private static readonly HashSet<string> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "0m", "1m", "2m", "0.5m", "12m", "15m", "30m", "100m", "360m", "0.0m", "1.0m",
    };

    /// <summary>Los valores de data-model §23 que rigen hoy como parámetro: no pueden aparecer escritos en el código.</summary>
    private static readonly string[] SospechososLiterales = ["5000000", "5_000_000", "30000000", "30_000_000", "0.004m", "0.004M"];

    /// <summary>Archivos exentos (relativos a la raíz, barras normales).</summary>
    private static readonly string[] Exentos =
    [
        // Fase 2 (T0042): el catálogo de las claves RCD y TES es donde viven los defectos como dato (texto con vigencia), no reglas.
        "src/Core/IngenIA365ERP.Domain/Receipts/Parameters/ParametrosDeRecaudoYTesoreria.cs",
    ];

    [Fact]
    public void Domain_y_Application_de_Recaudo_y_Tesoreria_no_llevan_valores_legales_fijos()
    {
        var root = RepoPath.FindRepoRoot();
        var archivos = RecaudoYTesoreria.ArchivosDeRecaudo().Concat(RecaudoYTesoreria.ArchivosDeTesoreria())
            .Where(a => ReglaSobreFuente.Relativo(root, a) is var r
                        && (r.StartsWith("src/Core/IngenIA365ERP.Domain/", StringComparison.Ordinal)
                            || r.StartsWith("src/Core/IngenIA365ERP.Application/", StringComparison.Ordinal))
                        && !r.Contains("Seeder", StringComparison.OrdinalIgnoreCase)
                        && !Exentos.Contains(r, StringComparer.Ordinal))
            .ToList();

        var infracciones = new List<string>();
        foreach (var archivo in archivos)
        {
            var relativo = ReglaSobreFuente.Relativo(root, archivo);
            var lineas = File.ReadAllLines(archivo);
            for (var i = 0; i < lineas.Length; i++)
            {
                var linea = lineas[i].Trim();
                if (ReglaSobreFuente.EsComentario(linea)) continue;
                foreach (Match m in LiteralDecimal.Matches(linea))
                    if (!Permitidos.Contains(m.Value)) infracciones.Add($"{relativo}:{i + 1}: literal decimal '{m.Value}'");
                foreach (var sospechoso in SospechososLiterales.Where(s => linea.Contains(s, StringComparison.Ordinal)))
                    infracciones.Add($"{relativo}:{i + 1}: valor legal '{sospechoso}' escrito en el programa");
            }
        }

        ReglaSobreFuente.SinInfracciones(infracciones,
            "Valores legales o porcentajes fijos en Recaudo o Tesorería (FR-009). Cada uno es un parámetro con vigencia de data-model §23");
    }
}
