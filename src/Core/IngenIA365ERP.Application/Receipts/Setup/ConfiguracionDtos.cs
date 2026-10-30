using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Application.Receipts.Setup;

// Feature 015, US1 (T0072; contracts/api.md §2): lo que la configuración de Recaudo devuelve. Sólo PublicId y códigos; los enums salen como número.

public sealed record CollectionPointDto(
    Guid PublicId,
    Guid BranchPublicId,
    string BranchName,
    string Code,
    string Name,
    ReceiptsCatalogStatus Status,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? DocumentPrefix,
    bool IsMainCashBoxVault,
    int CashBoxCount);

public sealed record CashBoxDto(
    Guid PublicId,
    Guid CollectionPointPublicId,
    string CollectionPointCode,
    string Code,
    string Name,
    CashBoxBaseMode BaseMode,
    decimal BaseAmount,
    decimal? CashWarnCap,
    decimal? CashBlockCap,
    decimal? DailyCashOutCap,
    bool IsMainCashBox,
    ReceiptsCatalogStatus Status,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    int ActiveCashiers);

public sealed record CashierDto(Guid UserPublicId, string UserName, Guid PersonPublicId, string PersonName, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record PointPaymentMeansDto(
    Guid PaymentMeansPublicId,
    string Code,
    string Name,
    PaymentMeansClass Class,
    CashCountMethod CountMethod,
    decimal ToleranceAmount,
    bool AllowsChange,
    bool RequiresReference,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive);

public sealed record ReceiptDocumentTypeDto(Guid PublicId, string Code, string Name, ReceiptDocumentKind Kind, bool UsesPointPrefix, ReceiptsCatalogStatus Status);

public sealed record MiscConceptDto(
    Guid PublicId,
    string Code,
    string Name,
    MiscConceptDirection Direction,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresInvoice,
    bool RequiresApproval,
    decimal? MaxAmount,
    bool IsSystem,
    ReceiptsCatalogStatus Status);

public sealed record ContingencyBookDto(
    Guid PublicId,
    Guid CollectionPointPublicId,
    string CollectionPointCode,
    int FromNumber,
    int ToNumber,
    string RegisteredBy,
    ContingencyBookState Status,
    DateTime CreatedAt);

public sealed record SupervisorDelegationDto(
    Guid PublicId,
    Guid CollectionPointPublicId,
    string CollectionPointCode,
    Guid SupervisorUserPublicId,
    string SupervisorName,
    Guid DelegateUserPublicId,
    string DelegateName,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Reason);

public sealed record ChannelModalityRuleDto(
    Guid PublicId,
    ReceiptChannel Channel,
    ApplicationTarget Target,
    int? LoanModality,
    int? AccountModality,
    bool OnlyWhenInstallmentsDue,
    int Priority,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

public sealed record VaultCustodianDto(Guid UserPublicId, string UserName, DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record VaultDto(
    Guid PublicId,
    Guid BranchPublicId,
    string BranchName,
    VaultMode Mode,
    Guid? MainCashBoxPublicId,
    string? MainCashBoxCode,
    ReceiptsCatalogStatus Status,
    IReadOnlyList<VaultCustodianDto> Custodians);
