using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US5 (T0561): los espejos de contracts/api.md §8.3 (preferencia del deudor), §10 (simular y reversar por pantalla) y §20 (el
// recaudador de prueba sobre el contrato de obligaciones, interfaces.md §3). Las rutas de la pantalla de Cartera devuelven modalidades,
// conceptos y elecciones como texto (su nombre); las del contrato devuelven sus enums como número (CLAUDE.md «JSON de la API»): aquí se leen
// como int y TextosDePagos los traduce. Lo que se manda va por nombre («ExtraordinaryPrincipal», «ReduceTerm», «Cash»). Sólo PublicId hacia
// afuera. (nuevo)

// ------------------------------------------------------------------------------------------- §10 simular por pantalla --

/// <summary>La simulación por pantalla: modalidad por su nombre, valor (sin él, una cancelación cuesta lo que cuesta), fecha valor, elección y cuota.</summary>
public sealed record SimulacionDePagoRequest(string Modality, decimal? Amount, DateOnly ValueDate, string? PrepaymentChoice = null, int? InstallmentNumber = null);

/// <summary>Una línea de la distribución: cuota (nula = sin cuota), concepto por su nombre y valor.</summary>
public sealed record LineaDeDistribucionDePagoDto(int? Installment, string Concept, decimal Amount);

/// <summary>Un aviso de la simulación con su mensaje en español (el código no se muestra).</summary>
public sealed record AvisoDePagoDto(string Code, string Message);

/// <summary>Una cuota del plan que quedaría después del pago.</summary>
public sealed record CuotaDelPlanNuevoDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Other, decimal Total);

/// <summary>El plan resumido después del pago.</summary>
public sealed record PlanResumidoDePagoDto(int InstallmentCount, decimal InstallmentAmount, DateOnly? NextDueDate, DateOnly? LastDueDate,
    IReadOnlyList<CuotaDelPlanNuevoDto> Installments);

/// <summary>Una alternativa del abono extraordinario (<c>ReduceTerm</c> o <c>ReduceInstallment</c>): su huella, su plan y lo que queda.</summary>
public sealed record AlternativaDeAbonoDto(string SimulationId, int Installments, decimal InstallmentAmount, IReadOnlyList<CuotaDelPlanNuevoDto> Schedule,
    IReadOnlyDictionary<string, decimal> ResultingBalances);

/// <summary><c>PaymentSimulationDto</c> (§10): la distribución, lo que sobra, lo que queda y, en un abono sin elección, sus dos planes.</summary>
public sealed record PagoSimuladoDto(
    string SimulationId,
    string Modality,
    decimal Amount,
    DateOnly ValueDate,
    IReadOnlyList<LineaDeDistribucionDePagoDto> Distribution,
    decimal Surplus,
    decimal Advance,
    decimal CreditBalance,
    IReadOnlyDictionary<string, decimal> ResultingBalances,
    IReadOnlyDictionary<string, AlternativaDeAbonoDto> Alternatives,
    IReadOnlyList<AvisoDePagoDto> Notices,
    DateOnly ValidUntil,
    string? PrepaymentChoice,
    PlanResumidoDePagoDto? NewPlan);

// ------------------------------------------------------------------------------------------- §10 reversar por pantalla --

public sealed record VistaPreviaDeReversoRequest(string? Reason);

/// <summary>El reverso: el motivo y la huella de la vista previa que se vio.</summary>
public sealed record ReversoDePagoRequest(string Reason, string ExpectedPreviewSha256);

public sealed record ReversoPorConceptoDto(IReadOnlyDictionary<string, decimal> ByConcept);

/// <summary>Un pago posterior que el reverso redistribuye: lo que cubría antes y lo que cubriría después.</summary>
public sealed record ReasignacionDePagoDto(Guid OperationPublicId, string Number, IReadOnlyList<LineaDeDistribucionDePagoDto> Before,
    IReadOnlyList<LineaDeDistribucionDePagoDto> After);

public sealed record DocumentoAAnularDto(Guid PublicId, string Kind, string Number);

/// <summary>La vista previa del reverso (§10): su huella, lo que restituye, las reasignaciones y los documentos que se anularían.</summary>
public sealed record VistaPreviaDeReversoDto(
    string PreviewSha256,
    ReversoPorConceptoDto Reversal,
    IReadOnlyList<ReasignacionDePagoDto> Reallocations,
    IReadOnlyList<DocumentoAAnularDto> DocumentsToVoid,
    string? RatingNow,
    string? RatingAfter,
    bool NotifiesCollector,
    bool Reopens,
    IReadOnlyList<LineaDeDistribucionDePagoDto> Adjustments);

public sealed record MensajeDeReversoDto(string Type, Guid MessagePublicId);

/// <summary>Lo que responde el reverso (201).</summary>
public sealed record PagoReversadoDto(Guid ReversalOperationPublicId, string Number, IReadOnlyList<string> Reallocations, IReadOnlyList<Guid> VoidedDocuments,
    IReadOnlyList<MensajeDeReversoDto> Messages, bool Reopened);

// ------------------------------------------------------------------------------------ §8.3 preferencia del deudor --

/// <summary>Una versión de la preferencia: la elección (nula = revocada), desde cuándo y de dónde nació.</summary>
public sealed record VersionDePreferenciaDto(int Version, string? Choice, DateTime Since, string Origin);

public sealed record PreferenciaDePrepagoDto(VersionDePreferenciaDto? Current, IReadOnlyList<VersionDePreferenciaDto> History);

/// <summary>La preferencia nueva (nula = revocada) por su nombre, el motivo y la constancia.</summary>
public sealed record CambioDePreferenciaRequest(string? Choice, string Reason, Guid? EvidenceAttachmentPublicId = null);

// ------------------------------------------------------------------------- comprobante: el recibo del pago (T0560) --

/// <summary>El recibo de un pago o de su reverso en el comprobante de aplicación (FR-077).</summary>
public sealed record ReciboDeOperacionDto(
    string? IdempotencyKey,
    string? Modality,
    string? PrepaymentChoice,
    bool? ChoiceFromPreference,
    decimal? Excess,
    DateOnly? FinalityDate,
    string? ReceiptNumber,
    DateOnly? ReceiptDate,
    string? ReceiptMeansReference,
    string? Reason,
    string? PrevalidationOutcome);

// -------------------------------------------------------------------- §20 el recaudador de prueba (contrato §3) --

/// <summary>La respuesta de cada ítem del contrato: resultado (número de <c>ResultadoDelContrato</c>), código, mensaje en español y valor.</summary>
public sealed record RespuestaDelContratoDto<T>(int Result, string Code, string Message, T? Value, JsonElement? Data = null);

/// <summary>Las respuestas de un lote, en el orden del lote.</summary>
public sealed record RespuestaDeAplicacionDto(
    IReadOnlyList<RespuestaDelContratoDto<ComprobanteDelContratoDto>> Payments,
    IReadOnlyList<RespuestaDelContratoDto<JsonElement>> NotDeducted);

/// <summary>Las obligaciones de una persona a la fecha (estado de Cartera como número).</summary>
public sealed record ListadoDeObligacionesDto(int LendingStatus, Guid PersonPublicId, DateOnly Date, IReadOnlyList<ObligacionListadaDto> Obligations);

/// <summary>Una obligación listada con lo que se puede pagar (papel, estado y modalidades como número).</summary>
public sealed record ObligacionListadaDto(
    Guid ObligationPublicId,
    string Number,
    string CreditLineCode,
    string CreditLineName,
    int Role,
    int Status,
    int DaysPastDue,
    decimal DueToday,
    decimal CycleInstallment,
    decimal PastDue,
    decimal PayoffAmount,
    decimal Advance,
    decimal CreditBalance,
    IReadOnlyList<int> AdmittedModalities,
    IReadOnlyList<string> BlockReasons,
    bool HasPaymentAgreement,
    bool ValidationFailed);

/// <summary>Una línea de la distribución del contrato (concepto como número).</summary>
public sealed record LineaDelContratoDto(int? InstallmentNumber, int Concept, decimal Amount);

/// <summary>Saldos de la obligación.</summary>
public sealed record SaldosDeObligacionDto(
    decimal Principal, decimal CurrentInterest, decimal DefaultInterest, decimal InterestInSuspense,
    decimal Insurance, decimal Charges, decimal Advance, decimal CreditBalance,
    decimal PastDue, decimal DueToday, decimal PayoffAmount, int DaysPastDue);

/// <summary>La simulación del contrato: distribución, saldos después, plan nuevo y, en el abono, las alternativas.</summary>
public sealed record SimulacionDelContratoDto(
    string SimulationId,
    Guid ObligationPublicId,
    int Modality,
    decimal Amount,
    DateOnly ValueDate,
    int? Choice,
    decimal AppliedAmount,
    decimal AdvanceAmount,
    decimal CreditBalanceAmount,
    decimal Surplus,
    IReadOnlyList<LineaDelContratoDto> Distribution,
    SaldosDeObligacionDto BalancesAfter,
    PlanResumidoDePagoDto? NewPlan,
    IReadOnlyList<SimulacionDelContratoDto> Alternatives,
    IReadOnlyList<string> Notices);

/// <summary>El comprobante de aplicación del contrato.</summary>
public sealed record ComprobanteDelContratoDto(
    Guid OperationPublicId,
    string Number,
    Guid ObligationPublicId,
    string ObligationNumber,
    string IdempotencyKey,
    int Modality,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    decimal AppliedAmount,
    decimal AdvanceAmount,
    decimal CreditBalanceAmount,
    decimal Surplus,
    IReadOnlyList<LineaDelContratoDto> Distribution,
    SaldosDeObligacionDto BalancesAfter,
    PlanResumidoDePagoDto? NewPlan,
    bool PaidOff,
    DateOnly? FinalityUntil,
    string InputsHash,
    string RulesVersion,
    IReadOnlyList<string> Warnings);

/// <summary>El comprobante de reverso del contrato.</summary>
public sealed record ComprobanteDeReversoDelContratoDto(
    Guid OperationPublicId,
    string Number,
    Guid ReversedOperationPublicId,
    string ReversedNumber,
    Guid ObligationPublicId,
    string ObligationNumber,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal ReversedAmount,
    string Reason,
    IReadOnlyList<LineaDelContratoDto> Adjustments,
    IReadOnlyList<string> ReallocatedPaymentNumbers,
    SaldosDeObligacionDto BalancesAfter,
    bool ObligationReopened,
    IReadOnlyList<Guid> VoidedDocumentPublicIds);

/// <summary>El comprobante de la resolución de un saldo a favor o de un anticipo (concepto y forma como número).</summary>
public sealed record ComprobanteDeResolucionDto(
    Guid OperationPublicId,
    string Number,
    Guid ObligationPublicId,
    int Concept,
    int Form,
    decimal Amount,
    ComprobanteDelContratoDto? TargetApplication,
    SaldosDeObligacionDto BalancesAfter);

/// <summary>Un <c>PagoReversadoPorCartera</c> que recibió el recaudador de prueba.</summary>
public sealed record PagoReversadoRecibidoDto(
    Guid MessagePublicId,
    string LoanNumber,
    string ReversalNumber,
    string PaymentNumber,
    string IdempotencyKey,
    decimal ReversedAmount,
    string Reason,
    bool Reopened,
    DateTime EmittedAt,
    string? DeliveryStatus);

/// <summary>La simulación del recaudador: con <see cref="PaymentIdempotencyKeyToReverse"/>, la redistribución del reverso de ese pago.</summary>
public sealed record SimulacionDelRecaudadorRequest(
    Guid ObligationPublicId,
    string Modality,
    decimal Amount,
    DateOnly? ValueDate,
    string? PrepaymentChoice = null,
    int? InstallmentNumber = null,
    string? PaymentIdempotencyKeyToReverse = null);

/// <summary>El recibo del recaudador que respalda el pago.</summary>
public sealed record ReciboDePruebaRequest(string Number, DateOnly Date, Guid? OfficePublicId = null, string? MeansReference = null, string? DocumentKind = null);

/// <summary>Un pago del lote (<c>PagoAAplicar</c>): la clave <c>TST:{guid:N}</c>, modalidad y clase de medio por su nombre.</summary>
public sealed record PagoDePruebaRequest(
    string IdempotencyKey,
    Guid ObligationPublicId,
    string Modality,
    decimal Amount,
    DateOnly ValueDate,
    string MeansClass,
    Guid PayerPublicId,
    ReciboDePruebaRequest Receipt,
    string? Choice = null,
    int? InstallmentNumber = null,
    string? SimulationId = null);

/// <summary>Un lote de pagos y su forma (interactiva o no).</summary>
public sealed record LoteDePagosDePruebaRequest(IReadOnlyList<PagoDePruebaRequest> Items, bool Interactive);

public sealed record ReversoDePruebaRequest(string Reason);

/// <summary>
/// La resolución de un saldo: forma (<c>RefundThroughCollector</c>, <c>ApplyToOtherObligation</c>, <c>ApplyAsExtraordinaryPrincipal</c>), la clave
/// (una nueva si no viene), el valor (todo lo disponible si no viene), el concepto (<c>CreditBalance</c> o <c>Advance</c>) y el destino.
/// </summary>
public sealed record ResolucionDePruebaRequest(
    Guid ObligationPublicId,
    string Kind,
    string? IdempotencyKey = null,
    decimal? Amount = null,
    string? Concept = null,
    Guid? TargetObligationPublicId = null,
    string? TargetModality = null,
    string? PrepaymentChoice = null);
