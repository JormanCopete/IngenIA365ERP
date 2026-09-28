using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Core.PaymentMeans;

/// <summary>
/// Las condiciones de crédito que propone un medio de clase crédito (contracts/api.md §22.1 <c>creditDefaults</c>; T32): plazo,
/// cuotas, periodicidad y la línea de Cartera sugerida como texto, sin llave a <c>LND_*</c>. <see cref="MaxTermDays"/> y
/// <see cref="DefaultInstallments"/> son opcionales (nuevo): sin ellos, el plazo propuesto es el máximo y las cuotas propuestas
/// son las máximas.
/// </summary>
public sealed record CreditDefaultsInput(
    short TermDays,
    short MaxInstallments,
    short PeriodicityDays,
    string? SuggestedLineCode = null,
    short? MaxTermDays = null,
    short? DefaultInstallments = null);

/// <summary>
/// El cuerpo de <c>POST</c> y <c>PUT /api/core/payment-means</c> (feature 012, I3, T590; contracts/api.md §22.1). En la edición
/// <see cref="Code"/> se ignora (no cambia nunca, T27) y <see cref="Class"/> sólo cambia en un medio sin pagos. Sin
/// <see cref="CountMethod"/>, el arqueo sale de la clase (<c>ClasesDeMedio.ArqueoPorDefecto</c>); sin
/// <see cref="UniqueReference"/>, es verdadero sólo en <c>Voucher</c>. Las tres marcas «todos» de disponibilidad viven aquí;
/// los conjuntos explícitos, en <c>/api/inventory/payment-means/{id}/availability</c> (§22.3). (nuevo)
/// </summary>
public sealed record PaymentMeansInput
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public short DisplayOrder { get; init; }
    public string? QuickKey { get; init; }
    public PaymentMeansClass Class { get; init; }
    public Guid? CardNetworkPublicId { get; init; }
    public Guid? CardAcquirerPublicId { get; init; }
    public Guid? BankPublicId { get; init; }
    public string? DestinationAccountNumber { get; init; }

    /// <summary>1 ahorros, 2 corriente.</summary>
    public byte? DestinationAccountType { get; init; }

    public bool RequiresReference { get; init; }
    public PaymentReferenceKind? ReferenceKind { get; init; }
    public byte? ReferenceMinLength { get; init; }
    public byte? ReferenceMaxLength { get; init; }
    public bool AllowsChange { get; init; }
    public bool AllowsPartial { get; init; } = true;
    public bool? UniqueReference { get; init; }
    public CashCountMethod? CountMethod { get; init; }
    public bool RequiresTerminalBatchAtClose { get; init; }
    public decimal ToleranceAmount { get; init; }

    /// <summary>Fracción (0,025 = 2,5 %).</summary>
    public decimal? ExpectedCommissionRate { get; init; }

    public decimal? ExpectedCommissionFixed { get; init; }
    public string DianPaymentMeansCode { get; init; } = string.Empty;
    public CreditDefaultsInput? CreditDefaults { get; init; }
    public bool OfferedAtAllPoints { get; init; } = true;
    public bool OfferedOnAllChannels { get; init; } = true;
    public bool OfferedForAllDocumentTypes { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Las condiciones de crédito de un medio, como salen (§22.1). (nuevo)</summary>
public sealed record CreditDefaultsDto(short? TermDays, short? MaxTermDays, short? MaxInstallments, short? DefaultInstallments, short? PeriodicityDays, string? SuggestedLineCode);

/// <summary>
/// Dónde se ofrece un medio (contracts/api.md §22.3): las tres marcas «todos» del medio y los tres conjuntos explícitos del
/// módulo. Un conjunto vacío no es «todos». (nuevo)
/// </summary>
public sealed record PaymentMeansAvailabilityDto(
    bool OfferedAtAllPoints,
    IReadOnlyList<Guid> PointOfSalePublicIds,
    bool OfferedOnAllChannels,
    IReadOnlyList<Guid> SalesChannelPublicIds,
    bool OfferedForAllDocumentTypes,
    IReadOnlyList<Guid> DocumentTypePublicIds);

/// <summary>Un medio de pago (§22.1). <see cref="PaymentsCount"/> y <see cref="Availability"/> sólo en el detalle. (nuevo)</summary>
public sealed record PaymentMeansDto(
    Guid PaymentMeansPublicId,
    string Code,
    string Name,
    short DisplayOrder,
    string? QuickKey,
    PaymentMeansClass Class,
    Guid? CardNetworkPublicId,
    string? CardNetworkCode,
    Guid? CardAcquirerPublicId,
    string? CardAcquirerCode,
    Guid? BankPublicId,
    string? DestinationAccountNumber,
    byte? DestinationAccountType,
    bool RequiresReference,
    PaymentReferenceKind? ReferenceKind,
    byte? ReferenceMinLength,
    byte? ReferenceMaxLength,
    bool AllowsChange,
    bool AllowsPartial,
    bool UniqueReference,
    CashCountMethod CountMethod,
    bool RequiresTerminalBatchAtClose,
    decimal ToleranceAmount,
    decimal? ExpectedCommissionRate,
    decimal? ExpectedCommissionFixed,
    string DianPaymentMeansCode,
    CreditDefaultsDto? CreditDefaults,
    bool OfferedAtAllPoints,
    bool OfferedOnAllChannels,
    bool OfferedForAllDocumentTypes,
    bool IsActive,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Notes)
{
    public int? PaymentsCount { get; init; }

    public PaymentMeansAvailabilityDto? Availability { get; init; }
}

/// <summary>Una franquicia o red de tarjetas (§22.2). (nuevo)</summary>
public sealed record CardNetworkDto(Guid CardNetworkPublicId, string Code, string Name, CardKind CardKind, bool IsActive);

/// <summary>Un adquirente (§22.2); <see cref="PersonPublicId"/> es el tercero de la cuenta por cobrar. (nuevo)</summary>
public sealed record CardAcquirerDto(Guid CardAcquirerPublicId, string Code, string Name, Guid? PersonPublicId, bool IsActive);

/// <summary>Un datáfono de cobro (§22.2); el código es el TER del voucher. (nuevo)</summary>
public sealed record CardTerminalDto(Guid CardTerminalPublicId, string Code, Guid CardAcquirerPublicId, string CardAcquirerCode, string? Serial, string? Description, bool IsActive);

/// <summary>Un billete o una moneda con su vigencia (§22.2). (nuevo)</summary>
public sealed record CashDenominationDto(Guid CashDenominationPublicId, string Currency, CashDenominationKind Kind, decimal Value, short DisplayOrder, bool IsActive, DateOnly ValidFrom, DateOnly? ValidTo);
