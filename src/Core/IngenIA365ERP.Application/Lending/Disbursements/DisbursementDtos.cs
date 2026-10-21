namespace IngenIA365ERP.Application.Lending.Disbursements;

// Feature 013, US3 (T0441; api.md §6 y §9): lo que responden el desembolso, su vista previa, las órdenes de giro, las garantías y el pagaré.
// Propiedades en inglés (C26); los enums viajan por nombre como texto. (nuevo)

/// <summary>Una garantía o el pagaré que falta perfeccionar (<c>data.pending[]</c> de <c>Lending.Disbursement.GuaranteesPending</c>).</summary>
public sealed record GarantiaPendienteDto(string Kind, string Reason);

/// <summary>Un crédito recogido que está en mora o perdió capacidad (<c>data.loans[]</c> de <c>Lending.Payoff.RequiresRestructuring</c>).</summary>
public sealed record RecogidaEnMoraDto(Guid LoanPublicId, string Number, int DaysPastDue);

/// <summary>Un crédito recogido en el desembolso: lo aplicado y su comprobante de aplicación (<c>OwnLoanPayoff</c>).</summary>
public sealed record DisbursementPayoffDto(Guid LoanPublicId, string Number, decimal Applied, Guid ReceiptOperationPublicId);

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
    IReadOnlyList<string> Warnings);

/// <summary>La usura del día del desembolso frente a la efectiva con cargos.</summary>
public sealed record DisbursementUsuryDto(decimal Rate, decimal? Usury, bool Passes, Guid? UsuryValuePublicId);

/// <summary>La vigencia de la aprobación.</summary>
public sealed record DisbursementApprovalDto(DateOnly? ExpiresAt, bool Valid);

/// <summary>Las garantías: si están perfeccionadas y lo que falta.</summary>
public sealed record DisbursementCollateralsDto(bool Perfected, IReadOnlyList<GarantiaPendienteDto> Pending);

/// <summary>Un crédito a recoger con lo que hay que pagar para cancelarlo a la fecha valor.</summary>
public sealed record DisbursementPreviewPayoffDto(Guid LoanPublicId, string Number, decimal ToPayOff, int DaysPastDue, bool RequiresRestructuring);

/// <summary>Una cuota del plan propuesto.</summary>
public sealed record DisbursementScheduleLineDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Total);

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
    string PreviewSha256);

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
}
