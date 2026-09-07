using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>Una línea liquidada. Es lo que se vuelve <c>PayrollRunLine</c>.</summary>
public sealed class CalculationLine
{
    public required int ConceptDefinitionId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required ConceptNature Nature { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? BaseAmount { get; init; }
    public decimal? Factor { get; init; }
    public decimal? RangeFrom { get; init; }
    public decimal? RangeTo { get; init; }

    /// <summary>Valor antes de redondear. Sólo para el ajuste de redondeo.</summary>
    public decimal RawAmount { get; set; }

    /// <summary>Valor redondeado según la política. Es el que suma, contabiliza y se paga.</summary>
    public decimal Amount { get; set; }

    /// <summary>Parte de una deducción autorizada que no cupo en el tope y se difiere al período siguiente.</summary>
    public decimal DeferredAmount { get; set; }

    public int? LegalParameterId { get; init; }
    public string? ParameterCode { get; init; }
    public Guid? NoveltyPublicId { get; init; }
    public required Explanation Explanation { get; init; }

    /// <summary>Falso para lo que ya contabilizó otro módulo (Cartera, D-08) y para lo informativo.</summary>
    public bool AffectsAccounting { get; init; } = true;

    public int Order { get; set; }
}

public sealed record CalculationTotals(
    decimal Earnings,
    decimal Deductions,
    decimal EmployerContributions,
    decimal Provisions,
    decimal Net,
    decimal RoundingAdjustment,
    decimal DeferredDeductions);

/// <summary>Un tramo de salario dentro del período, ya con sus días comerciales.</summary>
public sealed record SalaryTranche(DateTime From, DateTime To, int Days, int AbsenceDays, decimal MonthlySalary)
{
    public int PaidDays => Math.Max(0, Days - AbsenceDays);
}

public sealed class CalculationResult
{
    public required Guid EmployeePublicId { get; init; }
    public required IReadOnlyList<CalculationLine> Lines { get; init; }
    public required CalculationTotals Totals { get; init; }
    public required RunEmployeeFlag Flags { get; init; }
    public required IReadOnlyList<SalaryTranche> Tranches { get; init; }

    /// <summary>Días vinculados dentro del período (suma de tramos), antes de restar ausencias.</summary>
    public required int DaysLinked { get; init; }

    /// <summary>Días que redujeron el salario (incapacidades, licencias, vacaciones).</summary>
    public required int AbsenceDays { get; init; }

    /// <summary>Lo que el motor se negó a calcular para este empleado, nombrando el motivo (FR-011, FR-039).</summary>
    public required IReadOnlyList<string> Refusals { get; init; }

    /// <summary>Conceptos automáticos que no produjeron línea y por qué (no aplica, sin derecho, valor cero).</summary>
    public IReadOnlyList<string> Skips { get; init; } = [];

    /// <summary>Cómo se formaron las bases del período, paso a paso, para el detalle del empleado.</summary>
    public IReadOnlyList<ExplanationStep> BaseSteps { get; init; } = [];

    public required string InputsHash { get; init; }

    public bool HasBlockers => Flags != RunEmployeeFlag.None;
}

/// <summary>
/// El motor se niega a calcular: falta un parámetro requerido con vigencia, una
/// novedad referencia un concepto que no existe o un concepto compuesto forma un
/// ciclo. Nombra exactamente qué (FR-011). Nunca se calcula «con lo que hay».
/// </summary>
public sealed class CalculationRefusedException(string reason, IReadOnlyList<string> missingCodes)
    : Exception(reason)
{
    public IReadOnlyList<string> MissingCodes { get; } = missingCodes;

    public static CalculationRefusedException MissingParameters(IEnumerable<string> codes, DateTime asOf)
    {
        var list = codes.ToList();
        return new CalculationRefusedException(
            $"No hay vigencia al {asOf:yyyy-MM-dd} para los parámetros legales: {string.Join(", ", list)}. " +
            "Regístrelos en Nómina › Parámetros legales antes de calcular.", list);
    }
}
