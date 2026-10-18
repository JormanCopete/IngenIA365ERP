using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US9 (T0713; contracts/api.md §12.1–§12.5): la política SARC, la calificación informativa y por análisis, los cierres mensuales con sus
// líneas, cambios, partes y reaperturas, el modelo de deterioro por segmento y la completitud del historial de mora. Espejo de los DTOs de la API:
// categorías, causas, clases y estados llegan como texto (su nombre); el cuadre del cierre reutiliza ConciliacionContableDto. (nuevo)

// ------------------------------------------------------------------------------------------------- política SARC --

/// <summary>Una versión de la política SARC del consejo con su estado calculado (<c>PendingValidation</c>, <c>Validated</c>).</summary>
public sealed record VersionDePoliticaSarcDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    string Status,
    string Reason,
    string OverridesSha256,
    DateTime RegisteredAt,
    string? RegisteredBy,
    DateTime? ValidatedAt,
    string? ValidatedBy,
    Guid? SessionItemPublicId);

/// <summary>Una celda de la política: ruta, lo que dice la norma, lo que propone el consejo y lo que rige (lo más estricto).</summary>
public sealed record CeldaDePoliticaSarcDto(string Path, decimal Norm, decimal Proposed, decimal Effective);

/// <summary>El documento de la norma (Anexo 1) con su huella y las tablas todavía sin cotejar.</summary>
public sealed record NormaSarcDto(Guid DocumentPublicId, string Code, DateOnly ValidFrom, string Sha256, bool Verified, IReadOnlyList<string> UnverifiedTables);

/// <summary>La norma, la política vigente y la efectiva a una fecha, con todas las versiones.</summary>
public sealed record PoliticaSarcDto(
    DateOnly Date,
    NormaSarcDto? Anexo1,
    Guid? EffectiveVersionPublicId,
    string? Overrides,
    IReadOnlyList<CeldaDePoliticaSarcDto> Combined,
    decimal GeneralImpairmentAdditional,
    bool AdditionalCompensatesIndividual,
    IReadOnlyList<VersionDePoliticaSarcDto> Versions);

/// <summary><c>POST /risk/policy/versions</c>: el esquema parcial del Anexo 1 como texto JSON, desde cuándo rige y el motivo.</summary>
public sealed record NuevaVersionDePoliticaSarcRequest(DateOnly ValidFrom, string Overrides, string Reason);

/// <summary><c>POST /risk/policy/versions/{id}/validate</c>: el punto del acta del consejo.</summary>
public sealed record ValidacionDePoliticaSarcRequest(Guid SessionItemPublicId);

/// <summary>Una celda menos estricta que la norma (<c>data.cells[]</c> de <c>Lending.RiskPolicy.LessStrict</c>).</summary>
public sealed record CeldaMenosEstrictaDto(string Path, decimal Norm, decimal Proposed);

// --------------------------------------------------------------------------------------------- calificaciones --

/// <summary>La calificación informativa de un crédito a una fecha (no se guarda; <c>Label</c> = «Informativa»).</summary>
public sealed record CalificacionInformativaDto(
    Guid LoanPublicId,
    string LoanNumber,
    ReferenciaDeCreditoDto Debtor,
    string Modality,
    int DaysPastDue,
    string Category,
    string? Subcategory,
    string Cause,
    string? OfficialCategory,
    string Label);

/// <summary>Una calificación por análisis (sólo peor que la de las reglas).</summary>
public sealed record CalificacionPorAnalisisDto(
    Guid PublicId,
    Guid LoanPublicId,
    string LoanNumber,
    string Category,
    string? Subcategory,
    string Reason,
    Guid? SupportAttachmentPublicId,
    DateOnly Since,
    DateTime SetAt,
    DateTime? LiftedAt,
    string? LiftReason);

/// <summary><c>POST /risk/analysis-ratings</c>: la categoría (A–E) por nombre, la subcategoría sólo en E.</summary>
public sealed record NuevaCalificacionPorAnalisisRequest(Guid LoanPublicId, string Category, string? Subcategory, string Reason,
    Guid? SupportAttachmentPublicId = null);

/// <summary>Una candidata de la calificación con su causa y en palabras.</summary>
public sealed record CandidataDeCalificacionDto(string Category, string Cause, string Detail);

/// <summary>La calificación oficial del último cierre vigente.</summary>
public sealed record CalificacionOficialDto(Guid MonthlyClosePublicId, string Kind, int Year, int Month, int Version, string Category, string? Subcategory,
    string AccountingCategory, string Cause, decimal TotalImpairment);

/// <summary>La calificación de un crédito: la informativa con candidatas y explicación, la oficial y el análisis vigente.</summary>
public sealed record CalificacionDelCreditoDto(
    Guid LoanPublicId,
    string LoanNumber,
    DateOnly Date,
    DateTime? RecordedAsOf,
    string Label,
    string Category,
    string? Subcategory,
    string Cause,
    string AccountingCategory,
    int DaysPastDue,
    IReadOnlyList<CandidataDeCalificacionDto> Candidates,
    IReadOnlyList<string> Explanation,
    CalificacionOficialDto? Official,
    CalificacionPorAnalisisDto? Analysis);

// ------------------------------------------------------------------------------------------------- cierres --

/// <summary>Las marcas de un cierre.</summary>
public sealed record MarcasDelCierreDto(bool ObligatoryModelUnavailable, bool UnvalidatedParameters, bool IncompleteHistory);

/// <summary>Los totales de un cierre.</summary>
public sealed record TotalesDelCierreDto(int Loans, decimal GrossPortfolio, decimal IndividualImpairment, decimal GeneralImpairment);

/// <summary>Un cierre en la lista (<c>Kind</c>: <c>Base</c> o <c>Monthly</c>; <c>Status</c>: <c>Closed</c> o <c>Reopened</c>).</summary>
public sealed record CierreEnListaDto(
    Guid PublicId,
    string Kind,
    int Year,
    int Month,
    int Version,
    DateOnly CloseDate,
    string Status,
    DateTime InformationCutoffAt,
    MarcasDelCierreDto Marks,
    TotalesDelCierreDto Totals,
    DateTime ClosedAt);

/// <summary>Una reapertura del cierre.</summary>
public sealed record ReaperturaDelCierreDto(Guid PublicId, string Reason, DateTime ReopenedAt, string? ReopenedBy, int ReversalPartCount);

/// <summary>Una parte emitida por el cierre o por su reapertura, con su mensaje.</summary>
public sealed record ParteDelCierreDto(Guid PublicId, string FactType, int? BatchNumber, string Branch, int LoanCount, decimal TotalAmount, Guid? MessagePublicId,
    DateOnly AccountingDate);

/// <summary>El encabezado de un cierre con R, marca de agua, documentos, política, modelo por segmento, reaperturas y partes.</summary>
public sealed record CierreDto(
    CierreEnListaDto Close,
    long NationalValuesWatermark,
    JsonElement DocumentsUsed,
    Guid? RiskPolicyVersionPublicId,
    int? RiskPolicyVersionNumber,
    JsonElement ModelBySegment,
    JsonElement? UnvalidatedParameters,
    JsonElement Totals,
    string LinesSha256,
    Guid? PreviousClosePublicId,
    Guid? SupersedesClosePublicId,
    Guid? ProcessRunPublicId,
    IReadOnlyList<ReaperturaDelCierreDto> Reopenings,
    IReadOnlyList<ParteDelCierreDto> Parts)
{
    /// <summary>K6a: los créditos en pérdida esperada con el historial completado por la regla de meses faltantes (<c>missingMonthsRule</c>,
    /// <c>fixedDays</c>, <c>loans[]</c>); nulo si no hubo ninguno.</summary>
    public JsonElement? IncompleteHistory { get; init; }
}

/// <summary>K6a: los componentes de la pérdida esperada de una línea (PI, PDI, exposición, puntaje, incumplimiento, garantía).</summary>
public sealed record PerdidaEsperadaDeLineaDto(
    string? Segment,
    decimal? Z,
    decimal? Score,
    string? ModelRating,
    bool? IsInDefault,
    int? DaysInDefault,
    string? ProbabilityOfDefaultKey,
    decimal? ProbabilityOfDefault,
    string? CollateralType,
    decimal? LossGivenDefault,
    decimal? ExposureAtDefault,
    bool IncompleteArrearsHistory);

/// <summary>La línea de un crédito en el cierre.</summary>
public sealed record LineaDelCierreDto(
    Guid LoanPublicId,
    string LoanNumber,
    ReferenciaDeCreditoDto Debtor,
    string Modality,
    string SarcSegment,
    int DaysPastDue,
    string Category,
    string? Subcategory,
    string AccountingCategory,
    string Cause,
    decimal PrincipalBalance,
    decimal ImpairmentBase,
    decimal CollateralAdmissibleValue,
    decimal ImpairmentPercentage,
    decimal TotalImpairment,
    decimal ImpairmentDelta,
    bool Current,
    IReadOnlyList<string> Explanation)
{
    /// <summary><c>ArrearsAnnex1</c> o <c>ExpectedLossAnnex2</c> (K6a).</summary>
    public string ImpairmentModel { get; init; } = "ArrearsAnnex1";

    /// <summary>Los componentes de la pérdida esperada; nulo en las líneas del Anexo 1 (K6a).</summary>
    public PerdidaEsperadaDeLineaDto? ExpectedLoss { get; init; }
}

/// <summary>Un cambio de calificación frente al cierre anterior (el primero, frente al base).</summary>
public sealed record CambioDeCalificacionDto(Guid LoanPublicId, string LoanNumber, ReferenciaDeCreditoDto Debtor, string? Previous, string Current, string Cause)
{
    /// <summary>K6a: el crédito está en pérdida esperada y su historial de mora se completó con la regla de meses faltantes.</summary>
    public bool IncompleteHistory { get; init; }
}

/// <summary>Un requisito del cierre: código, si se cumple y el detalle.</summary>
public sealed record RequisitoDelCierreDto(string Code, bool Met, string Detail);

/// <summary>La preparación del cierre de un mes: si está listo, la versión que tocaría y, si algo lo impide antes de los requisitos, el bloqueo.</summary>
public sealed record PreparacionDelCierreDto(int Year, int Month, bool Ready, int Version, IReadOnlyList<RequisitoDelCierreDto> Requirements, string? BlockedCode,
    string? BlockedMessage);

/// <summary><c>POST /risk/monthly-closes/{year}/{month}/close</c>.</summary>
public sealed record OrdenDeCierreRequest(string? Reason);

/// <summary><c>POST /risk/monthly-closes/{id}/reopen</c>.</summary>
public sealed record ReaperturaDeCierreRequest(string Reason, Guid? SessionItemPublicId);

// ------------------------------------------------------------------------------------ modelo e historial de mora --

/// <summary>El modelo de deterioro de un segmento con su fundamento y la marca «modelo obligatorio no disponible».</summary>
public sealed record ModeloPorSegmentoDto(string Segment, string Model, string Basis, string? Mark, DateOnly? Since, string? Election);

/// <summary>Un crédito sin historial de mora completo.</summary>
public sealed record HistorialIncompletoDto(Guid LoanPublicId, string LoanNumber, string Segment, int MonthsAvailable, IReadOnlyList<string> MissingMonths);

/// <summary>La completitud del historial de mora a una fecha.</summary>
public sealed record CompletitudDelHistorialDto(DateOnly Date, int MonthsRequired, Guid? RuleDocumentPublicId, string? RuleDocumentCode,
    IReadOnlyList<HistorialIncompletoDto> Incomplete);

/// <summary>Una adopción o un desistimiento voluntario del modelo de pérdida esperada (K6a; <c>Kind</c>: <c>VoluntaryAdoption</c>,
/// <c>VoluntaryWithdrawal</c>).</summary>
public sealed record EleccionDelModeloDto(
    Guid PublicId,
    string Kind,
    DateOnly ValidFrom,
    IReadOnlyList<string> Segments,
    Guid? SessionItemPublicId,
    Guid ImpactStudyAttachmentPublicId,
    DateOnly SupervisorNoticeDate,
    Guid SupervisorNoticeAttachmentPublicId,
    int NoticeLeadDays,
    Guid RegulatoryDocumentPublicId,
    string Reason,
    string? RegisteredBy,
    DateTime RegisteredAt);

/// <summary><c>POST /risk/impairment-model/elections</c> (K6a): la propuesta (sujeto del punto del acta), la clase por nombre, los segmentos (vacío =
/// los tres), desde cuándo rige, el punto del acta, el estudio de impacto, la fecha y el soporte del aviso y el motivo.</summary>
public sealed record NuevaEleccionDelModeloRequest(
    Guid ElectionPublicId,
    string Kind,
    IReadOnlyList<string>? Segments,
    DateOnly ValidFrom,
    Guid SessionItemPublicId,
    Guid ImpactStudyAttachmentPublicId,
    DateOnly NoticeDate,
    Guid NoticeAttachmentPublicId,
    string Reason);
