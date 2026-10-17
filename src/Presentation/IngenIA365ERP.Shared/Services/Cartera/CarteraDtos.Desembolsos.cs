namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US3 (T0458): los espejos de contracts/api.md §6 (garantías y pagaré), §8.4 (anulación del crédito pendiente de giro), §9
// (desembolso, órdenes de giro y archivos) y §20 (tesorería de prueba). Las clases, formas y estados viajan como texto (su nombre en el
// dominio); la pantalla los traduce con TextosDeDesembolso. (nuevo)

// --------------------------------------------------------------------------------------------- §9.1 desembolso --

/// <summary>El beneficiario del giro: la persona y, en transferencia, su cuenta (<c>accountType</c> <c>Savings</c> o <c>Checking</c>).</summary>
public sealed record BeneficiarioRequest(Guid PersonPublicId, Guid? BankPublicId = null, string? AccountType = null, string? AccountNumber = null);

/// <summary>Un crédito propio que se recoge (<c>mode</c> <c>Full</c> o <c>Partial</c> con <c>amount</c>).</summary>
public sealed record RecogidaRequest(Guid LoanPublicId, string Mode = "Full", decimal? Amount = null);

/// <summary><c>DisbursementInput</c> (§9.1) con <c>acknowledgeWarnings</c>.</summary>
public sealed record DesembolsoRequest
{
    public Guid ApplicationPublicId { get; init; }
    public Guid? LoanPublicId { get; init; }
    public decimal? TrancheAmount { get; init; }
    public DateOnly? ValueDate { get; init; }
    public string Form { get; init; } = "Transfer";
    public BeneficiarioRequest? Beneficiary { get; init; }
    public IReadOnlyList<RecogidaRequest> OwnLoanPayoffs { get; init; } = [];
    public bool PaidByFile { get; init; }
    public string? ExpectedPreviewSha256 { get; init; }
    public bool AcknowledgeWarnings { get; init; }
}

public sealed record GarantiaPendienteDto(string Kind, string Reason);

public sealed record UsuraDelDesembolsoDto(decimal Rate, decimal? Usury, bool Passes, Guid? UsuryValuePublicId);

public sealed record AprobacionDelDesembolsoDto(DateOnly? ExpiresAt, bool Valid);

public sealed record GarantiasDelDesembolsoDto(bool Perfected, IReadOnlyList<GarantiaPendienteDto> Pending);

public sealed record RecogidaPropuestaDto(Guid LoanPublicId, string Number, decimal ToPayOff, int DaysPastDue, bool RequiresRestructuring);

public sealed record CuotaPropuestaDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Total);

public sealed record PlanPropuestoDto(int InstallmentCount, decimal AnnualEffectiveRate, IReadOnlyList<CuotaPropuestaDto> Installments);

public sealed record ValidacionPreviaDto(bool Applies, bool? Postable, bool Responded);

public sealed record SegregacionDelDesembolsoDto(string? Conflict);

/// <summary>La vista previa del desembolso: no escribe. <c>Blocking</c> trae los códigos que impedirían desembolsar.</summary>
public sealed record VistaPreviaDeDesembolsoDto(
    UsuraDelDesembolsoDto Usury,
    AprobacionDelDesembolsoDto Approval,
    GarantiasDelDesembolsoDto Collaterals,
    IReadOnlyList<RecogidaPropuestaDto> Payoffs,
    int Tranche,
    decimal GrossAmount,
    decimal NetToDisburse,
    PlanPropuestoDto Schedule,
    ValidacionPreviaDto Prevalidation,
    SegregacionDelDesembolsoDto Segregation,
    IReadOnlyList<string> Blocking,
    IReadOnlyList<string> Warnings,
    string PreviewSha256);

public sealed record RecogidaAplicadaDto(Guid LoanPublicId, string Number, decimal Applied, Guid ReceiptOperationPublicId);

public sealed record ReferenciaDeOrdenDto(Guid PublicId, string Number, string Status);

public sealed record MensajeEmitidoDeCarteraDto(string Type, Guid MessagePublicId);

/// <summary>La respuesta del desembolso (§9.1 <c>DisbursementResultDto</c>).</summary>
public sealed record DesembolsoRealizadoDto(
    Guid LoanPublicId,
    string LoanNumber,
    Guid OperationPublicId,
    string OperationNumber,
    int Tranche,
    decimal GrossAmount,
    IReadOnlyList<RecogidaAplicadaDto> Payoffs,
    decimal NetToDisburse,
    ReferenciaDeOrdenDto? Order,
    int ScheduleVersion,
    IReadOnlyList<MensajeEmitidoDeCarteraDto> Messages,
    IReadOnlyList<string> Warnings);

/// <summary>Un tramo desembolsado (lista de §9.1).</summary>
public sealed record TramoDesembolsadoDto(
    Guid OperationPublicId,
    string Number,
    Guid LoanPublicId,
    string LoanNumber,
    int Tranche,
    decimal Amount,
    decimal? NetAmount,
    string Form,
    DateOnly ValueDate,
    ReferenciaDeOrdenDto? Order,
    bool Reversed);

// ------------------------------------------------------------------------------------------ §9.2 órdenes de giro --

public sealed record BeneficiarioDeOrdenDto(Guid PersonPublicId, string Name, string TaxId, string? Bank, string? AccountType, string? Account);

public sealed record ResultadoDeOrdenDto(string Outcome, string? Reference, DateOnly Date, DateTime At, string? Reason, bool Reversed);

/// <summary>Una orden de giro (§9.2): la cuenta enmascarada en la lista y completa en el detalle.</summary>
public sealed record OrdenDeGiroDto(
    Guid PublicId,
    string Number,
    Guid LoanPublicId,
    string LoanNumber,
    int Tranche,
    string Form,
    BeneficiarioDeOrdenDto Beneficiary,
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
    IReadOnlyList<ResultadoDeOrdenDto> Results);

/// <summary>El giro nuevo de un crédito pendiente de giro.</summary>
public sealed record GiroNuevoRequest(string Form, BeneficiarioRequest? Beneficiary, string Reason, bool PaidByFile = false);

/// <summary>El crédito anulado (§8.4).</summary>
public sealed record CreditoAnuladoDto(Guid LoanPublicId, string Number, string Status, DateOnly VoidedOn, string VoidReason,
    IReadOnlyList<Guid> PayoffReversalOperationPublicIds);

// --------------------------------------------------------------------------------------- §9.3 archivos de desembolso --

public sealed record ArchivoDeDesembolsoRequest(Guid FormatPublicId, Guid? SourceAccountPublicId, IReadOnlyList<Guid>? OrderPublicIds = null,
    DateOnly? PaymentDate = null, string? Reason = null);

public sealed record CuentaDeOrigenDto(Guid AccountPublicId, string Code, string Name, Guid? BankPublicId, string? BankName, string? BankAccountNumber,
    string? BankTransferCode);

/// <summary>Una orden que no entra al archivo, con su motivo (<c>BankCodeMissing</c>, <c>NoBankAccount</c>, <c>AlreadyInFile</c> o el campo).</summary>
public sealed record PendienteDeArchivoDto(Guid OrderPublicId, string OrderNumber, string Reason, string Detail, decimal NetAmount);

public sealed record VistaPreviaDeArchivoDto(string FileName, IReadOnlyList<string> Lines, IReadOnlyList<PendienteDeArchivoDto> Pending, decimal Total);

public sealed record ArchivoDeDesembolsoDto(
    Guid PublicId,
    string Number,
    Guid FormatPublicId,
    string FormatCode,
    string? BankName,
    string SourceAccountMasked,
    DateOnly PaymentDate,
    int Orders,
    decimal Total,
    string Sha256,
    string Status,
    bool HasResults,
    DateTime GeneratedAt,
    string FileName,
    string? VoidReason);

public sealed record ArchivoGeneradoDto(ArchivoDeDesembolsoDto File, IReadOnlyList<PendienteDeArchivoDto> Pending);

// ------------------------------------------------------------------------------------------ §6 garantías y pagaré --

public sealed record DuenoDeGarantiaDto(string Kind, Guid PublicId, string? Number);

/// <summary>Una garantía (§6.1).</summary>
public sealed record GarantiaDto(
    Guid PublicId,
    string Kind,
    string Status,
    DuenoDeGarantiaDto Owner,
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
    public IReadOnlyList<Guid> Documents { get; init; } = [];

    /// <summary>La matrícula o el registro de la garantía.</summary>
    public string? RegistrationReference { get; init; }

    /// <summary>K6b: el grado de una hipoteca.</summary>
    public byte? MortgageRank { get; init; }
}

/// <summary>Registrar o editar una garantía real o de fondo (§6.1).</summary>
public sealed record GarantiaRequest
{
    public string OwnerKind { get; init; } = "Application";
    public Guid OwnerPublicId { get; init; }
    public string Kind { get; init; } = "Pledge";
    public string Description { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public decimal? AdmissibleValue { get; init; }
    public DateOnly? Date { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public string? PolicyNumber { get; init; }
    public DateOnly? PolicyExpiresOn { get; init; }
    public string? RegistrationReference { get; init; }

    /// <summary>K6b: el grado de una hipoteca (1 = primer grado, el que exige el desembolso de vivienda).</summary>
    public byte? MortgageRank { get; init; }

    public decimal? FundCoverage { get; init; }
}

public sealed record EstadoDeGarantiaRequest(string Status, string Reason, Guid? SupportAttachmentPublicId = null);

/// <summary>Un pagaré (§6.2).</summary>
public sealed record PagareDto(
    Guid PublicId,
    string Form,
    string Status,
    DuenoDeGarantiaDto Owner,
    string? Number,
    string? ChannelCode,
    string? ExternalNoteId,
    bool Simulated,
    bool Perfected,
    string? Custody)
{
    public DateTime? SignedAt { get; init; }
    public DateTime? InCustodyAt { get; init; }
    public bool SignatureRejected { get; init; }
    public IReadOnlyList<Guid> Documents { get; init; } = [];
}

public sealed record PagareRequest(string OwnerKind, Guid OwnerPublicId, string Form, string? PhysicalNumber = null, string? CustodyLocation = null);

public sealed record CustodiaDePagareRequest(DateTime SignedAt, string CustodyLocation, Guid AttachmentPublicId);

// -------------------------------------------------------------------------------------- §20 tesorería de prueba --

public sealed record DoblesDisponiblesDto(bool Treasury, bool Collector);

public sealed record OrdenEnTesoreriaDePruebaDto(
    Guid OrderPublicId,
    string OrderNumber,
    string LoanNumber,
    string Form,
    string Beneficiary,
    decimal NetAmount,
    string Status,
    bool PaidByFile,
    bool VoidRequested,
    string? VoidReason,
    Guid? OrderMessagePublicId,
    IReadOnlyList<string> Results);

public sealed record ResultadoDePruebaRequest(string Outcome, string Reference, DateOnly? Date = null, decimal? Amount = null, string? Reason = null,
    string? CheckNumber = null);

public sealed record ResultadoDePruebaEmitidoDto(Guid MessagePublicId, Guid OrderPublicId, string Outcome);
