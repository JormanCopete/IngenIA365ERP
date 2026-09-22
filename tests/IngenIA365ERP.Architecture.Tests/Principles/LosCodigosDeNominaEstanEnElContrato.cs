using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 010 (D-38): el contrato <c>contracts/api.md</c> es la fuente de verdad de los códigos de
/// error y de aviso, y las pantallas los desarman por código. La revisión de N1 encontró dieciocho
/// códigos <c>Payroll.*</c> que nacieron al implementar y ningún documento registraba
/// (<c>Payroll.Severance.CutoffOutsideYear</c>, <c>Payroll.Settlement.KeyMissing</c>, los avisos
/// <c>*.Warning</c>…): un cliente de la API no podía saber qué esperar. Esta prueba exige que todo
/// código <c>Payroll.Recurso.Sufijo</c> escrito como literal en <c>Application/Payroll</c> figure en
/// el contrato de la 010 (completo, o como <c>`.Sufijo`</c>, la abreviatura que el contrato usa bajo
/// cada recurso) o en el de la 005 (<c>specs/005-*/contracts/</c>), de donde vienen los heredados.
/// </summary>
public class LosCodigosDeNominaEstanEnElContrato
{
    private static readonly Regex Codigo = new(@"""(Payroll\.[A-Z][A-Za-z]+\.[A-Z][A-Za-z.]+)""", RegexOptions.Compiled);

    [Fact]
    public void Todo_codigo_Payroll_emitido_por_Application_Payroll_figura_en_el_contrato()
    {
        var root = RepoPath.FindRepoRoot();
        var contratos = Directory.EnumerateFiles(Path.Combine(root, "specs"), "*.md", SearchOption.AllDirectories)
            .Where(f => f.Replace('\\', '/').Contains("/contracts/", StringComparison.Ordinal)
                        && (f.Contains("010-nomina", StringComparison.Ordinal) || f.Contains("005-", StringComparison.Ordinal) || f.Contains("006-", StringComparison.Ordinal)))
            .Select(File.ReadAllText)
            .ToList();
        Assert.True(contratos.Count > 0, "No se encontraron los contratos de nómina bajo specs/*/contracts/.");
        var contrato = string.Join('\n', contratos);

        var emitidos = RepoPath.ProductionCSharpFiles()
            .Where(f => f.Replace('\\', '/').Contains("/IngenIA365ERP.Application/Payroll/", StringComparison.Ordinal))
            .SelectMany(f => Codigo.Matches(File.ReadAllText(f)).Select(m => (Codigo: m.Groups[1].Value, Archivo: Path.GetRelativePath(root, f))))
            .GroupBy(x => x.Codigo)
            .OrderBy(g => g.Key)
            .ToList();
        Assert.True(emitidos.Count > 0, "No se encontró ningún código Payroll.* en Application/Payroll: la expresión regular quedó desalineada del código.");

        var faltantes = emitidos
            .Where(g => !EstaEnElContrato(contrato, g.Key))
            .Select(g => $"{g.Key}  ({string.Join(", ", g.Select(x => x.Archivo).Distinct().Take(3))})")
            .ToList();

        Assert.True(faltantes.Count == 0,
            "Códigos Payroll.* que la API emite y ningún contrato registra (D-38: contracts/api.md §3.6 o la sección del recurso):\n  "
            + string.Join("\n  ", faltantes));
    }

    /// <summary>Completo (<c>Payroll.Vacation.Overlaps</c>) o abreviado como lo escribe el contrato (<c>`.Overlaps`</c>).</summary>
    private static bool EstaEnElContrato(string contrato, string codigo)
    {
        if (contrato.Contains(codigo, StringComparison.Ordinal)) return true;
        var sufijo = codigo[(codigo.IndexOf('.', "Payroll.".Length))..];
        return contrato.Contains($"`{sufijo}`", StringComparison.Ordinal) || contrato.Contains($"{sufijo}`", StringComparison.Ordinal);
    }
}
