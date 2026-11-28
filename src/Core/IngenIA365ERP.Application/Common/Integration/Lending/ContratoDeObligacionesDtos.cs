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
    bool ValidationFailed)
{
    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): lo reconocido al inicio del ciclo y todavía no causado de la cuota del ciclo. Con algo reconocido,
    /// la cuota del ciclo es la del período y la caja la precarga como exigible (F4). Campo nuevo y opcional: no cambia la versión del contrato. (nuevo)
    /// </summary>
    public decimal RecognizedPending { get; init; }

    /// <summary>
    /// 2026-11-26 (pedido del dueño): lo exigible <b>sin</b> lo causado de la cuota del período calendario en curso —las cuotas vencidas o que vencen hoy y los
    /// cargos sin cuota—: la mora para las reglas de la caja. Con el ciclo de la cuota es igual a <see cref="ObligacionListadaDto.DueToday"/>; por período
    /// calendario, <c>DueToday</c> suma además el interés ya causado del mes, que no es mora. Nulo —un doble de pruebas o un contrato anterior— se lee como
    /// <c>DueToday</c>. Campo nuevo y opcional: no cambia la versión del contrato. (nuevo)
    /// </summary>
    public decimal? DueWithoutPeriodInProgress { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, T065; R6, FR-021, FR-023; api.md §4): la cuota del período que ya va en un archivo de libranza o en la nómina propia —en qué
    /// va, la cuota, la pagaduría, el archivo, el período y el valor—; nula si no hay nada enviado. La caja la avisa en «Todo para cancelar» y en la
    /// cancelación. Campo nuevo y opcional: no cambia la versión del contrato. (nuevo)
    /// </summary>
    public CuotaEnviadaDto? SentInstallment { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T071; FR-023, D4): con <see cref="SentInstallment"/>, lo que dice <c>Pagos.CuotaEnviadaAlCancelar</c> para la línea del
    /// crédito (<see cref="CuotaEnviadaAlCancelar"/>: <c>Incluir</c>, <c>Excluir</c> o <c>Preguntar</c>); nulo sin nada enviado. La caja no lee parámetros de
    /// Cartera: lo lee aquí. Campo nuevo y opcional. (nuevo)
    /// </summary>
    public string? SentInstallmentAtPayoff { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T071; FR-019, FR-023): lo que cuesta cancelar todo menos la cuota enviada (la modalidad
    /// <see cref="ModalidadDePago.PayoffExceptCurrentInstallment"/>) si esa cuota es la causada que la modalidad deja, sigue en su canal sin respuesta y el
    /// recaudador la admite; nulo si no se puede dejar. Campo nuevo y opcional. (nuevo)
    /// </summary>
    public decimal? PayoffExceptSentInstallmentAmount { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T071): lo que la cuota enviada queda debiendo a su canal si se deja (con <see cref="PayoffExceptSentInstallmentAmount"/>). (nuevo)</summary>
    public decimal? SentInstallmentLeftAmount { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada por el dueño): con <see cref="SentInstallmentLeftAmount"/>, la regla con que el contrato dejaría la cuota:
    /// <c>TalComoSeEnvio</c> (fuera del tope legal del prepago) o <c>Proporcional</c> (dentro del tope —Ley 1555 de 2012— o sin los valores nacionales para
    /// saberlo). La misma que aplica la modalidad: la caja no la decide. Campo nuevo y opcional. (nuevo)
    /// </summary>
    public string? SentInstallmentLeftRule { get; init; }

    /// <summary>2026-11-27 (feature 028, D13 ajustada): por qué se deja con el proporcional (<c>WithinLegalPrepaymentCap</c>, <c>LegalPrepaymentCapUnknown</c>). (nuevo)</summary>
    public string? SentInstallmentLeftRuleReason { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada): con el proporcional, lo que la cuota debería si se dejara tal como se envió —lo que su canal tiene para pagar—;
    /// la diferencia con <see cref="SentInstallmentLeftAmount"/> es lo que sobra si la paga completa. Igual a ella tal como se envió. (nuevo)
    /// </summary>
    public decimal? SentInstallmentAsSentAmount { get; init; }
}

/// <summary>
/// 2026-11-27 (feature 028, T065; R6, FR-021; api.md §4): la cuota del período de una obligación que ya salió a su canal: en qué va, la cuota que nombra la
/// línea o la novedad, su vencimiento, la pagaduría y el convenio, el archivo (número), el período y los valores de la línea (corriente, vencido y total);
/// en la nómina propia, el período, la corrida y el valor (en <see cref="TotalAmount"/>). <see cref="Explanation"/>, en español. (nuevo)
/// </summary>
public sealed record CuotaEnviadaDto(
    EstadoDeCuotaEnviada State,
    int? InstallmentNumber,
    DateOnly? DueDate,
    string? Paymaster,
    string? Agreement,
    string? File,
    string? Period,
    decimal? CurrentAmount,
    decimal? PastDueAmount,
    decimal? TotalAmount,
    Guid? PayrollRunPublicId,
    string Explanation);

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
    string? PaymentIdempotencyKeyToReverse = null)
{
    /// <summary>
    /// 2026-11-26 (pedido del dueño): los pagos que van <b>antes</b> en el mismo recibo a la misma obligación, en su orden. Cartera los reparte uno tras otro
    /// sobre el estado, sin escribir, y simula éste sobre lo que dejan —lo mismo que hace al aplicar el lote, en orden, en una transacción—: así lo vencido y una
    /// cuota anticipada en el mismo recibo se simulan juntos. Vacío, como siempre. Campo nuevo y opcional: no cambia la versión del contrato. (nuevo)
    /// </summary>
    public IReadOnlyList<PagoPrevioDeSimulacion> Previous { get; init; } = [];
}

/// <summary>2026-11-26: un pago anterior del mismo recibo a la misma obligación, para simular el siguiente sobre lo que deja. (nuevo)</summary>
public sealed record PagoPrevioDeSimulacion(ModalidadDePago Modality, decimal Amount, EleccionDeAbono? Choice = null, int? InstallmentNumber = null);

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
    decimal PastDue, decimal DueToday, decimal PayoffAmount, int DaysPastDue)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): el saldo de la administración, aparte del seguro y de los cargos. (nuevo)</summary>
    public decimal Administration { get; init; }
}

/// <summary>El plan resumido después del pago. (nuevo)</summary>
public sealed record PlanResumidoDto(
    int InstallmentCount, decimal InstallmentAmount, DateOnly? NextDueDate, DateOnly? LastDueDate,
    IReadOnlyList<CuotaDelPlanDto> Installments);

/// <summary>Una cuota del plan. (nuevo)</summary>
/// <remarks>2026-11-16 (feature 024, fase 2): <c>Other</c> es la administración de la cuota (el plan no tiene otros cargos), dentro de <c>Total</c>.</remarks>
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
