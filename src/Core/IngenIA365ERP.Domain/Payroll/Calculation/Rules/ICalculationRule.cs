using System.Globalization;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;

namespace IngenIA365ERP.Domain.Payroll.Calculation.Rules;

/// <summary>
/// Una forma de cálculo de FR-009. Recibe la definición del concepto (la versión
/// vigente), la novedad que la dispara si la hay, y el contexto con tramos, bases y
/// parámetros. Devuelve la línea SIN redondear, con su explicación en los términos de
/// la forma; o nulo si el concepto no produce línea (y deja el motivo en
/// <see cref="RuleContext.Skip"/>).
/// </summary>
public interface ICalculationRule
{
    CalculationKind Kind { get; }
    CalculationLine? Evaluate(PayrollConceptDefinition concept, NoveltyInput? novelty, RuleContext ctx);
}

/// <summary>Las bases del período, que el motor va llenando en orden (data-model §3).</summary>
public sealed class Bases
{
    public decimal? BasicSalary { get; set; }
    public decimal? SalaryEarnings { get; set; }
    public decimal? ContributionBase { get; set; }
    public decimal? BenefitsBase { get; set; }
    public decimal? WithholdingBase { get; set; }
    public decimal? TransportAllowanceBase { get; set; }

    public decimal? Get(CalculationBase kind) => kind switch
    {
        CalculationBase.BasicSalary => BasicSalary,
        CalculationBase.SalaryEarnings => SalaryEarnings,
        CalculationBase.ContributionBase => ContributionBase,
        CalculationBase.BenefitsBase => BenefitsBase,
        CalculationBase.WithholdingBase => WithholdingBase,
        CalculationBase.TransportAllowanceBase => TransportAllowanceBase,
        _ => null,
    };

    public static string Label(CalculationBase kind) => kind switch
    {
        CalculationBase.BasicSalary => "Salario básico del período",
        CalculationBase.SalaryEarnings => "Devengos salariales",
        CalculationBase.ContributionBase => "Base de aportes (IBC)",
        CalculationBase.BenefitsBase => "Base prestacional",
        CalculationBase.WithholdingBase => "Base de retención depurada",
        CalculationBase.TransportAllowanceBase => "Salario mensual para auxilio de transporte",
        _ => kind.ToString(),
    };
}

/// <summary>Lo que toda regla puede consultar. Inmutable salvo las bases y las líneas, que crecen con el cálculo.</summary>
public sealed class RuleContext
{
    public required CalculationInput Input { get; init; }
    public required ParameterSet Parameters { get; init; }
    public required ConceptSet Concepts { get; init; }
    public required IReadOnlyList<SalaryTranche> Tranches { get; init; }
    public Bases Bases { get; } = new();

    /// <summary>Líneas ya calculadas (redondeadas), en orden. Las compuestas leen de aquí.</summary>
    public List<CalculationLine> Lines { get; } = [];

    /// <summary>Por qué un concepto automático no produjo línea. Va al detalle del empleado, no es un bloqueo.</summary>
    public List<string> Skips { get; } = [];

    public PeriodInput Period => Input.Period;
    public EmployeeInput Employee => Input.Employee;

    public int PaidDays => Tranches.Sum(t => t.PaidDays);
    public int LinkedDays => Tranches.Sum(t => t.Days);

    public void Skip(string reason) => Skips.Add(reason);

    /// <summary>
    /// Porcentaje del concepto como fracción: el parámetro legal manda sobre el fijo.
    /// Sustituye <c>{CLASE}</c> por la clase de riesgo ARL del empleado (I..V); si el
    /// empleado no tiene clase registrada, devuelve nulo y el motor lo señala.
    /// </summary>
    public (decimal Fraction, ExplanationParameter? Parameter)? ResolvePercent(PayrollConceptDefinition concept)
    {
        if (!string.IsNullOrWhiteSpace(concept.PercentParameterCode))
        {
            var code = concept.PercentParameterCode;
            if (code.Contains(WellKnownConceptCodes.WorkRiskClassPlaceholder, StringComparison.Ordinal))
            {
                if (Employee.Affiliations.WorkRiskClass is not { } clase) return null;
                code = code.Replace(WellKnownConceptCodes.WorkRiskClassPlaceholder, Roman(clase), StringComparison.Ordinal);
            }
            return (Parameters.Fraction(code), Parameters.Describe(code));
        }
        if (concept.Percent is { } pct) return (pct / 100m, null);
        return (1m, null);
    }

    public static string Roman(int riskClass) => riskClass switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
        _ => throw new CalculationRefusedException($"La clase de riesgo ARL {riskClass} no existe (1 a 5).", []),
    };

    /// <summary>Descripción de la novedad para la explicación.</summary>
    public static ExplanationNovelty? Describe(NoveltyInput? novelty) =>
        novelty is null ? null : new ExplanationNovelty(novelty.PublicId, novelty.Description);
}

/// <summary>Formato de los textos de resumen. Los valores numéricos viajan aparte; esto es sólo la frase.</summary>
public static class Fmt
{
    private static readonly CultureInfo EsCo = CultureInfo.GetCultureInfo("es-CO");

    public static string Money(decimal v) => "$" + v.ToString(v == decimal.Truncate(v) ? "N0" : "N2", EsCo);
    public static string Num(decimal v) => v.ToString(v == decimal.Truncate(v) ? "N0" : "N2", EsCo);
    public static string Pct(decimal fraction) => (fraction * 100m).ToString("0.###", EsCo) + " %";
    public static string Date(DateTime d) => d.ToString("dd/MM/yyyy", EsCo);
}

/// <summary>Construcción uniforme de líneas; el motor redondea después.</summary>
public static class LineFactory
{
    public static CalculationLine Create(PayrollConceptDefinition concept, decimal rawAmount, Explanation explanation,
        NoveltyInput? novelty = null, decimal? quantity = null, decimal? baseAmount = null, decimal? factor = null,
        decimal? rangeFrom = null, decimal? rangeTo = null, int? legalParameterId = null, string? parameterCode = null,
        bool? affectsAccounting = null)
    {
        var contabiliza = affectsAccounting ?? concept.Nature != ConceptNature.Informative;
        if (novelty?.Origin == NoveltyOrigin.LoanDeduction) contabiliza = false; // D-08: Cartera ya contabilizó
        return new CalculationLine
        {
            ConceptDefinitionId = concept.Id,
            Code = concept.Code,
            Name = concept.Name,
            Nature = concept.Nature,
            Quantity = quantity,
            BaseAmount = baseAmount,
            Factor = factor,
            RangeFrom = rangeFrom,
            RangeTo = rangeTo,
            RawAmount = rawAmount,
            Amount = rawAmount,
            LegalParameterId = legalParameterId,
            ParameterCode = parameterCode,
            NoveltyPublicId = novelty?.PublicId,
            Explanation = explanation,
            AffectsAccounting = contabiliza,
        };
    }
}
