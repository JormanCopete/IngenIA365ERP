namespace IngenIA365ERP.Application.Common.Integration.Lending;

// DTO del contrato de obligaciones (feature 013, T0149; contracts/interfaces.md §3.4–§3.9). Entrada sin sufijo, salida con
// «Dto»; propiedades en inglés (C26). Un DTO de este archivo cambia sólo con una VersionDelContrato nueva.

// ------------------------------------------------------------------------------------------------ respuesta común (§3.4) --

/// <summary>La respuesta de cada ítem: resultado, código (el evento del hecho o el del error), mensaje en español y valor. (nuevo)</summary>
public sealed record RespuestaDelContratoDto<T>(
    ResultadoDelContrato Result,
    string Code,
    string Message,
    T? Value,
    IReadOnlyDictionary<string, object?>? Data = null);

/// <summary>Las respuestas de un lote, en el orden del lote. (nuevo)</summary>
public sealed record RespuestaDeAplicacionDto(
    IReadOnlyList<RespuestaDelContratoDto<ComprobanteDeAplicacionDto>> Payments,
    IReadOnlyList<RespuestaDelContratoDto<NoDescontadoRegistradoDto>> NotDeducted);

/// <summary>Un no descontado que Cartera guardó en <c>LND_InstallmentNonDeductions</c>. (nuevo)</summary>
public sealed record NoDescontadoRegistradoDto(
    Guid NonDeductionPublicId, Guid ObligationPublicId, int? InstallmentNumber, decimal NotDeductedAmount);

// ------------------------------------------------------------------------------------------------------- listar (§3.5) --

/// <summary>Pide las obligaciones de una persona. (nuevo)</summary>
public sealed record SolicitudDeListado(
    Recaudador Collector,
    Guid PersonPublicId,
    DateOnly Date,
    bool IncludeAsCoDebtor = false);

/// <summary>Las obligaciones de la persona a la fecha. (nuevo)</summary>
public sealed record ListadoDeObligacionesDto(
    EstadoDeCartera LendingStatus,
    Guid PersonPublicId,
    DateOnly Date,
    IReadOnlyList<ObligacionListadaDto> Obligations);

/// <summary>Una obligación listada con lo que se puede pagar. (nuevo)</summary>
public sealed record ObligacionListadaDto(
    Guid ObligationPublicId,
    string Number,
    string CreditLineCode,
    string CreditLineName,
    RolEnLaObligacion Role,
    EstadoDeObligacion Status,
    int DaysPastDue,
    decimal DueToday,
    decimal CycleInstallment,
    decimal PastDue,
    decimal PayoffAmount,
    decimal Advance,
    decimal CreditBalance,
    IReadOnlyList<ModalidadDePago> AdmittedModalities,
    IReadOnlyList<string> BlockReasons,
    bool HasPaymentAgreement,
    bool ValidationFailed);

// ------------------------------------------------------------------------------------------------------ simular (§3.6) --

/// <summary>Pide la distribución de un pago (o de un reverso, con <see cref="PaymentIdempotencyKeyToReverse"/>, C38) sin escribir. (nuevo)</summary>
public sealed record SolicitudDeSimulacion(
    Recaudador Collector,
    Guid ObligationPublicId,
    ModalidadDePago Modality,
    decimal Amount,
    DateOnly ValueDate,
    EleccionDeAbono? Choice = null,
    int? InstallmentNumber = null,
    string? PaymentIdempotencyKeyToReverse = null);

/// <summary>La simulación: distribución, saldos después y plan nuevo. (nuevo)</summary>
public sealed record SimulacionDePagoDto(
    string SimulationId,
    Guid ObligationPublicId,
    ModalidadDePago Modality,
    decimal Amount,
    DateOnly ValueDate,
    EleccionDeAbono? Choice,
    decimal AppliedAmount,
    decimal AdvanceAmount,
    decimal CreditBalanceAmount,
    decimal Surplus,
    IReadOnlyList<LineaDeDistribucionDto> Distribution,
    SaldosDeObligacionDto BalancesAfter,
    PlanResumidoDto? NewPlan,
    IReadOnlyList<SimulacionDePagoDto> Alternatives,
    IReadOnlyList<string> Notices);

/// <summary>Una línea de la distribución por cuota y concepto. (nuevo)</summary>
public sealed record LineaDeDistribucionDto(int? InstallmentNumber, ConceptoDeObligacion Concept, decimal Amount);

/// <summary>Saldos de la obligación. (nuevo)</summary>
public sealed record SaldosDeObligacionDto(
    decimal Principal, decimal CurrentInterest, decimal DefaultInterest, decimal InterestInSuspense,
    decimal Insurance, decimal Charges, decimal Advance, decimal CreditBalance,
    decimal PastDue, decimal DueToday, decimal PayoffAmount, int DaysPastDue);

/// <summary>El plan resumido después del pago. (nuevo)</summary>
public sealed record PlanResumidoDto(
    int InstallmentCount, decimal InstallmentAmount, DateOnly? NextDueDate, DateOnly? LastDueDate,
    IReadOnlyList<CuotaDelPlanDto> Installments);

/// <summary>Una cuota del plan. (nuevo)</summary>
public sealed record CuotaDelPlanDto(
    int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Other, decimal Total);

// ------------------------------------------------------------------------------------------------------- aplicar (§3.7) --

/// <summary>Un lote de pagos y, sólo para <see cref="ClaseDeRecaudador.Payroll"/>, sus no descontados. (nuevo)</summary>
public sealed record SolicitudDeAplicacion(
    Recaudador Collector,
    FormaDeAplicacion Form,
    IReadOnlyList<PagoAAplicar> Payments,
    IReadOnlyList<NoDescontado> NotDeducted);

/// <summary>
/// Un pago del lote. Feature 015, R1 (E-01 a; contratos-v2.md §1): <see cref="BankItem"/> es de la versión 2 del contrato —la partida bancaria que el pago
/// cita—; un recaudador que sólo adoptó la v1 y la manda recibe <c>Integration.Participant.VersionNotSupported</c>. (nuevo)
/// </summary>
public sealed record PagoAAplicar(
    string IdempotencyKey,
    Guid ObligationPublicId,
    ModalidadDePago Modality,
    decimal Amount,
    DateOnly ValueDate,
    ClaseDeMedioDeRecaudo MeansClass,
    Guid PayerPublicId,
    ReferenciaDeRecibo Receipt,
    EleccionDeAbono? Choice = null,
    int? InstallmentNumber = null,
    string? SimulationId = null,
    Guid? DeductionFileLinePublicId = null,
    PartidaBancariaCitada? BankItem = null);

/// <summary>
/// La partida bancaria que cita un pago o una consignación de la versión 2 de los contratos (feature 015, R1, E-01 a y E-02 a; contratos-v2.md §1.1): la línea
/// del extracto, su fecha de movimiento y la cuenta bancaria propia donde entró. Con <c>ValueDate = MovementDate</c> el módulo exime la ventana de fecha valor
/// (no el corte de la carga ni el permiso con monto de un mes cerrado); con otra fecha responde <c>Lending.Payment.BankItemDateMismatch</c>. (nuevo)
/// </summary>
public sealed record PartidaBancariaCitada(
    Guid BankItemPublicId,
    DateOnly MovementDate,
    Guid OwnBankAccountPublicId);

/// <summary>El documento del recaudador que respalda el pago: recibo, NM o liquidación. (nuevo)</summary>
public sealed record ReferenciaDeRecibo(
    string Number,
    DateOnly Date,
    Guid? OfficePublicId,
    string? MeansReference,
    string? DocumentKind);

/// <summary>Una cuota que la nómina propia no descontó, con su motivo (FR-112). (nuevo)</summary>
public sealed record NoDescontado(
    string IdempotencyKey,
    Guid ObligationPublicId,
    int? InstallmentNumber,
    decimal ExpectedAmount,
    decimal NotDeductedAmount,
    MotivoDeNoDescuento Reason,
    string? Detail,
    Guid? SourceItemPublicId);

/// <summary>El comprobante de aplicación: la <c>LoanOperation</c> del pago (T23). (nuevo)</summary>
public sealed record ComprobanteDeAplicacionDto(
    Guid OperationPublicId,
    string Number,
    Guid ObligationPublicId,
    string ObligationNumber,
    string IdempotencyKey,
    ModalidadDePago Modality,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    DateTime RegisteredAt,
    decimal Amount,
    decimal AppliedAmount,
    decimal AdvanceAmount,
    decimal CreditBalanceAmount,
    decimal Surplus,
    IReadOnlyList<LineaDeDistribucionDto> Distribution,
    SaldosDeObligacionDto BalancesAfter,
    PlanResumidoDto? NewPlan,
    bool PaidOff,
    DateOnly? FinalityUntil,
    string InputsHash,
    string RulesVersion,
    IReadOnlyList<string> Warnings);

// ------------------------------------------------------------------------------------------------------ reversar (§3.8) --

/// <summary>Reversa un pago del mismo recaudador por la clave con que se aplicó. (nuevo)</summary>
public sealed record SolicitudDeReverso(
    Recaudador Collector,
    string PaymentIdempotencyKey,
    string Reason,
    ReferenciaDeRecibo? Receipt = null);

/// <summary>El comprobante de reverso. (nuevo)</summary>
public sealed record ComprobanteDeReversoDto(
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
    IReadOnlyList<LineaDeDistribucionDto> Adjustments,
    IReadOnlyList<string> ReallocatedPaymentNumbers,
    SaldosDeObligacionDto BalancesAfter,
    bool ObligationReopened,
    IReadOnlyList<Guid> VoidedDocumentPublicIds);

// --------------------------------------------------------------------------------------------- resolver saldo (§3.9) --

/// <summary>Devuelve o aplica un saldo a favor o un anticipo. (nuevo)</summary>
public sealed record SolicitudDeResolucion(
    Recaudador Collector,
    string IdempotencyKey,
    Guid ObligationPublicId,
    ConceptoDeObligacion Concept,
    decimal Amount,
    FormaDeResolucion Form,
    ReferenciaDeRecibo Receipt,
    Guid? TargetObligationPublicId = null,
    ModalidadDePago? TargetModality = null,
    EleccionDeAbono? Choice = null,
    string? SimulationId = null,
    string? Reason = null);

/// <summary>El comprobante de la resolución. (nuevo)</summary>
public sealed record ComprobanteDeResolucionDto(
    Guid OperationPublicId,
    string Number,
    Guid ObligationPublicId,
    ConceptoDeObligacion Concept,
    FormaDeResolucion Form,
    decimal Amount,
    ComprobanteDeAplicacionDto? TargetApplication,
    SaldosDeObligacionDto BalancesAfter);
