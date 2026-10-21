using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>
/// Los soportes de un crédito del lote de castigo (api.md §11.3, <c>reports[]</c>): el concepto de irrecuperabilidad, el informe motivado y, mientras la
/// consulta de aportes no esté disponible (D19), el soporte del cruce de los aportes. Son adjuntos <c>LendingWriteOffSupport</c> del crédito, ya
/// disponibles; uno que no lo sea cuenta como que falta. (nuevo)
/// </summary>
public sealed record WriteOffSupportInput(
    Guid LoanPublicId,
    Guid? IrrecoverabilityAttachmentPublicId = null,
    Guid? ReportAttachmentPublicId = null,
    Guid? ContributionsOffsetAttachmentPublicId = null);

/// <summary>Un requisito del castigo verificado: su clave estable, de dónde sale (<c>Norma</c> o <c>Politica</c>), si se cumple y la evidencia. (nuevo)</summary>
public sealed record WriteOffRequirementDto(string Code, string Source, bool Met, string? Evidence);

/// <summary>Un crédito incluido (o ya castigado) en el lote (api.md §11.3, <c>items[]</c>). (nuevo)</summary>
public sealed record WriteOffBatchItemDto(
    Guid ItemPublicId,
    Guid LoanPublicId,
    string LoanNumber,
    RefDto Debtor,
    WriteOffItemStatus Status,
    decimal Balance,
    decimal Principal,
    decimal Interest,
    decimal Other,
    decimal Impairment,
    int? DaysPastDue,
    IReadOnlyList<WriteOffRequirementDto> Requirements,
    Guid? ReportAttachmentPublicId,
    Guid? IrrecoverabilityAttachmentPublicId,
    Guid? OperationPublicId);

/// <summary>Un crédito rechazado del lote con lo que le falta (api.md §11.3, <c>rejected[]</c>; C98). (nuevo)</summary>
public sealed record WriteOffRejectedDto(
    Guid ItemPublicId,
    Guid LoanPublicId,
    string LoanNumber,
    RefDto Debtor,
    IReadOnlyList<string> Missing,
    string? Reason,
    IReadOnlyList<WriteOffRequirementDto> Requirements);

/// <summary>El punto del acta que decidió el lote: la sesión, su número y el orden del punto. (nuevo)</summary>
public sealed record WriteOffSessionItemDto(Guid ItemPublicId, Guid SessionPublicId, string? MinutesNumber, int Order, CreditDecisionOutcome? Outcome);

/// <summary>
/// Un lote de castigo (api.md §11.3): lista y detalle. <c>Warnings</c> (bloque D) trae, al armarlo, lo de la norma y la política que sigue pendiente de
/// validar (<see cref="NormaPendienteDeCartera"/>): no bloquea. (nuevo)
/// </summary>
public sealed record WriteOffBatchDto(
    Guid PublicId,
    string Number,
    WriteOffBatchStatus Status,
    DateOnly CutoffDate,
    string Reason,
    string PreparedBy,
    DateTime PreparedAt,
    int LoanCount,
    decimal TotalPrincipal,
    decimal TotalInterest,
    decimal TotalOther,
    decimal Total,
    WriteOffSessionItemDto? SessionItem,
    string? AppliedBy,
    DateTime? AppliedAt,
    bool SupervisorReportRequired,
    DateOnly? SupervisorReportDueDate,
    DateTime? SupervisorReportedAt,
    string? SupervisorReportReference,
    Guid? SupervisorReportAttachmentPublicId,
    IReadOnlyList<WriteOffBatchItemDto> Items,
    IReadOnlyList<WriteOffRejectedDto> Rejected,
    IReadOnlyList<AvisoDeNormaPendienteDto>? Warnings = null);

/// <summary>Un crédito castigado al aplicar el lote: su operación, lo castigado y lo que salió contra el deterioro. (nuevo)</summary>
public sealed record WriteOffOperationDto(
    Guid LoanPublicId,
    string LoanNumber,
    Guid OperationPublicId,
    string OperationNumber,
    decimal Amount,
    decimal ImpairmentApplied,
    decimal ImpairmentShortfall,
    decimal SuspenseWrittenOff,
    Guid MessagePublicId);

/// <summary>
/// Un crédito que se excluyó del lote al aplicarlo porque ya no cumplía (bloque D, decisión del dueño del 2026-10-15): lo que falta y el motivo. El resto
/// del lote se castigó. (nuevo)
/// </summary>
public sealed record WriteOffExclusionDto(Guid LoanPublicId, string LoanNumber, IReadOnlyList<string> Missing, string Reason);

/// <summary>Lo que dejó aplicar el lote (api.md §11.3, <c>POST /{id}/apply</c>); <c>Excluded</c>, los que ya no cumplían (bloque D). (nuevo)</summary>
public sealed record ApplyWriteOffBatchResultDto(
    IReadOnlyList<WriteOffOperationDto> Operations,
    IReadOnlyList<string> Messages,
    bool SupervisorReportRequired,
    DateOnly? SupervisorReportDueDate,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<WriteOffExclusionDto>? Excluded = null);
