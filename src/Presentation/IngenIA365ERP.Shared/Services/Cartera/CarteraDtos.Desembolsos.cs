namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US3 (T0458): los espejos de contracts/api.md §6 (garantías y pagaré), §8.4 (anulación del crédito pendiente de giro), §9
// (desembolso, órdenes de giro y archivos) y §20 (tesorería de prueba). Las clases, formas y estados viajan como texto (su nombre en el
// dominio); la pantalla los traduce con TextosDeDesembolso. (nuevo)

// --------------------------------------------------------------------------------------------- §9.1 desembolso --

/// <summary>El beneficiario del giro: la persona y, en transferencia, su cuenta (<c>accountType</c> <c>Savings</c> o <c>Checking</c>).</summary>
public sealed record BeneficiarioRequest(Guid PersonPublicId, Guid? BankPublicId = null, string? AccountType = null, string? AccountNumber = null);

/// <summary>
/// <c>DisbursementInput</c> (§9.1) con <c>acknowledgeWarnings</c>. 2026-11-27 (feature 028, T042; api.md §3): ya no lleva los créditos que se recogen —salen de
/// la solicitud, todas sus recogidas autorizadas (D9)— y gana <see cref="AcknowledgedPayoffDifferences"/>.
/// </summary>
public sealed record DesembolsoRequest
{
    public Guid ApplicationPublicId { get; init; }
    public Guid? LoanPublicId { get; init; }
    public decimal? TrancheAmount { get; init; }
    public DateOnly? ValueDate { get; init; }
    public string Form { get; init; } = "Transfer";
    public BeneficiarioRequest? Beneficiary { get; init; }
    public bool PaidByFile { get; init; }
    public string? ExpectedPreviewSha256 { get; init; }
    public bool AcknowledgeWarnings { get; init; }

    /// <summary>2026-11-27 (feature 028): quien desembolsa revisó las diferencias de lo que se recoge y los productos que se omiten. (nuevo)</summary>
    public bool AcknowledgedPayoffDifferences { get; init; }
}

public sealed record GarantiaPendienteDto(string Kind, string Reason);

public sealed record UsuraDelDesembolsoDto(decimal Rate, decimal? Usury, bool Passes, Guid? UsuryValuePublicId);

public sealed record AprobacionDelDesembolsoDto(DateOnly? ExpiresAt, bool Valid);

public sealed record GarantiasDelDesembolsoDto(bool Perfected, IReadOnlyList<GarantiaPendienteDto> Pending);

/// <summary>
/// Un producto que recoge el desembolso, a la fecha valor. 2026-11-27 (feature 028, T042; api.md §3): con lo proyectado en la solicitud, lo de ahora, lo que
/// se aplica, la diferencia (ahora − proyectado) por concepto con sus causas, si se omite (ya cancelado), si pasa de la tolerancia y la cuota causada.
/// Clase, modo y causas viajan como texto (<c>Loan</c>, <c>Total</c>/<c>Partial</c>, <c>Payments</c>…).
/// </summary>
public sealed record RecogidaPropuestaDto(Guid LoanPublicId, string Number, decimal ToPayOff, int DaysPastDue, bool RequiresRestructuring)
{
    public string Kind { get; init; } = "Loan";
    public Guid ProductPublicId { get; init; }
    public string Mode { get; init; } = "Total";
    public string RequestedMode { get; init; } = "Total";

    /// <summary>Nulo si la solicitud no traía proyección (no hay con qué comparar).</summary>
    public decimal? Projected { get; init; }

    public decimal Current { get; init; }
    public decimal ToApply { get; init; }

    /// <summary>Lo de ahora menos lo proyectado; nulo sin proyección.</summary>
    public decimal? Difference { get; init; }

    public IReadOnlyList<string> Causes { get; init; } = [];

    /// <summary>La diferencia por concepto (<c>principal</c>, <c>currentInterest</c>…), sólo los que cambiaron.</summary>
    public IReadOnlyDictionary<string, decimal> Detail { get; init; } = new Dictionary<string, decimal>();

    public bool Omitted { get; init; }
    public bool AboveTolerance { get; init; }
    public string? InterestMode { get; init; }
    public CuotaCausadaDelDesembolsoDto? CurrentInstallment { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, T051): el interés de lo de ahora explicado renglón a renglón (vacío en un omitido o un abono).</summary>
    public IReadOnlyList<RenglonDeInteresesDto> InterestExplanation { get; init; } = [];

    /// <summary>2026-11-27 (feature 028, T051): lo reconocido al inicio del ciclo y no causado que se libera (informativo).</summary>
    public decimal RecognizedReleased { get; init; }
}

/// <summary>
/// 2026-11-27 (feature 028): la cuota causada del crédito recogido y qué se hace con ella (<c>Include</c> o <c>Exclude</c>). 2026-11-27 (feature 028, E2,
/// T066): <see cref="Left"/> es lo que la cuota que se deja a su canal debe al vencer, con la cuota enviada y la explicación. (nuevo)
/// </summary>
public sealed record CuotaCausadaDelDesembolsoDto(int Number, DateOnly? DueDate, decimal Amount, string Treatment, decimal? Left)
{
    public CuotaEnviadaDto? Sent { get; init; }
    public string? Explanation { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada por el dueño): con <see cref="Left"/>, la regla con que se deja (<c>TalComoSeEnvio</c> o <c>Proporcional</c>, dentro
    /// del tope legal del prepago), su motivo y lo que la cuota debería tal como se envió. (nuevo)
    /// </summary>
    public string? LeftRule { get; init; }
    public string? LeftRuleReason { get; init; }
    public decimal? LeftAsSent { get; init; }

    /// <summary>¿Se deja a su canal? («todo menos la cuota causada»).</summary>
    public bool SeDeja => Treatment == "Exclude";

    /// <summary>«Cuota 7 (vence el 30/11/2026) por $ 312.400: se deja a su canal, como se envió.»</summary>
    public string Texto =>
        $"Cuota causada {Number}{(DueDate is { } vence ? $" (vence el {vence:dd/MM/yyyy})" : "")} por {TextosDeRecogida.Pesos(SeDeja ? Left ?? Amount : Amount)}: "
        + (SeDeja ? "se deja a su canal, como se envió." : "se incluye en la recogida.");
}

/// <summary>2026-11-27 (feature 028, <c>payoffDifferences</c>): cuántos productos difieren o se omiten, el total y si confirmar exige reconocerlo. (nuevo)</summary>
public sealed record DiferenciasDeRecogidasDto(int Count, decimal Total, bool RequiresAcknowledgement, bool AboveTolerance);

public sealed record CuotaPropuestaDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Total)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración de la cuota, aparte del seguro.</summary>
    public decimal Administration { get; init; }
}

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
    string PreviewSha256)
{
    /// <summary>2026-11-27 (feature 028): el resumen de las diferencias de lo que se recoge. (nuevo)</summary>
    public DiferenciasDeRecogidasDto PayoffDifferences { get; init; } = new(0, 0m, false, false);

    /// <summary>Lo que se aplica a los productos que no se omiten (bruto − neto, sin el aporte de una compra con descuento).</summary>
    public decimal Recogido => Payoffs.Where(p => !p.Omitted).Sum(p => p.ToApply);
}

/// <summary>Un producto recogido: lo aplicado y su comprobante. 2026-11-27 (feature 028): con modo, proyectado, actual, diferencia y causas.</summary>
public sealed record RecogidaAplicadaDto(Guid LoanPublicId, string Number, decimal Applied, Guid ReceiptOperationPublicId)
{
    public string Kind { get; init; } = "Loan";
    public string Mode { get; init; } = "Total";
    public decimal? Projected { get; init; }
    public decimal Current { get; init; }
    public decimal? Difference { get; init; }
    public IReadOnlyList<string> Causes { get; init; } = [];
    public decimal? LeftInstallment { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T066): el número de la cuota causada que se dejó a su canal.</summary>
    public int? LeftInstallmentNumber { get; init; }
}

/// <summary>2026-11-27 (feature 028, FR-012): un producto autorizado que ya estaba cancelado al desembolsar: no se recogió. (nuevo)</summary>
public sealed record RecogidaOmitidaDto(Guid LoanPublicId, string Number, decimal? Projected, string Reason)
{
    /// <summary>2026-11-27 (feature 028, E3, T081): <c>Loan</c> o <c>Account</c>. (nuevo)</summary>
    public string Kind { get; init; } = "Loan";
}

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
    IReadOnlyList<string> Warnings)
{
    /// <summary>2026-11-27 (feature 028): los productos autorizados que ya estaban cancelados y no se recogieron. (nuevo)</summary>
    public IReadOnlyList<RecogidaOmitidaDto> OmittedPayoffs { get; init; } = [];

    /// <summary>Lo aplicado a todos los productos recogidos.</summary>
    public decimal Recogido => Payoffs.Sum(p => p.Applied);
}

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

    /// <summary>2026-11-27 (feature 028, FR-027d): «Por liberar al girar el crédito …» si un desembolso sin girar canceló su crédito. (nuevo)</summary>
    public string? ReleaseNotice { get; init; }
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

    /// <summary>2026-11-27 (feature 028, FR-027d): «Por liberar al girar el crédito …» si un desembolso sin girar canceló su crédito. (nuevo)</summary>
    public string? ReleaseNotice { get; init; }
}

public sealed record PagareRequest(string OwnerKind, Guid OwnerPublicId, string Form, string? PhysicalNumber = null, string? CustodyLocation = null);

public sealed record CustodiaDePagareRequest(DateTime SignedAt, string CustodyLocation, Guid AttachmentPublicId);
