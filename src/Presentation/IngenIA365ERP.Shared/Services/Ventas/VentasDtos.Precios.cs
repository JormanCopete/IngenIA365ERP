namespace IngenIA365ERP.Shared.Services.Ventas;

// Espejos de listas de precios, la resolución del precio y los topes de descuento (feature 012, I3, T633; contracts/api.md §19.1–§19.3).
// Los porcentajes de los topes son fracción (0,05 = 5 %). (nuevos)

public sealed record AmbitoDeListaDto(Guid? PersonPublicId, string? PersonName, string? Segment, Guid? SalesChannelPublicId, Guid? BranchPublicId);

public sealed record ListaDePreciosDto(
    Guid PriceListPublicId,
    string Code,
    string Name,
    bool IncludesTaxes,
    AmbitoDeListaDto Scope,
    string ScopeKey,
    int Specificity,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive,
    int ItemCount);

public sealed record PrecioDeListaDto(Guid ItemPublicId, Guid ProductPublicId, string ProductCode, string ProductName, Guid UnitPublicId, string UnitCode, decimal Price);

public sealed record DetalleDeListaDto(ListaDePreciosDto List, IReadOnlyList<PrecioDeListaDto> Items, int Page, int PageSize, long TotalItems);

public sealed record ListaCreadaDto(Guid PriceListPublicId);

public sealed record AmbitoDeListaRequest(Guid? PersonPublicId = null, string? Segment = null, Guid? SalesChannelPublicId = null, Guid? BranchPublicId = null);

/// <summary>El cuerpo de una lista (§19.1). El ámbito, el código e «incluye impuestos» no cambian al editar.</summary>
public sealed record ListaDePreciosRequest(string? Code, string Name, bool IncludesTaxes, AmbitoDeListaRequest? Scope, DateOnly? ValidFrom, DateOnly? ValidTo,
    bool IsActive, string Reason, string? Notes);

public sealed record PrecioDeListaRequest(Guid ProductPublicId, Guid UnitPublicId, decimal Price);

public sealed record PreciosDeListaRequest(IReadOnlyList<PrecioDeListaRequest> Items, IReadOnlyList<Guid>? RemoveProductPublicIds, string Reason);

public sealed record ResultadoDePreciosDto(int Created, int Updated, int Unchanged, int Removed);

public sealed record CandidataDePrecioDto(Guid PriceListPublicId, string Code, IReadOnlyList<string> MatchedDimensions, bool HasProduct);

public sealed record ListaQueGanoDto(Guid PublicId, string Code, string Name, IReadOnlyList<string> MatchedDimensions);

/// <summary>El precio resuelto con la lista que ganó y las candidatas (§19.2).</summary>
public sealed record PrecioResueltoDto(decimal Price, bool IncludesTaxes, ListaQueGanoDto PriceList, bool FallbackUsed, IReadOnlyList<CandidataDePrecioDto> Candidates);

public sealed record TopeDeDescuentoDto(Guid DiscountCapPublicId, Guid RolePublicId, string RoleCode, string RoleName, decimal MaxLinePercent, decimal MaxDocumentPercent,
    DateOnly ValidFrom, DateOnly? ValidTo);

public sealed record RolDelTopeDto(Guid RolePublicId, string RoleCode, string RoleName);

public sealed record MiTopeDto(decimal MaxLinePercent, decimal MaxDocumentPercent, IReadOnlyList<RolDelTopeDto> FromRoles);

public sealed record TopeCreadoDto(Guid DiscountCapPublicId);

// ------------------------------------------------------------------------------------- promociones (I6) --
// Espejos de las promociones (feature 012, I6, T893; contracts/api.md §19.4). Kind y Scopes[].Kind llegan como número
// (PromotionKind, PromotionScopeKind); al guardar la clase viaja por nombre (TextosDeVentas.NombresDeClaseDePromocion). Percent es
// fracción (0,10 = 10 %). (nuevos)

public sealed record AmbitoDePromocionDto(int Kind, Guid? ProductPublicId, string? ProductCode, Guid? CategoryPublicId, string? CategoryCode, string? Segment,
    Guid? SalesChannelPublicId, string? SalesChannelCode, decimal? RequiredQuantity);

public sealed record TramoDePromocionDto(decimal MinQuantity, decimal Price);

public sealed record PromocionDto(
    Guid PromotionPublicId,
    string Code,
    string Name,
    int Kind,
    decimal? Percent,
    decimal? Amount,
    decimal? BuyQuantity,
    decimal? PayQuantity,
    decimal? BundlePrice,
    DateOnly ValidFrom,
    DateOnly ValidTo,
    bool Cumulative,
    bool IsActive,
    string? Notes,
    bool InUse,
    IReadOnlyList<AmbitoDePromocionDto> Scopes,
    IReadOnlyList<TramoDePromocionDto> Tiers);

/// <summary>Un ámbito al guardar: exactamente uno de los cuatro destinos; <see cref="RequiredQuantity"/> sólo con producto en el paquete.</summary>
public sealed record AmbitoDePromocionRequest(Guid? ProductPublicId = null, Guid? CategoryPublicId = null, string? Segment = null,
    Guid? SalesChannelPublicId = null, decimal? RequiredQuantity = null);

public sealed record TramoDePromocionRequest(decimal MinQuantity, decimal Price);

/// <summary>Alta de una promoción (<c>POST /promotions</c>, §19.4).</summary>
public sealed record PromocionRequest(string Code, string Name, string Kind, DateOnly ValidFrom, DateOnly ValidTo, string Reason, decimal? Percent = null,
    decimal? Amount = null, decimal? BuyQuantity = null, decimal? PayQuantity = null, decimal? BundlePrice = null, bool Cumulative = false, bool IsActive = true,
    IReadOnlyList<AmbitoDePromocionRequest>? Scopes = null, IReadOnlyList<TramoDePromocionRequest>? Tiers = null, string? Notes = null);

/// <summary>
/// Edición (<c>PUT /promotions/{id}</c>, §19.4): nombre, fin de vigencia y activo siempre; lo demás, nulo = no cambia, y ya aplicada en un
/// documento confirmado no cambia (<c>Inventory.Promotion.InUse</c>).
/// </summary>
public sealed record EdicionDePromocionRequest(string Name, DateOnly ValidTo, bool IsActive, string Reason, string? Kind = null, decimal? Percent = null,
    decimal? Amount = null, decimal? BuyQuantity = null, decimal? PayQuantity = null, decimal? BundlePrice = null, bool? Cumulative = null,
    DateOnly? ValidFrom = null, IReadOnlyList<AmbitoDePromocionRequest>? Scopes = null, IReadOnlyList<TramoDePromocionRequest>? Tiers = null, string? Notes = null);

public sealed record TopeRequest(Guid RolePublicId, decimal MaxLinePercent, decimal MaxDocumentPercent, DateOnly ValidFrom, DateOnly? ValidTo, string Reason);
