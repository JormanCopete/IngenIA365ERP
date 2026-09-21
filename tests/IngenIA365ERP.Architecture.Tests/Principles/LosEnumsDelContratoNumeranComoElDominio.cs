using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 010 (D-31): los enums «entran por nombre o por número y salen como número», y el
/// contrato <c>contracts/api.md</c> escribe cada uno como <c>Nombre (N)</c>. La revisión de N1
/// encontró cuatro numerados al revés o con nombres que no existen (<c>Enjoyment (0)</c>,
/// <c>Pending (0) | Confirmed (1)</c>, <c>Indefinite (1) | FixedTerm (2)</c>, <c>Loan (0)</c>): un
/// cliente que siguiera el contrato mandaba un valor inválido o registraba un término fijo como
/// indefinido, y ninguna prueba lo notaba porque las e2e afirman los números del código. Esta prueba
/// lee todos los <c>Nombre (N)</c> del contrato y exige que, cuando <c>Nombre</c> es miembro de uno
/// de los enums de nómina que viajan por HTTP, <c>N</c> sea su valor en el Domain.
/// </summary>
public class LosEnumsDelContratoNumeranComoElDominio
{
    private static readonly Type[] EnumsQueViajan =
    [
        typeof(VacationMovementKind), typeof(VacationMovementStatus), typeof(DianContractType),
        typeof(SettlementDeductionKind), typeof(SettlementDeductionStatus), typeof(PayrollRunKind),
        typeof(HolidayOrigin), typeof(ApprenticeStage), typeof(PensionTransitionRegime), typeof(SemanaLaboral),
    ];

    private static readonly Regex NombreYNumero = new(@"\b([A-Z][A-Za-z]+) \((\d+)\)", RegexOptions.Compiled);

    [Fact]
    public void Cada_Nombre_N_del_contrato_lleva_el_numero_del_enum_del_Domain()
    {
        var contrato = File.ReadAllText(Path.Combine(RepoPath.FindRepoRoot(), "specs", "010-nomina-prestaciones-pila-dian", "contracts", "api.md"));

        // nombre → valores que ese nombre tiene en los enums de nómina (un mismo nombre puede vivir en varios).
        var valoresPorNombre = EnumsQueViajan
            .SelectMany(t => Enum.GetNames(t).Select(n => (Nombre: n, Valor: Convert.ToInt32(Enum.Parse(t, n)), Enum: t.Name)))
            .GroupBy(x => x.Nombre)
            .ToDictionary(g => g.Key, g => g.ToList());

        var contrastados = 0;
        var errados = new List<string>();
        foreach (Match m in NombreYNumero.Matches(contrato))
        {
            var nombre = m.Groups[1].Value;
            var numero = int.Parse(m.Groups[2].Value);
            if (!valoresPorNombre.TryGetValue(nombre, out var candidatos)) continue;
            contrastados++;
            if (candidatos.Any(c => c.Valor == numero)) continue;
            errados.Add($"«{m.Value}» — en el Domain vale {string.Join(" / ", candidatos.Select(c => $"{c.Enum}.{c.Nombre} = {c.Valor}"))}");
        }

        Assert.True(contrastados >= 20, $"Sólo se contrastaron {contrastados} pares Nombre (N): la expresión regular quedó desalineada del contrato.");
        Assert.True(errados.Count == 0,
            "contracts/api.md numera enums distinto del Domain (D-31; los enums salen como número y un cliente que siga el contrato manda un valor inválido):\n  "
            + string.Join("\n  ", errados));
    }
}
