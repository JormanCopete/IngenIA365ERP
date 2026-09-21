using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Withholding;

namespace IngenIA365ERP.Application.Payroll.WithholdingRates;

/// <summary>Errores del porcentaje fijo (feature 010, US7; contracts/api.md §6).</summary>
public static class WithholdingRateErrors
{
    public static readonly Error NoProcedure2Employees = new("Payroll.WithholdingRate.NoProcedure2Employees", "Ningún empleado activo está en procedimiento 2: no hay porcentaje que calcular.");
    public static Error NoHistory(Guid employeePublicId, string? firstApprovedMonth) =>
        new ErrorConDatos("Payroll.WithholdingRate.NoHistory", "El empleado no tiene nómina aprobada en los doce meses anteriores al cálculo.", new { employeePublicId, firstApprovedMonth });
    public static Error AlreadyApproved(Guid calculationPublicId) =>
        new ErrorConDatos("Payroll.WithholdingRate.AlreadyApproved", "Ese cálculo ya está aprobado y su vigencia abierta.", new { calculationPublicId });
    public static readonly Error NotCalculated = new("Payroll.WithholdingRate.NotCalculated", "Sólo un cálculo en estado Calculated se aprueba o se rechaza.");
    public static readonly Error TableMissing = new("Payroll.WithholdingRate.TableMissing", "No hay tabla de retención vigente (RETEFTE_TABLA_UVT ni la del plan) al mes del cálculo.");
    public static readonly Error CalculationNotFound = new("Payroll.WithholdingRate.CalculationNotFound", "No existe el cálculo indicado.");
    public static Error ParametersMissing(IReadOnlyList<string> codes) =>
        new ErrorConDatos("Payroll.WithholdingRate.ParametersMissing", $"Sin vigencia de los parámetros legales al mes del cálculo: {string.Join(", ", codes)}.", new { codes });
    public const string SemesterIncomplete = "Payroll.WithholdingRate.SemesterIncomplete";
}

public sealed record WithholdingRateItemDto(
    Guid CalculationPublicId, Guid EmployeePublicId, string EmployeeName, string Document, short Year, byte Semester, int Version,
    int MonthsUsed, decimal Divisor, decimal AverageBase, decimal AverageBaseUvt, decimal TheoreticalWithholding, decimal Percentage,
    WithholdingRateCalculationStatus Status, DateOnly ValidFrom, DateOnly ValidTo, DateTime CalculatedAt, string CalculatedBy, DateTime? ApprovedAt, string? ApprovedBy,
    string Sequence, bool PlanTableUsed, decimal? CurrentRatePercent);

public sealed record WithholdingRateSkippedDto(Guid EmployeePublicId, string EmployeeName, string Reason, string Message);

public sealed record WithholdingRateBatchDto(Guid BatchPublicId, IReadOnlyList<WithholdingRateItemDto> Items, IReadOnlyList<WithholdingRateSkippedDto> Skipped, IReadOnlyList<WarningDto> Warnings);

public sealed record WithholdingRateMonthDto(short Year, byte Month, decimal GrossTaxable, decimal MandatoryContributions, bool IncludedSpecialRuns, IReadOnlyList<FixedRateSourceRun> SourceRuns);

public sealed record WithholdingRateTableDto(string Code, DateTime ValidFrom, string Source, IReadOnlyList<WithholdingRateRangeDto> Ranges);

public sealed record WithholdingRateRangeDto(decimal From, decimal? To, decimal? Rate, decimal? Fixed);

public sealed record WithholdingRateDetailDto(
    WithholdingRateItemDto Summary,
    IReadOnlyList<WithholdingRateMonthDto> Months,
    decimal Divisor, string DivisorSource, DepurationSequence Sequence,
    decimal TotalGrossIncome, decimal TotalMandatoryContributions, decimal TotalDeclaredDeductions, decimal TotalExemptIncome, decimal DepuratedBase,
    decimal AverageBase, decimal Uvt, decimal AverageBaseUvt, WithholdingRateTableDto Table, string RangeText, decimal TheoreticalWithholding, decimal Percentage,
    string Rounding, IReadOnlyList<ExplanationStep> Steps, IReadOnlyList<ExplanationStep> DepurationSteps);

public sealed record WithholdingRateApprovedDto(Guid CalculationPublicId, Guid EmployeePublicId, DateOnly ValidFrom, DateOnly ValidTo, DateOnly? PreviousClosedAt, decimal Percentage);

public sealed record WithholdingRateBatchApproveItemDto(Guid CalculationPublicId, bool Approved, string? ErrorCode, string? Message, WithholdingRateApprovedDto? Result);
