using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 005, FR-010: ningún porcentaje, tope ni valor legal de nómina puede vivir
/// en el programa. El cálculo preliminar que esta feature retiró tenía salud al 4 %,
/// pensión al 4 %, aportes del empleador al 8,5 % y 12 %, y el salario dividido
/// entre 30 sin mirar tramos: cada cambio de ley era una recompilación.
///
/// <para>
/// La regla mecánica: en <c>Domain/Payroll/</c> y <c>Application/Payroll/</c> (las
/// carpetas nuevas del motor y sus casos de uso) no se admite ningún literal
/// <c>decimal</c> salvo los de calendario y proporción (<c>0m</c>, <c>1m</c>,
/// <c>2m</c>, <c>0.5m</c>, <c>12m</c>, <c>15m</c>, <c>30m</c>, <c>100m</c>,
/// <c>360m</c>). Todo lo demás —incluido «240 horas al mes»— es un parámetro con
/// vigencia (<c>LegalParameterCodes</c>). Las semillas quedan fuera a propósito:
/// son datos del año, no reglas del motor.
/// </para>
/// </summary>
public class LaNominaNoTieneValoresLegalesFijos
{
    private static readonly Regex LiteralDecimal = new(@"(?<![\w.])\d+(?:\.\d+)?[mM]\b", RegexOptions.Compiled);

    private static readonly HashSet<string> Permitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "0m", "1m", "2m", "0.5m", "12m", "15m", "30m", "100m", "360m", "0.0m", "1.0m",
    };

    // Los números del cálculo retirado, por si vuelven como enteros o en otra forma.
    private static readonly string[] SospechososLiterales =
    [
        "0.04", "0.085", "0.12", "0.0850", "8.5m", "1300000", "1423500", "200000", "162000", "49799", "47065", "240m",
    ];

    [Fact]
    public void Domain_y_Application_de_nomina_no_llevan_porcentajes_ni_topes_fijos()
    {
        var root = RepoPath.FindRepoRoot();
        var offenders = new List<string>();

        foreach (var file in RepoPath.ProductionCSharpFiles())
        {
            var relative = file.Replace(root, string.Empty).Replace('\\', '/');
            var esDeNomina = relative.Contains("/IngenIA365ERP.Domain/Payroll/", StringComparison.Ordinal)
                          || relative.Contains("/IngenIA365ERP.Application/Payroll/", StringComparison.Ordinal);
            if (!esDeNomina) continue;
            if (relative.Contains("Seeder", StringComparison.OrdinalIgnoreCase)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal)
                    || line.StartsWith("*", StringComparison.Ordinal)
                    || line.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match m in LiteralDecimal.Matches(line))
                {
                    if (!Permitidos.Contains(m.Value))
                        offenders.Add($"{relative}:{i + 1}: literal decimal '{m.Value}'");
                }

                foreach (var sospechoso in SospechososLiterales)
                {
                    if (line.Contains(sospechoso, StringComparison.Ordinal))
                        offenders.Add($"{relative}:{i + 1}: valor legal '{sospechoso}' escrito en el programa");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Valores legales o porcentajes fijos en el codigo de nomina (FR-010). " +
            "Cada uno debe ser un parametro con vigencia (LegalParameterCodes):\n  "
            + string.Join("\n  ", offenders));
    }
}
