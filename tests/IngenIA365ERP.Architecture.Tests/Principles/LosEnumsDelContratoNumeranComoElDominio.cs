using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 010 (D-39): los enums «entran por nombre o por número y salen como número», y el
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
            "contracts/api.md numera enums distinto del Domain (D-39; los enums salen como número y un cliente que siga el contrato manda un valor inválido):\n  "
            + string.Join("\n  ", errados));
    }

    // ----------------------------------------------------------------- feature 013 (T0121, data-model §9.4) --

    /// <summary>
    /// <c>ClaseDeMedioDeRecaudo</c> son los valores de <c>PaymentMeansClass</c> de la 012 (mismo nombre y número) más los tres de
    /// Cartera con números fijados: <c>OwnPayrollDeduction = 20</c>, <c>ExternalPaymasterDeduction = 21</c>, <c>InternalOffset = 22</c>
    /// (data-model §9.4 rige sobre interfaces.md §3.1).
    /// </summary>
    [Fact]
    public void La_clase_de_medio_de_recaudo_es_la_de_la_012_mas_las_tres_de_Cartera()
    {
        var puerto = Enum.GetValues<IngenIA365ERP.Application.Common.Integration.Lending.ClaseDeMedioDeRecaudo>()
            .ToDictionary(v => v.ToString(), v => (int)v);
        var comercio = Enum.GetValues<IngenIA365ERP.Domain.Enums.Core.PaymentMeansClass>().ToDictionary(v => v.ToString(), v => (int)v);

        foreach (var (nombre, numero) in comercio)
            Assert.True(puerto.TryGetValue(nombre, out var n) && n == numero, $"ClaseDeMedioDeRecaudo.{nombre} debe valer {numero}, como PaymentMeansClass.");
        Assert.Equal(20, puerto["OwnPayrollDeduction"]);
        Assert.Equal(21, puerto["ExternalPaymasterDeduction"]);
        Assert.Equal(22, puerto["InternalOffset"]);
        Assert.Equal(comercio.Count + 3, puerto.Count);
    }

    /// <summary>
    /// Cada enum del contrato de obligaciones lleva los mismos nombres y números que su par del dominio de Cartera (data-model §9.4,
    /// CH40; T0121): <c>ModalidadDePago</c> ↔ <c>PaymentModality</c>, <c>MotivoDeNoDescuento</c> ↔ <c>NonDeductionReason</c>,
    /// <c>EleccionDeAbono</c> ↔ <c>PrepaymentChoice</c>, <c>ConceptoDeObligacion</c> ↔ <c>LoanConcept</c>, <c>EstadoDeObligacion</c>
    /// ↔ <c>LoanStatus</c>. Si alguno diverge, el contrato traduciría mal el número que guarda el libro.
    /// </summary>
    [Theory]
    [InlineData(typeof(IngenIA365ERP.Application.Common.Integration.Lending.ModalidadDePago), typeof(IngenIA365ERP.Domain.Enums.Lending.PaymentModality))]
    [InlineData(typeof(IngenIA365ERP.Application.Common.Integration.Lending.MotivoDeNoDescuento), typeof(IngenIA365ERP.Domain.Enums.Lending.NonDeductionReason))]
    [InlineData(typeof(IngenIA365ERP.Application.Common.Integration.Lending.EleccionDeAbono), typeof(IngenIA365ERP.Domain.Enums.Lending.PrepaymentChoice))]
    [InlineData(typeof(IngenIA365ERP.Application.Common.Integration.Lending.ConceptoDeObligacion), typeof(IngenIA365ERP.Domain.Enums.Lending.LoanConcept))]
    [InlineData(typeof(IngenIA365ERP.Application.Common.Integration.Lending.EstadoDeObligacion), typeof(IngenIA365ERP.Domain.Enums.Lending.LoanStatus))]
    public void Cada_enum_del_contrato_numera_como_su_par_del_dominio(Type contrato, Type dominio)
    {
        static Dictionary<string, int> Valores(Type t) => Enum.GetNames(t).ToDictionary(n => n, n => Convert.ToInt32(Enum.Parse(t, n)));

        Assert.Equal(Valores(dominio), Valores(contrato));
    }

    /// <summary>
    /// Los enums del contrato de obligaciones con los números de data-model §9.4 (los que no tienen par en el dominio, escritos).
    /// </summary>
    [Fact]
    public void Los_enums_del_contrato_de_obligaciones_llevan_los_numeros_de_data_model()
    {
        static Dictionary<string, int> Valores<T>() where T : struct, Enum => Enum.GetValues<T>().ToDictionary(v => v.ToString(), v => Convert.ToInt32(v));
        Assert.Equal(new Dictionary<string, int> { ["FullPayoff"] = 1, ["PastDue"] = 2, ["CycleInstallment"] = 3, ["Advance"] = 4, ["ExtraordinaryPrincipal"] = 5, ["SpecificInstallment"] = 6, ["FreeAmount"] = 7 },
            Valores<IngenIA365ERP.Application.Common.Integration.Lending.ModalidadDePago>());
        Assert.Equal(new Dictionary<string, int> { ["DeductionCap"] = 1, ["InsufficientNet"] = 2, ["Termination"] = 3, ["Leave"] = 4, ["Disability"] = 5, ["DeductionPriority"] = 6, ["Other"] = 99 },
            Valores<IngenIA365ERP.Application.Common.Integration.Lending.MotivoDeNoDescuento>());
        Assert.Equal(new Dictionary<string, int> { ["ReduceTerm"] = 1, ["ReduceInstallment"] = 2 },
            Valores<IngenIA365ERP.Application.Common.Integration.Lending.EleccionDeAbono>());
        Assert.Equal(new Dictionary<string, int> { ["Applied"] = 1, ["AlreadyApplied"] = 2, ["Rejected"] = 3, ["Answered"] = 4 },
            Valores<IngenIA365ERP.Application.Common.Integration.Lending.ResultadoDelContrato>());
        Assert.Equal(new Dictionary<string, int> { ["NotActivated"] = 1, ["Active"] = 2 },
            Valores<IngenIA365ERP.Application.Common.Integration.Lending.EstadoDeCartera>());
        // Feature 014 (T014; research C13, E04): Tesorería recibe transferencias a cuentas y créditos.
        Assert.Equal(new Dictionary<string, int> { ["Receipts"] = 1, ["Payroll"] = 2, ["Contributions"] = 3, ["OwnLoanPayoff"] = 4, ["Test"] = 5, ["Treasury"] = 6 },
            Valores<IngenIA365ERP.Application.Common.Integration.Lending.ClaseDeRecaudador>());
        Assert.Equal(new Dictionary<string, int> { ["PendingDisbursement"] = 1, ["Active"] = 2, ["PaidOff"] = 3, ["WrittenOff"] = 4, ["Voided"] = 5, ["Novated"] = 6 },
            Valores<IngenIA365ERP.Application.Common.Integration.Lending.EstadoDeObligacion>());
        Assert.Equal(10, Enum.GetValues<IngenIA365ERP.Application.Common.Integration.Lending.ConceptoDeObligacion>().Length);
        // El motivo que decide el motor de Nómina es texto del mismo enum.
        Assert.True(Enum.IsDefined(Enum.Parse<IngenIA365ERP.Application.Common.Integration.Lending.MotivoDeNoDescuento>(IngenIA365ERP.Domain.Payroll.Calculation.LoanNonDeductionReasons.DeductionCap)));
        Assert.True(Enum.IsDefined(Enum.Parse<IngenIA365ERP.Application.Common.Integration.Lending.MotivoDeNoDescuento>(IngenIA365ERP.Domain.Payroll.Calculation.LoanNonDeductionReasons.InsufficientNet)));
    }
}
