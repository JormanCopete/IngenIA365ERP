namespace IngenIA365ERP.Shared.Services.Core;

// Espejos del catálogo de medios de pago de Core (feature 012, I3, T633; contracts/api.md §22.1–§22.3). Los enums entran como número
// (int) y se mandan por nombre (string) con TextosDeVentas. Sólo PublicId (Principio VI). Nunca hay un número de tarjeta: el medio sólo
// dice si pide referencia y de qué tipo (LosPagosNoGuardanElNumeroDeTarjeta). (nuevos)

public sealed record CreditoPorDefectoDto(short? TermDays, short? MaxTermDays, short? MaxInstallments, short? DefaultInstallments, short? PeriodicityDays,
    string? SuggestedLineCode);

public sealed record MedioDePagoDto(
    Guid PaymentMeansPublicId,
    string Code,
    string Name,
    short DisplayOrder,
    string? QuickKey,
    int Class,
    Guid? CardNetworkPublicId,
    string? CardNetworkCode,
    Guid? CardAcquirerPublicId,
    string? CardAcquirerCode,
    Guid? BankPublicId,
    string? DestinationAccountNumber,
    byte? DestinationAccountType,
    bool RequiresReference,
    int? ReferenceKind,
    byte? ReferenceMinLength,
    byte? ReferenceMaxLength,
    bool AllowsChange,
    bool AllowsPartial,
    bool UniqueReference,
    int CountMethod,
    bool RequiresTerminalBatchAtClose,
    decimal ToleranceAmount,
    decimal? ExpectedCommissionRate,
    decimal? ExpectedCommissionFixed,
    string DianPaymentMeansCode,
    CreditoPorDefectoDto? CreditDefaults,
    bool OfferedAtAllPoints,
    bool OfferedOnAllChannels,
    bool OfferedForAllDocumentTypes,
    bool IsActive,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Notes)
{
    public int? PaymentsCount { get; init; }
}

/// <summary>Los valores por defecto del crédito del medio (§22.1, FR-096).</summary>
public sealed record CreditoPorDefectoRequest(short TermDays, short MaxInstallments, short PeriodicityDays, string? SuggestedLineCode = null,
    short? MaxTermDays = null, short? DefaultInstallments = null);

/// <summary>
/// El medio que se crea o edita (<c>PaymentMeansInput</c>, §22.1). En la edición <see cref="Reason"/> es obligatorio y viaja en el mismo
/// cuerpo (<c>IConMotivo</c>).
/// </summary>
public sealed class MedioDePagoRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public short DisplayOrder { get; set; }
    public string? QuickKey { get; set; }
    public string Class { get; set; } = "Cash";
    public Guid? CardNetworkPublicId { get; set; }
    public Guid? CardAcquirerPublicId { get; set; }
    public Guid? BankPublicId { get; set; }
    public string? DestinationAccountNumber { get; set; }
    public byte? DestinationAccountType { get; set; }
    public bool RequiresReference { get; set; }
    public string? ReferenceKind { get; set; }
    public byte? ReferenceMinLength { get; set; }
    public byte? ReferenceMaxLength { get; set; }
    public bool AllowsChange { get; set; }
    public bool AllowsPartial { get; set; } = true;
    public bool? UniqueReference { get; set; }
    public string? CountMethod { get; set; }
    public bool RequiresTerminalBatchAtClose { get; set; }
    public decimal ToleranceAmount { get; set; }
    public decimal? ExpectedCommissionRate { get; set; }
    public decimal? ExpectedCommissionFixed { get; set; }
    public string DianPaymentMeansCode { get; set; } = string.Empty;
    public CreditoPorDefectoRequest? CreditDefaults { get; set; }
    public bool OfferedAtAllPoints { get; set; } = true;
    public bool OfferedOnAllChannels { get; set; } = true;
    public bool OfferedForAllDocumentTypes { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public string? Notes { get; set; }
    public string? Reason { get; set; }
}

public sealed record MedioCreadoDto(Guid PaymentMeansPublicId);

public sealed record FranquiciaDto(Guid CardNetworkPublicId, string Code, string Name, int CardKind, bool IsActive);

public sealed record AdquirenteDto(Guid CardAcquirerPublicId, string Code, string Name, Guid? PersonPublicId, bool IsActive);

public sealed record DatafonoDto(Guid CardTerminalPublicId, string Code, Guid CardAcquirerPublicId, string CardAcquirerCode, string? Serial, string? Description, bool IsActive);

public sealed record DenominacionDto(Guid CashDenominationPublicId, string Currency, int Kind, decimal Value, short DisplayOrder, bool IsActive, DateOnly ValidFrom,
    DateOnly? ValidTo);

/// <summary>El cuerpo de una franquicia (§22.2); en la edición el código se ignora.</summary>
public sealed record FranquiciaRequest(string? Code, string Name, string CardKind, bool IsActive);

/// <summary>El cuerpo de un adquirente (§22.2); en la edición el código se ignora.</summary>
public sealed record AdquirenteRequest(string? Code, string Name, Guid? PersonPublicId, bool IsActive);

/// <summary>El cuerpo de un datáfono de cobro (§22.2); en la edición el código se ignora.</summary>
public sealed record DatafonoRequest(string? Code, Guid CardAcquirerPublicId, string? Serial, string? Description, bool IsActive);

/// <summary>El cuerpo de una denominación (§22.2): el alta usa tipo, valor y vigencia; la edición el orden, la actividad y el fin de vigencia.</summary>
public sealed record DenominacionRequest(string Kind, decimal Value, DateOnly? ValidFrom, DateOnly? ValidTo, short? DisplayOrder, bool? IsActive, Guid? RetiresPublicId);

/// <summary>Los conjuntos explícitos de un medio (§22.3); las marcas «todos» viven en el medio.</summary>
public sealed record DisponibilidadDeMedioDto(bool OfferedAtAllPoints, IReadOnlyList<Guid> PointOfSalePublicIds, bool OfferedOnAllChannels,
    IReadOnlyList<Guid> SalesChannelPublicIds, bool OfferedForAllDocumentTypes, IReadOnlyList<Guid> DocumentTypePublicIds);

public sealed record DisponibilidadRequest(IReadOnlyList<Guid> PointOfSalePublicIds, IReadOnlyList<Guid> SalesChannelPublicIds, IReadOnlyList<Guid> DocumentTypePublicIds);
