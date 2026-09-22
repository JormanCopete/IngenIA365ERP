using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;

namespace IngenIA365ERP.Domain.Payroll.Withholding;

/// <summary>Secuencia de depuración del procedimiento 2 (política <c>P2SecuenciaDepuracion</c>, research R8).</summary>
public enum DepurationSequence
{
    /// <summary>Depurar la sumatoria de los doce meses y luego dividir (consultorcontable: 3,71 %).</summary>
    DepurateThenDivide = 0,

    /// <summary>Dividir la sumatoria y depurar el promedio con topes mensuales (3,44 %).</summary>
    DivideThenDepurate = 1,
}

/// <summary>Una corrida aprobada que aportó ingreso al mes.</summary>
public sealed record FixedRateSourceRun(Guid RunPublicId, int Version, string Kind, decimal Amount);

/// <summary>Un mes de los doce anteriores: ingreso gravable y aportes obligatorios reales, con sus corridas de origen.</summary>
public sealed record FixedRateMonth(short Year, byte Month, decimal GrossIncome, decimal MandatoryContributions, bool IncludedSpecialRuns, IReadOnlyList<FixedRateSourceRun> SourceRuns);

/// <summary>
/// Lo que el cálculo del porcentaje fijo necesita (feature 010, US7; ET art. 386): los meses
/// con historia, las deducciones declaradas, los parámetros vigentes al mes del cálculo, la
/// tabla (legal o la del plan) y la política de secuencia. Puro: ningún valor legal.
/// </summary>
public sealed record FixedRateInput(
    Guid EmployeePublicId,
    short TargetYear,
    byte TargetSemester,
    IReadOnlyList<FixedRateMonth> Months,
    IReadOnlyList<TaxDeductionInput> DeclaredDeductions,
    ParameterSet Parameters,
    PayrollLegalParameter Table,
    bool PlanTableUsed,
    DepurationSequence Sequence,
    ModoDeTopesAnuales TopesMode = ModoDeTopesAnuales.Mensualizado);

/// <summary>El porcentaje y cada paso que lo formó.</summary>
public sealed record FixedRateResult(
    int MonthsConsidered,
    decimal Divisor,
    string DivisorSource,
    decimal TotalGrossIncome,
    decimal TotalMandatoryContributions,
    decimal TotalDeclaredDeductions,
    decimal TotalExemptIncome,
    decimal DepuratedBase,
    decimal AverageMonthlyBase,
    decimal UvtValue,
    decimal AverageInUvt,
    decimal TheoreticalWithholding,
    decimal RatePercent,
    DepurationSequence Sequence,
    IReadOnlyList<ExplanationStep> Steps,
    IReadOnlyList<ExplanationStep> DepurationSteps,
    string TableCode,
    DateTime TableValidFrom,
    string RangeText);
