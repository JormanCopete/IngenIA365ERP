using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.Terminations;

/// <summary>Un motivo de retiro del catálogo (contracts/api.md §3.4 <c>reasons</c>).</summary>
public sealed record TerminationReasonDto(
    Guid PublicId,
    string Code,
    string Name,
    bool GeneratesSeverancePay,
    bool RequiresContractEndDate,
    string? LegalBasis,
    bool IsSeeded,
    bool IsActive);

/// <summary>Una fila de la lista de terminaciones (contracts/api.md §3.4 <c>GET /</c>).</summary>
public sealed record TerminationListItemDto(
    Guid TerminationPublicId,
    Guid? RunPublicId,
    int? RunVersion,
    string? RunStatus,
    Guid EmployeePublicId,
    string EmployeeName,
    string Document,
    DateOnly TerminationDate,
    string ReasonCode,
    string ReasonName,
    bool GeneratesSeverancePay,
    DianContractType? ContractType,
    DateOnly? ContractEndDate,
    TerminationStatus Status,
    decimal Net,
    DateTime? ApprovedAt,
    string? Notes,
    Guid? SettlementDocumentAttachmentPublicId);

/// <summary>Una línea de la definitiva recién calculada, con su resumen (la explicación completa va por <c>/api/payroll/runs/{runId}/employees/{employeeId}</c>).</summary>
public sealed record SettlementLineDto(
    string ConceptCode,
    string ConceptName,
    ConceptNature Nature,
    decimal? Quantity,
    decimal? BaseAmount,
    decimal Amount,
    bool AffectsAccounting,
    string Summary);

/// <summary>Lo que devuelve registrar la terminación o recalcular la definitiva (contracts/api.md §3.4 <c>POST /</c>).</summary>
public sealed record TerminationRegisteredDto(
    Guid TerminationPublicId,
    Guid RunPublicId,
    int Version,
    DateOnly TerminationDate,
    IReadOnlyList<SettlementLineDto> Lines,
    SettlementDeductionsDto Deductions,
    IReadOnlyList<WarningDto> Warnings,
    IReadOnlyList<string> Skips,
    IReadOnlyList<string> Refusals,
    RunTotalsDto Totals);

/// <summary>
/// Sanción moratoria del CST art. 65, sólo informativa (research R7): un día de salario por cada día
/// de retardo hasta el tope en meses del parámetro <c>SANCION_MORA_ART65_TOPE_MESES</c>. Nunca es una
/// línea: su procedencia la decide un juez.
/// </summary>
public sealed record LatePaymentPenaltyDto(
    DateOnly TerminationDate,
    DateOnly AsOf,
    int DaysLate,
    int DaysCharged,
    decimal MonthlySalary,
    decimal DailyRate,
    decimal Penalty,
    int CapMonths,
    string Note);
