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
