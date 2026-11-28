using IngenIA365ERP.Application.Lending.Payoffs;

namespace IngenIA365ERP.Application.Lending.Disbursements;

// Feature 013, US3 (T0441; api.md §6 y §9): lo que responden el desembolso, su vista previa, las órdenes de giro, las garantías y el pagaré.
// Propiedades en inglés (C26); los enums viajan por nombre como texto. (nuevo)

/// <summary>Una garantía o el pagaré que falta perfeccionar (<c>data.pending[]</c> de <c>Lending.Disbursement.GuaranteesPending</c>).</summary>
public sealed record GarantiaPendienteDto(string Kind, string Reason);

/// <summary>Un crédito recogido que está en mora o perdió capacidad (<c>data.loans[]</c> de <c>Lending.Payoff.RequiresRestructuring</c>).</summary>
public sealed record RecogidaEnMoraDto(Guid LoanPublicId, string Number, int DaysPastDue);

/// <summary>Un crédito recogido en el desembolso: lo aplicado y su comprobante de aplicación (<c>OwnLoanPayoff</c>).</summary>
public sealed record DisbursementPayoffDto(Guid LoanPublicId, string Number, decimal Applied, Guid ReceiptOperationPublicId)
{
    /// <summary>2026-11-27 (feature 028, api.md §3): la clase del producto: <c>Loan</c> o, desde E3, <c>Account</c>. (nuevo)</summary>
    public string Kind { get; init; } = "Loan";

    /// <summary>
    /// 2026-11-27 (feature 028, E3): el producto recogido (en una cuenta, su <c>PublicId</c>; <see cref="LoanPublicId"/> va vacío). Nulo en un crédito: es
    /// <see cref="LoanPublicId"/>. (nuevo)
    /// </summary>
    public Guid? ProductPublicId { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028): cómo se aplicó: <c>Total</c> (cancelación) o <c>Partial</c> (lo exigible y el resto a capital); en una cuenta (E3),
    /// <c>Installments</c> o <c>Deposit</c>. (nuevo)
    /// </summary>
    public string Mode { get; init; } = "Total";

    /// <summary>2026-11-27 (feature 028): lo que proyectó la solicitud; nulo si la solicitud no traía proyección. (nuevo)</summary>
    public decimal? Projected { get; init; }

    /// <summary>2026-11-27 (feature 028): lo que costaba recogerlo a la fecha valor (en un parcial, el valor pedido). (nuevo)</summary>
    public decimal Current { get; init; }

    /// <summary>2026-11-27 (feature 028): lo de ahora menos lo proyectado; nulo sin proyección. (nuevo)</summary>
    public decimal? Difference { get; init; }

    /// <summary>2026-11-27 (feature 028): las causas de la diferencia (<c>Payments</c>, <c>Accrual</c>…). (nuevo)</summary>
    public IReadOnlyList<string> Causes { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, E3): la operación de la cuenta recogida; nula en un crédito. (nuevo)</summary>
    public Guid? AccountOperationPublicId { get; init; }

    /// <summary>2026-11-27 (feature 028, E4): el recibo de Recaudo de la factura recogida; nulo en un crédito. (nuevo)</summary>
    public Guid? ReceiptPublicId { get; init; }

    /// <summary>2026-11-27 (feature 028, E4, T089): el número del recibo de Recaudo de la factura recogida; nulo en un crédito o una cuenta. (nuevo)</summary>
    public string? ReceiptNumber { get; init; }

    /// <summary>2026-11-27 (feature 028, E4, T089): en una factura, su número (el documento cruce); nulo en un crédito o una cuenta. (nuevo)</summary>
    public string? Reference { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T066): lo que la cuota causada que se dejó a su canal debe al vencer; nulo si se recogió todo. (nuevo)</summary>
    public decimal? LeftInstallment { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T066): el número de la cuota causada que se dejó a su canal; nulo si se recogió todo. (nuevo)</summary>
    public int? LeftInstallmentNumber { get; init; }
}

/// <summary>2026-11-27 (feature 028, FR-012): un producto autorizado que ya estaba cancelado al desembolsar: no se recogió. (nuevo)</summary>
public sealed record DisbursementOmittedPayoffDto(Guid LoanPublicId, string Number, decimal? Projected, string Reason)
{
    /// <summary>2026-11-27 (feature 028, E3): <c>Loan</c> o <c>Account</c>. (nuevo)</summary>
    public string Kind { get; init; } = "Loan";

    /// <summary>2026-11-27 (feature 028, E3): en una cuenta, su <c>PublicId</c> (<see cref="LoanPublicId"/> va vacío). (nuevo)</summary>
    public Guid? ProductPublicId { get; init; }

    /// <summary>2026-11-27 (feature 028, E4): en una factura, su número. (nuevo)</summary>
    public string? Reference { get; init; }
}

/// <summary>La orden de giro en la respuesta del desembolso.</summary>
public sealed record DisbursementOrderRefDto(Guid PublicId, string Number, string Status);

/// <summary>Un mensaje que nació en el guardado.</summary>
public sealed record EmittedMessageDto(string Type, Guid MessagePublicId);

/// <summary>Lo que respondió la validación previa contable (T56).</summary>
public sealed record PrevalidationDto(bool Applies, bool? Postable, bool Responded, IReadOnlyList<object> Errors, IReadOnlyList<object> Warnings);

/// <summary>La respuesta del desembolso (api.md §9.1 <c>DisbursementResultDto</c>).</summary>
public sealed record DisbursementResultDto(
    Guid LoanPublicId,
    string LoanNumber,
    Guid OperationPublicId,
    string OperationNumber,
    int Tranche,
    decimal GrossAmount,
    IReadOnlyList<DisbursementPayoffDto> Payoffs,
    decimal NetToDisburse,
    DisbursementOrderRefDto? Order,
    int ScheduleVersion,
    IReadOnlyList<EmittedMessageDto> Messages,
    PrevalidationDto Prevalidation,
    IReadOnlyList<string> Warnings)
{
    /// <summary>
    /// 2026-11-27 (feature 028, FR-012): los productos autorizados que ya estaban cancelados y no se recogieron (aparte de <see cref="Payoffs"/>, que son los
    /// aplicados). (nuevo)
    /// </summary>
    public IReadOnlyList<DisbursementOmittedPayoffDto> OmittedPayoffs { get; init; } = [];
}

/// <summary>La usura del día del desembolso frente a la efectiva con cargos.</summary>
public sealed record DisbursementUsuryDto(decimal Rate, decimal? Usury, bool Passes, Guid? UsuryValuePublicId);

/// <summary>La vigencia de la aprobación.</summary>
public sealed record DisbursementApprovalDto(DateOnly? ExpiresAt, bool Valid);

/// <summary>Las garantías: si están perfeccionadas y lo que falta.</summary>
public sealed record DisbursementCollateralsDto(bool Perfected, IReadOnlyList<GarantiaPendienteDto> Pending);

/// <summary>
/// Un crédito a recoger con lo que hay que pagar para cancelarlo a la fecha valor. 2026-11-27 (feature 028, T034; api.md §3): con lo proyectado en la
/// solicitud, lo de ahora, lo que se aplica, la diferencia por concepto con su causa, si se omite y la cuota causada; <see cref="ToPayOff"/>,
/// <see cref="DaysPastDue"/> y <see cref="RequiresRestructuring"/> siguen como antes.
/// </summary>
public sealed record DisbursementPreviewPayoffDto(Guid LoanPublicId, string Number, decimal ToPayOff, int DaysPastDue, bool RequiresRestructuring)
{
    /// <summary>La clase del producto; en la entrega E1, siempre <c>Loan</c>. (nuevo)</summary>
    public string Kind { get; init; } = "Loan";

    /// <summary>El producto (en un crédito, <see cref="LoanPublicId"/>). (nuevo)</summary>
    public Guid ProductPublicId { get; init; }

    /// <summary>2026-11-27 (feature 028, E4, T087): en una factura a crédito, su número (el documento cruce); nulo en los demás productos. (nuevo)</summary>
    public string? Reference { get; init; }

    /// <summary>Cómo se aplica: <c>Total</c> (cancelación) o <c>Partial</c> (lo exigible y el resto a capital). (nuevo)</summary>
    public string Mode { get; init; } = "Total";

    /// <summary>Cómo lo pidió la solicitud: <c>Total</c> o <c>Partial</c>. (nuevo)</summary>
    public string RequestedMode { get; init; } = "Total";

    /// <summary>Lo que proyectó la solicitud; nulo si no traía proyección (no hay con qué comparar). (nuevo)</summary>
    public decimal? Projected { get; init; }

    /// <summary>Lo de ahora a la fecha valor: todo en un total, el valor pedido en un parcial; cero si se omite. (nuevo)</summary>
    public decimal Current { get; init; }

    /// <summary>Lo que se le aplica (con <c>Recogida.AlDesembolsar = MantenerLoProyectado</c>, lo proyectado topado en lo de ahora). (nuevo)</summary>
    public decimal ToApply { get; init; }

    /// <summary>Lo de ahora menos lo proyectado; nulo sin proyección. (nuevo)</summary>
    public decimal? Difference { get; init; }

    /// <summary>Las causas de la diferencia: <c>Payments</c>, <c>Accrual</c>, <c>Arrears</c>, <c>CurrentInstallment</c>, <c>Parameters</c>, <c>ProductClosed</c>, <c>Other</c>. (nuevo)</summary>
    public IReadOnlyList<string> Causes { get; init; } = [];

    /// <summary>La diferencia por concepto, sólo los que cambiaron (<c>principal</c>, <c>currentInterest</c>…). (nuevo)</summary>
    public IReadOnlyDictionary<string, decimal> Detail { get; init; } = new Dictionary<string, decimal>();

    /// <summary>Ya estaba cancelado: no se recoge y el neto sube. (nuevo)</summary>
    public bool Omitted { get; init; }

    /// <summary>La diferencia pasa de <c>Recogida.ToleranciaDeDiferencia</c>. (nuevo)</summary>
    public bool AboveTolerance { get; init; }

    /// <summary>El modo de intereses con que se liquidó (<c>Corrido</c> o <c>CuotaCompleta</c>). (nuevo)</summary>
    public string? InterestMode { get; init; }

    /// <summary>La cuota causada del crédito a la fecha valor; nula sin cuota por vencer o sin plan. (nuevo)</summary>
    public DisbursementPayoffInstallmentDto? CurrentInstallment { get; init; }

    /// <summary>Lo que hay que saber de este producto (la diferencia y su causa, que se omite, que queda con saldo). (nuevo)</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// 2026-11-27 (feature 028, T051): el interés de lo de ahora explicado renglón a renglón, con los días de la reconstrucción a la fecha valor; vacío en un
    /// omitido o sin reconstrucción. (nuevo)
    /// </summary>
    public IReadOnlyList<PayoffInterestLineDto> InterestExplanation { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, T051): lo reconocido al inicio del ciclo y no causado que se libera; informativo. (nuevo)</summary>
    public decimal RecognizedReleased { get; init; }
}

/// <summary>
/// 2026-11-27 (feature 028, api.md §3): la cuota causada del crédito recogido: número, vencimiento, valor y qué se hace con ella. 2026-11-27 (feature 028, E2,
/// T066): <see cref="Left"/> es lo que la cuota que se deja a su canal debe al vencer (nulo si se incluye), con la cuota enviada y la explicación. (nuevo)
/// </summary>
public sealed record DisbursementPayoffInstallmentDto(int Number, DateOnly? DueDate, decimal Amount, string Treatment, decimal? Left)
{
    /// <summary>La cuota que ya salió a la pagaduría o a la nómina propia; nula si no salió.</summary>
    public PayoffSentInstallmentDto? Sent { get; init; }

    /// <summary>Qué pasa con la cuota en la recogida («se deja en el crédito y la pagará la pagaduría…», «se recoge completa…»).</summary>
    public string? Explanation { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada por el dueño): con <c>Left</c>, la regla con que se deja (<c>TalComoSeEnvio</c> o <c>Proporcional</c>, dentro del
    /// tope legal del prepago), su motivo y lo que la cuota debería tal como se envió. (nuevo)
    /// </summary>
    public string? LeftRule { get; init; }

    /// <summary>2026-11-27 (feature 028, D13 ajustada): por qué el proporcional (<c>WithinLegalPrepaymentCap</c>, <c>LegalPrepaymentCapUnknown</c>). (nuevo)</summary>
    public string? LeftRuleReason { get; init; }

    /// <summary>2026-11-27 (feature 028, D13 ajustada): lo que la cuota debería tal como se envió (la diferencia con <c>Left</c> es lo que sobraría). (nuevo)</summary>
    public decimal? LeftAsSent { get; init; }
}

/// <summary>
/// 2026-11-27 (feature 028, T034; api.md §3 <c>payoffDifferences</c>): cuántos productos cambiaron o se omiten, cuánto en total (ahora − proyectado), si
/// confirmar exige reconocerlo y si alguno pasa de la tolerancia. (nuevo)
/// </summary>
public sealed record DisbursementPayoffDifferencesDto(int Count, decimal Total, bool RequiresAcknowledgement, bool AboveTolerance);

/// <summary>Una cuota del plan propuesto.</summary>
public sealed record DisbursementScheduleLineDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Total)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración de la cuota, aparte del seguro y dentro de <see cref="Total"/>. (nuevo)</summary>
    public decimal Administration { get; init; }
}

/// <summary>El plan propuesto (regenerado sobre lo desembolsado en cada tramo, D12).</summary>
public sealed record DisbursementScheduleDto(int InstallmentCount, decimal AnnualEffectiveRate, IReadOnlyList<DisbursementScheduleLineDto> Installments);

/// <summary>Si la persona que desembolsa choca con la segregación (<c>Decidir × Desembolsar</c>).</summary>
public sealed record DisbursementSegregationDto(string? Conflict);

/// <summary>La vista previa del desembolso (api.md §9.1, <c>disbursement-preview</c>): no escribe.</summary>
public sealed record DisbursementPreviewDto(
    DisbursementUsuryDto Usury,
    DisbursementApprovalDto Approval,
    DisbursementCollateralsDto Collaterals,
    IReadOnlyList<DisbursementPreviewPayoffDto> Payoffs,
    int Tranche,
    decimal GrossAmount,
    decimal NetToDisburse,
    DisbursementScheduleDto Schedule,
    PrevalidationDto Prevalidation,
    DisbursementSegregationDto Segregation,
    IReadOnlyList<string> Blocking,
    IReadOnlyList<string> Warnings,
    string PreviewSha256)
{
    /// <summary>2026-11-27 (feature 028, T034): el resumen de las diferencias de las recogidas. (nuevo)</summary>
    public DisbursementPayoffDifferencesDto PayoffDifferences { get; init; } = new(0, 0m, false, false);
}

/// <summary>Un tramo desembolsado (api.md §9.1, lista).</summary>
public sealed record LoanDisbursementListItemDto(
    Guid OperationPublicId,
    string Number,
    Guid LoanPublicId,
    string LoanNumber,
    int Tranche,
    decimal Amount,
    decimal? NetAmount,
    string Form,
    DateOnly ValueDate,
    DisbursementOrderRefDto? Order,
    bool Reversed);

/// <summary>El beneficiario de una orden; la cuenta enmascarada en la lista y completa en el detalle.</summary>
public sealed record DisbursementBeneficiaryDto(Guid PersonPublicId, string Name, string TaxId, string? Bank, string? AccountType, string? Account);

/// <summary>Un resultado que Tesorería informó de la orden.</summary>
public sealed record DisbursementOrderResultDto(string Outcome, string? Reference, DateOnly Date, DateTime At, string? Reason, bool Reversed);

/// <summary>Una orden de giro (api.md §9.2 <c>DisbursementOrderDto</c>).</summary>
public sealed record DisbursementOrderDto(
    Guid PublicId,
    string Number,
    Guid LoanPublicId,
    string LoanNumber,
    int Tranche,
    string Form,
    DisbursementBeneficiaryDto Beneficiary,
    decimal GrossAmount,
    decimal PayoffAmount,
    decimal NetAmount,
    DateTime IssuedAt,
    DateOnly ResultDueOn,
    string Status,
    bool VoidRequested,
    string? VoidReason,
    bool PaidByFile,
    string? FileReference,
    string? PreviousOrderNumber,
    IReadOnlyList<DisbursementOrderResultDto> Results);

/// <summary>El dueño de una garantía o de un pagaré: la solicitud o el crédito.</summary>
public sealed record CollateralOwnerDto(string Kind, Guid PublicId, string? Number);

/// <summary>Una garantía (api.md §6.1 <c>CollateralDto</c>); sus comandos son los de <c>Disbursements/Collaterals</c> (bloque 7B).</summary>
public sealed record CollateralDto(
    Guid PublicId,
    string Kind,
    string Status,
    CollateralOwnerDto Owner,
    Guid OwnerPersonPublicId,
    string Description,
    decimal Value,
    decimal AdmissibleValue,
    DateOnly ConstitutedOn,
    DateOnly? ValidUntil,
    string? PolicyNumber,
    DateOnly? PolicyExpiresOn,
    decimal? FundCoverage)
{
    public DateOnly? ValuationDate { get; init; }
    public string? RegistrationReference { get; init; }
    public byte? MortgageRank { get; init; }
    public decimal? FundFee { get; init; }
    public DateOnly? ReleasedOn { get; init; }
    public DateOnly? ExtinguishedOn { get; init; }
    public string? ExtinguishReason { get; init; }
    public DateOnly? ExecutedOn { get; init; }

    /// <summary>Los adjuntos <c>LendingGuaranteeDocument</c> de la garantía.</summary>
    public IReadOnlyList<Guid> Documents { get; init; } = [];

    /// <summary>
    /// 2026-11-27 (feature 028, T039; FR-027d): «Por liberar al girar el crédito {número}» si el crédito de la garantía lo canceló el desembolso de otro
    /// que todavía no está girado; nulo en los demás casos. (nuevo)
    /// </summary>
    public string? ReleaseNotice { get; init; }
}

/// <summary>Un pagaré (api.md §6.2 <c>PromissoryNoteDto</c>); sus comandos son los de <c>Disbursements/PromissoryNotes</c> (bloque 7B).</summary>
public sealed record PromissoryNoteDto(
    Guid PublicId,
    string Form,
    string Status,
    CollateralOwnerDto Owner,
    string? Number,
    string? ChannelCode,
    string? ExternalNoteId,
    bool Simulated,
    bool Perfected,
    string? Custody)
{
    public DateOnly IssuedOn { get; init; }
    public DateTime? SignedAt { get; init; }
    public DateTime? InCustodyAt { get; init; }
    public DateTime? EndorsedAt { get; init; }
    public string? EndorseeName { get; init; }
    public DateTime? VoidedAt { get; init; }
    public string? VoidReason { get; init; }
    public DateTime? LastSyncedAt { get; init; }

    /// <summary>La última consulta al depósito informó la firma rechazada (sólo en la respuesta de esa consulta; no se guarda).</summary>
    public bool SignatureRejected { get; init; }

    /// <summary>Los adjuntos <c>LendingGuaranteeDocument</c> del pagaré.</summary>
    public IReadOnlyList<Guid> Documents { get; init; } = [];

    /// <summary>
    /// 2026-11-27 (feature 028, T039; FR-027d): «Por liberar al girar el crédito {número}» si el crédito del pagaré lo canceló el desembolso de otro que
    /// todavía no está girado; nulo en los demás casos. (nuevo)
    /// </summary>
    public string? ReleaseNotice { get; init; }
}
