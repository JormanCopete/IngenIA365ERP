namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, K3, US14 (T0916; contracts/api.md §11.3 y §12.6): los espejos de WriteOffBatchesEndpoints y de /risk/evaluations en RiskEndpoints.
// Los enums SALEN de la API como número y así se leen aquí (TextosDeCastigos los muestra); al ENTRAR se mandan por nombre («E», «E1»), que la API
// acepta igual que el número. (nuevo)

// ---------------------------------------------------------------------------------------------- §11.3 lotes de castigo --

/// <summary>Los soportes de un crédito del lote: concepto de irrecuperabilidad, informe motivado y, mientras rija D19, el cruce de aportes.</summary>
public sealed record SoportesDeCastigoRequest(
    Guid LoanPublicId,
    Guid? IrrecoverabilityAttachmentPublicId = null,
    Guid? ReportAttachmentPublicId = null,
    Guid? ContributionsOffsetAttachmentPublicId = null);

/// <summary><c>POST /write-off-batches</c>.</summary>
public sealed record LoteDeCastigoRequest(IReadOnlyList<Guid> LoanPublicIds, string Reason, IReadOnlyList<SoportesDeCastigoRequest>? Reports);

/// <summary><c>POST /write-off-batches/{id}/items</c>.</summary>
public sealed record ItemDeCastigoRequest(
    Guid LoanPublicId,
    Guid? IrrecoverabilityAttachmentPublicId = null,
    Guid? ReportAttachmentPublicId = null,
    Guid? ContributionsOffsetAttachmentPublicId = null);

/// <summary><c>POST /write-off-batches/{id}/apply</c>: el punto del acta del consejo que aprobó el lote.</summary>
public sealed record AplicarCastigoRequest(Guid SessionItemPublicId, bool? AcknowledgeWarnings = null);

/// <summary><c>POST /write-off-batches/{id}/supervisor-report</c>.</summary>
public sealed record ReporteAlSupervisorRequest(string Reference, Guid? AttachmentPublicId = null);

/// <summary>Un requisito verificado: clave, origen (<c>Norma</c> o <c>Politica</c>), resultado y evidencia.</summary>
public sealed record RequisitoDeCastigoDto(string Code, string Source, bool Met, string? Evidence);

/// <summary>Un crédito incluido (o ya castigado) en el lote. <c>Status</c>: 1 incluido, 2 rechazado, 3 castigado.</summary>
public sealed record ItemDeCastigoDto(
    Guid ItemPublicId,
    Guid LoanPublicId,
    string LoanNumber,
    ReferenciaDeCreditoDto Debtor,
    int Status,
    decimal Balance,
    decimal Principal,
    decimal Interest,
    decimal Other,
    decimal Impairment,
    int? DaysPastDue,
    IReadOnlyList<RequisitoDeCastigoDto> Requirements,
    Guid? ReportAttachmentPublicId,
    Guid? IrrecoverabilityAttachmentPublicId,
    Guid? OperationPublicId);

/// <summary>Un crédito rechazado con lo que le falta.</summary>
public sealed record RechazoDeCastigoDto(
    Guid ItemPublicId,
    Guid LoanPublicId,
    string LoanNumber,
    ReferenciaDeCreditoDto Debtor,
    IReadOnlyList<string> Missing,
    string? Reason,
    IReadOnlyList<RequisitoDeCastigoDto> Requirements);

/// <summary>El punto del acta que decidió el lote. <c>Outcome</c>: 1 aprueba, 2 con condiciones, 3 niega, 4 devuelve.</summary>
public sealed record PuntoDeCastigoDto(Guid ItemPublicId, Guid SessionPublicId, string? MinutesNumber, int Order, int? Outcome);

/// <summary>
/// Un aviso que no bloquea: un valor de la norma o de la política que sigue pendiente de validar (bloque D); <c>WhoFixes</c> dice dónde se corrige.
/// </summary>
public sealed record AvisoDeNormaPendienteDto(string Code, string Message, QuienCorrigeDeCarteraDto? WhoFixes);

/// <summary>Dónde se corrige un aviso: el módulo, la página y el permiso.</summary>
public sealed record QuienCorrigeDeCarteraDto(string Module, string? Page, string? Permission);

/// <summary>
/// Un lote de castigo. <c>Status</c>: 1 borrador, 2 enviado al consejo, 3 aprobado, 4 rechazado, 5 aplicado, 6 anulado. <c>Warnings</c>, al armarlo,
/// lo de la norma que sigue pendiente (bloque D).
/// </summary>
public sealed record LoteDeCastigoDto(
    Guid PublicId,
    string Number,
    int Status,
    DateOnly CutoffDate,
    string Reason,
    string PreparedBy,
    DateTime PreparedAt,
    int LoanCount,
    decimal TotalPrincipal,
    decimal TotalInterest,
    decimal TotalOther,
    decimal Total,
    PuntoDeCastigoDto? SessionItem,
    string? AppliedBy,
    DateTime? AppliedAt,
    bool SupervisorReportRequired,
    DateOnly? SupervisorReportDueDate,
    DateTime? SupervisorReportedAt,
    string? SupervisorReportReference,
    Guid? SupervisorReportAttachmentPublicId,
    IReadOnlyList<ItemDeCastigoDto> Items,
    IReadOnlyList<RechazoDeCastigoDto> Rejected,
    IReadOnlyList<AvisoDeNormaPendienteDto>? Warnings = null);

/// <summary>Un crédito castigado al aplicar: la operación, lo castigado y lo que salió contra el deterioro.</summary>
public sealed record OperacionDeCastigoDto(
    Guid LoanPublicId,
    string LoanNumber,
    Guid OperationPublicId,
    string OperationNumber,
    decimal Amount,
    decimal ImpairmentApplied,
    decimal ImpairmentShortfall,
    decimal SuspenseWrittenOff,
    Guid MessagePublicId);

/// <summary>Un crédito que se excluyó al aplicar el lote porque ya no cumplía (bloque D): lo que falta y el motivo.</summary>
public sealed record ExclusionDeCastigoDto(Guid LoanPublicId, string LoanNumber, IReadOnlyList<string> Missing, string Reason);

/// <summary>Lo que dejó aplicar el lote; <c>Excluded</c>, los que ya no cumplían (bloque D).</summary>
public sealed record CastigoAplicadoDto(
    IReadOnlyList<OperacionDeCastigoDto> Operations,
    IReadOnlyList<string> Messages,
    bool SupervisorReportRequired,
    DateOnly? SupervisorReportDueDate,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ExclusionDeCastigoDto>? Excluded = null);

// ------------------------------------------------------------------------------------------ §12.6 evaluación de cartera --

/// <summary>Un crédito que se propone recalificar: <c>ProposedCategory</c> y <c>ProposedSubcategory</c> por nombre («D», «E», «E1»).</summary>
public sealed record CreditoAEvaluarRequest(Guid LoanPublicId, string ProposedCategory, string Reason, string? ProposedSubcategory = null);

/// <summary><c>POST /risk/evaluations</c>: el corte (fin de mes), el mes de efecto <c>AAAA-MM</c>, los criterios (texto JSON) y los créditos.</summary>
public sealed record EvaluacionRequest(DateOnly CutoffDate, string EffectMonth, string? Criteria, IReadOnlyList<CreditoAEvaluarRequest> Loans);

/// <summary><c>POST /risk/evaluations/{id}/apply</c>: los puntos del comité de riesgos y del consejo.</summary>
public sealed record AplicarEvaluacionRequest(Guid RecommendationSessionItemPublicId, Guid ApprovalSessionItemPublicId);

/// <summary>Un crédito evaluado. Categorías: 1 A … 5 E; subcategoría 0 ninguna, 1 E1, 2 E2.</summary>
public sealed record CreditoEvaluadoDto(
    Guid LoanPublicId,
    string LoanNumber,
    ReferenciaDeCreditoDto Debtor,
    int RuleRatingAtCutoff,
    int ProposedCategory,
    int? ProposedSubcategory,
    string Reason,
    string? CriteriaScoresJson,
    Guid? SupportAttachmentPublicId);

/// <summary>Un punto de acta de la evaluación. <c>Kind</c>: 1 decisión, 2 recomendación.</summary>
public sealed record PuntoDeEvaluacionDto(Guid ItemPublicId, Guid SessionPublicId, string? MinutesNumber, int Order, int Kind, int? Outcome);

/// <summary>
/// Una evaluación de cartera. <c>Status</c>: 1 borrador, 2 recomendada, 3 aprobada, 4 aplicada, 5 rechazada, 6 descartada. <c>Warnings</c>, al
/// prepararla, la periodicidad de la norma si sigue pendiente (bloque D).
/// </summary>
public sealed record EvaluacionDeCarteraDto(
    Guid PublicId,
    string Number,
    DateOnly CutoffDate,
    string EffectMonth,
    int Status,
    string CriteriaJson,
    string PreparedBy,
    DateTime PreparedAt,
    int LoanCount,
    PuntoDeEvaluacionDto? RecommendationItem,
    PuntoDeEvaluacionDto? ApprovalItem,
    string? AppliedBy,
    DateTime? AppliedAt,
    IReadOnlyList<CreditoEvaluadoDto> Loans,
    int Page,
    int PageSize,
    IReadOnlyList<AvisoDeNormaPendienteDto>? Warnings = null);
