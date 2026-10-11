namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US4 (T0507): los espejos de contracts/api.md §8.1 (consultas del crédito, estado de cuenta y liquidación), §16.4 (corridas) y
// §16.5 (integridad del libro). Estados, clases y conceptos viajan como texto (su nombre en el dominio); la pantalla los traduce con
// TextosDeCreditos. Sólo PublicId hacia afuera. (nuevo)

// --------------------------------------------------------------------------------------------- §8.1 créditos --

/// <summary>Una referencia por PublicId con su texto visible.</summary>
public sealed record ReferenciaDeCreditoDto(Guid PublicId, string Name);

/// <summary>La línea del crédito.</summary>
public sealed record LineaDelCreditoDto(Guid PublicId, string Code, string Name);

/// <summary>Un crédito en la lista: los saldos son los de la posición diaria vigente (<see cref="PositionDate"/>), nunca la autoridad.</summary>
public sealed record CreditoEnListaDto(
    Guid PublicId,
    string Number,
    ReferenciaDeCreditoDto Debtor,
    LineaDelCreditoDto Line,
    ReferenciaDeCreditoDto Office,
    string Status,
    string Origin,
    decimal DisbursedAmount,
    decimal PrincipalBalance,
    int DaysPastDue,
    DateOnly? PositionDate,
    string? InformativeRating,
    IReadOnlyList<string> Marks);

public sealed record CondicionesDelCreditoDto(
    int Version,
    string RateKind,
    decimal? EffectiveAnnual,
    string? IndexCode,
    decimal? Margin,
    string DayCount,
    string RateEquivalence,
    string Rounding,
    string AllocationOrder,
    string PrepaymentPreference,
    string RulesVersion);

public sealed record MontosDelCreditoDto(decimal Approved, decimal Disbursed, int Tranches);

public sealed record MarcasDelCreditoDto(bool Restructured, int RestructuringCount, bool ValidationFailed, bool Suspended, bool UsuryCapped);

public sealed record VinculosDelCreditoDto(Guid? Application, Guid? OriginLoan, string? OriginLinkKind);

public sealed record GarantiaDelCreditoDto(Guid PublicId, string Kind, string Description, decimal AdmissibleValue, string Status);

public sealed record BloqueoDelDeudorDto(Guid PublicId, string Kind, DateOnly Since, bool Automatic);

/// <summary><c>LoanDto</c> (§8.1).</summary>
public sealed record CreditoDto(
    Guid PublicId,
    string Number,
    string Status,
    string Origin,
    ReferenciaDeCreditoDto Debtor,
    IReadOnlyList<ReferenciaDeCreditoDto> CoDebtors,
    ReferenciaDeCreditoDto Office,
    LineaDelCreditoDto Line,
    string Modality,
    string? Subsegment,
    CondicionesDelCreditoDto? Terms,
    MontosDelCreditoDto Amounts,
    MarcasDelCreditoDto Marks,
    VinculosDelCreditoDto Links,
    IReadOnlyList<GarantiaDelCreditoDto> Collaterals,
    Guid? PromissoryNote,
    IReadOnlyList<BloqueoDelDeudorDto> Blocks,
    string? InvoiceReference);

/// <summary>Una cuota del plan guardado.</summary>
public sealed record CuotaDelPlanDto(int Number, DateOnly PeriodStart, DateOnly DueDate, DateOnly ExigibleDate, decimal OpeningPrincipal,
    decimal Principal, decimal Interest, decimal GraceInterest, decimal Insurance, decimal OtherCharges, decimal Total, decimal ClosingPrincipal,
    bool IsExtraordinary, bool IsGraceInstallment);

/// <summary>Una versión del plan con sus cuotas (<c>GET /{id}/schedule?version=</c>).</summary>
public sealed record PlanDelCreditoDto(int Version, DateOnly ValidFrom, string Reason, string RulesVersion, int FirstRegeneratedInstallment,
    decimal PrincipalBase, IReadOnlyList<CuotaDelPlanDto> Installments);

/// <summary>Una versión del plan, sin cuotas (<c>GET /{id}/schedule/versions</c> y el estado de cuenta).</summary>
public sealed record VersionDelPlanDto(int Version, DateOnly ValidFrom, string Cause, string RulesVersion, int InstallmentCount, int FirstRegeneratedInstallment);

public sealed record RecaudadorDeOperacionDto(string? Code, string? Class);

/// <summary>Una operación del crédito.</summary>
public sealed record OperacionDelCreditoDto(
    Guid PublicId,
    string Number,
    string Kind,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    RecaudadorDeOperacionDto? Collector,
    string? MeansClass,
    Guid? Payer,
    string? ReceiptReference,
    Guid? ReversedBy,
    Guid? Reverses,
    string Actor);

public sealed record DistribucionDeOperacionDto(int? Installment, string Concept, decimal Amount);

public sealed record MensajeDeOperacionDto(string Type, Guid MessagePublicId, string Status);

/// <summary>El comprobante de aplicación: la operación, su distribución y sus mensajes (FR-077).</summary>
public sealed record ComprobanteDeOperacionDto(OperacionDelCreditoDto Operation, IReadOnlyList<DistribucionDeOperacionDto> Distribution,
    IReadOnlyList<MensajeDeOperacionDto> Messages);

/// <summary>Una fila del libro (sólo lectura).</summary>
public sealed record FilaDelLibroDto(
    Guid PublicId,
    string Kind,
    string Concept,
    int? Installment,
    decimal Amount,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    Guid? OperationPublicId,
    Guid? ProcessRunPublicId,
    Guid? ReversesTransaction,
    Guid? ReversedBy,
    Guid? MessagePublicId);

// ------------------------------------------------------------------------------------- estado de cuenta (FR-069) --

public sealed record CuotaDelEstadoDto(int Number, DateOnly DueDate, DateOnly ExigibleDate, string Status, decimal Principal, decimal Interest,
    decimal Insurance, decimal Total, IReadOnlyDictionary<string, decimal> Owed);

public sealed record NoAplicadoDto(int Installment, DateOnly DueDate, string Origin, string Reason, string? ReasonText, decimal ExpectedAmount,
    decimal NotDeductedAmount);

public sealed record MarcasDelEstadoDto(bool Suspended, bool UsuryCapped, bool Restructured, int RestructuringCount);

/// <summary>El estado de cuenta a D con la información hasta R.</summary>
public sealed record EstadoDeCuentaDto(
    Guid LoanPublicId,
    string Number,
    string Status,
    DateOnly Date,
    DateTime? RecordedAsOf,
    string Fingerprint,
    int DaysPastDue,
    int? OldestUnpaidInstallment,
    int InstallmentsPastDue,
    decimal ToPayOff,
    IReadOnlyDictionary<string, decimal> Balances,
    IReadOnlyList<CuotaDelEstadoDto> Installments,
    IReadOnlyList<VersionDelPlanDto> ScheduleVersions,
    MarcasDelEstadoDto Marks,
    IReadOnlyList<NoAplicadoDto> NotApplied,
    string? InformativeRating,
    string? OfficialRating,
    string? CollectionStage);

// --------------------------------------------------------------------------------------- liquidación (FR-068) --

public sealed record CuotaDeLaLiquidacionDto(int Number, DateOnly DueDate, IReadOnlyDictionary<string, decimal> Balances);

/// <summary>Un tramo de la explicación: días, base, tasa del día y si se topó en la usura o se suspendió.</summary>
public sealed record TramoDeLaExplicacionDto(string Concept, int? Installment, DateOnly From, DateOnly To, int Days, decimal Base, decimal? EffectiveAnnual,
    decimal DailyRate, decimal Amount, bool CappedByUsury, bool Suspended);

/// <summary><c>LoanSettlementDto</c> (§8.1): lo que cuesta cancelar a D con lo sabido hasta R, su huella y hasta cuándo vale si se proyectó.</summary>
public sealed record LiquidacionDto(
    Guid LoanPublicId,
    string Number,
    DateOnly Date,
    DateTime? RecordedAsOf,
    string RulesVersion,
    string Fingerprint,
    decimal ToPayOff,
    IReadOnlyDictionary<string, decimal> ByConcept,
    IReadOnlyDictionary<string, decimal> Credits,
    IReadOnlyList<CuotaDeLaLiquidacionDto> ByInstallment,
    int DaysPastDue,
    bool Suspended,
    bool UsuryCapped,
    IReadOnlyList<TramoDeLaExplicacionDto> Explanation,
    IReadOnlyList<string> ExplanationText,
    DateOnly? ValidUntil,
    IReadOnlyList<string> Warnings);

// ------------------------------------------------------------------------------------------- §16.4 corridas --

public sealed record AvanceDeCorridaDto(int Done, int Total);

public sealed record QuienPidioLaCorridaDto(string Kind, string? Name);

/// <summary>Una corrida en la lista, sin datos del deudor.</summary>
public sealed record CorridaEnListaDto(
    Guid PublicId,
    string Kind,
    DateOnly? Date,
    string? Period,
    string Status,
    AvanceDeCorridaDto Progress,
    QuienPidioLaCorridaDto RequestedBy,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    int Errors);

public sealed record ErrorDeCorridaDto(Guid? LoanPublicId, string? LoanNumber, string Step, string Code, string Message, int Attempts, DateTime LastAt,
    bool Resolved);

/// <summary>Una corrida con sus errores paginados.</summary>
public sealed record CorridaDto(
    CorridaEnListaDto Run,
    int? CurrentStep,
    string ExecutionOrigin,
    string? Reason,
    int ProcessedLoans,
    int FailedLoans,
    int SkippedLoans,
    int BatchCount,
    int PartCount,
    int RetryCount,
    string? TotalsJson,
    string? ResultSha256,
    PaginaDeCarteraDto<ErrorDeCorridaDto> Errors);

/// <summary>La paginación que devuelve la API (<c>PagedResult</c>).</summary>
public sealed record PaginaDeCarteraDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);

/// <summary>Lo que responde ordenar o reintentar (202).</summary>
public sealed record CorridaPedidaDto(Guid ProcessRunPublicId, string Status);

public sealed record OrdenDeCorridaRequest(string Kind = "DailyCycle", DateOnly? Date = null);

// ----------------------------------------------------------------------------------------- §16.5 integridad --

public sealed record VerificacionDelLibroRequest(IReadOnlyList<Guid>? LoanPublicIds = null, int? SampleSize = null, DateOnly? UpTo = null);

public sealed record DiferenciaDelLibroDto(Guid LoanPublicId, string Number, DateOnly Date, string Concept, int? Installment, decimal Ledger,
    decimal Reconstruction);

public sealed record VerificacionDelLibroDto(int Checked, DateOnly? UpTo, IReadOnlyList<DiferenciaDelLibroDto> Mismatches, long DurationMs);
