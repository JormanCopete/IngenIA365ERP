using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

// Feature 015, US3 (T0113–T0119; contracts/api.md §4): lo que cruzan las rutas de la caja y el recibo. Toda referencia por PublicId. (nuevo)

/// <summary>Un concepto vario de ingreso que la caja ofrece.</summary>
public sealed record ConceptoVarioPagableDto(Guid MiscConceptPublicId, string Code, string Name, bool RequiresThirdParty, bool RequiresCrossDocument,
    bool RequiresInvoice, decimal? MaxAmount);

/// <summary>Lo que una persona puede pagar: sus obligaciones, sus cuentas, lo exigible de aportes y ahorros, y los conceptos varios (FR-037).</summary>
public sealed record LoQuePuedePagarDto(
    Guid PersonPublicId,
    string PersonName,
    string? TaxId,
    bool IsAssociate,
    ListadoDeObligacionesDto? Obligations,
    CuentasDePersonaDto? Accounts,
    ExigiblesDeCuentasDto? DueInstallments,
    IReadOnlyList<ConceptoVarioPagableDto> MiscConcepts,
    IReadOnlyList<string> Notices);

/// <summary>Lo que el cajero elige pagar.</summary>
public sealed record ItemPedidoDto(
    ApplicationTarget Target,
    decimal Amount,
    Guid? ProductPublicId = null,
    Guid? BeneficiaryPersonPublicId = null,
    ModalidadDePago? LoanModality = null,
    ModalidadDeConsignacion? AccountModality = null,
    EleccionDeAbono? Choice = null,
    int? InstallmentNumber = null,
    IReadOnlyList<Guid>? InstallmentPublicIds = null,
    bool AdvanceNoticeAccepted = false,
    Guid? MiscConceptPublicId = null,
    string? CrossDocument = null,
    Guid? InvoicePublicId = null,
    Guid? ItemId = null);

/// <summary>Una alternativa del abono extraordinario (reducir plazo o cuota) con su nuevo plan.</summary>
public sealed record AlternativaDeAbonoDto(EleccionDeAbono? Choice, decimal AppliedAmount, PlanResumidoDto? NewPlan);

/// <summary>Un ítem de la operación con su última simulación (lo que guarda <c>ItemsJson</c>).</summary>
public sealed record ItemDeCajaDto(
    Guid ItemId,
    ApplicationTarget Target,
    decimal Amount,
    Guid? ProductPublicId,
    string? ProductNumber,
    Guid BeneficiaryPersonPublicId,
    ModalidadDePago? LoanModality,
    ModalidadDeConsignacion? AccountModality,
    EleccionDeAbono? Choice,
    int? InstallmentNumber,
    IReadOnlyList<Guid>? InstallmentPublicIds,
    bool AdvanceNoticeAccepted,
    Guid? MiscConceptPublicId,
    string? CrossDocument,
    Guid? InvoicePublicId,
    string? SimulationId,
    decimal? SimulatedApplied,
    decimal? SimulatedSurplus,
    bool AdvanceWithoutPrincipal,
    bool RequiresSourceOfFunds,
    IReadOnlyList<AlternativaDeAbonoDto> Alternatives,
    IReadOnlyList<string> Notices);

/// <summary>Un medio de la operación.</summary>
public sealed record MedioDeCajaDto(
    Guid PaymentMeansPublicId,
    decimal Amount,
    string? Reference = null,
    Guid? CardTerminalPublicId = null,
    string? TerminalBatch = null,
    string? ApprovalCode = null,
    string? VoucherNumber = null,
    Guid? MeansId = null);

/// <summary>Una porción: lo que un medio cubre de un ítem.</summary>
public sealed record PorcionDeCajaDto(Guid ItemId, Guid MeansId, decimal Amount);

/// <summary>La operación de caja.</summary>
public sealed record CashOperationDto(
    Guid PublicId,
    CashOperationState Status,
    Guid SessionPublicId,
    Guid PayerPersonPublicId,
    Guid? BeneficiaryPersonPublicId,
    Guid? DeliveredByPersonPublicId,
    DateOnly ValueDate,
    IReadOnlyList<ItemDeCajaDto> Items,
    IReadOnlyList<MedioDeCajaDto> Means,
    IReadOnlyList<PorcionDeCajaDto> Imputation,
    bool ImputationFixed,
    decimal TotalItems,
    decimal TotalMeans,
    decimal Change,
    bool SourceOfFundsDeclared,
    Guid? ReceiptPublicId,
    string? ReceiptNumber);

/// <summary>Una aplicación del recibo.</summary>
public sealed record AplicacionDelReciboDto(Guid PublicId, int Ordinal, ApplicationTarget Target, string? ProductNumber, decimal Amount, int? LoanModality,
    int? AccountModality, int? Choice, bool AdvanceNoticeAccepted, string? MiscConceptCode, string? ModuleVoucherNumber, DateOnly? FinalityUntil,
    ApplicationState Status);

/// <summary>Un medio del recibo.</summary>
public sealed record MedioDelReciboDto(Guid PublicId, string MeansCode, decimal Amount, string? Reference, ReceiptMeansState Status);

/// <summary>El recibo; con pagador tercero y <see cref="Masked"/>, sin productos ni saldos del beneficiario.</summary>
public sealed record ReceiptDto(
    Guid PublicId,
    string Number,
    ReceiptState Status,
    ReceiptChannel Channel,
    DateOnly ValueDate,
    DateTime RegisteredAt,
    Guid PayerPersonPublicId,
    string PayerName,
    Guid? DeliveredByPersonPublicId,
    bool PayerIsThirdParty,
    bool Masked,
    decimal TotalApplied,
    decimal TotalMeans,
    decimal ChangeGiven,
    string VerificationCode,
    IReadOnlyList<AplicacionDelReciboDto> Applications,
    IReadOnlyList<MedioDelReciboDto> Means);
