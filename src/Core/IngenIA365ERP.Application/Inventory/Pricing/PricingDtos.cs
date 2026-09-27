namespace IngenIA365ERP.Application.Inventory.Pricing;

// Contratos de precios y topes (feature 012, I3, T597–T599; contracts/api.md §19.1–§19.3). (nuevo)

/// <summary>El ámbito de una lista: dimensiones opcionales; todas vacías = la lista general. (nuevo)</summary>
public sealed record PriceListScopeInput(Guid? PersonPublicId = null, string? Segment = null, Guid? SalesChannelPublicId = null, Guid? BranchPublicId = null);

/// <summary>El ámbito visto: con el nombre de la persona. (nuevo)</summary>
public sealed record PriceListScopeDto(Guid? PersonPublicId, string? PersonName, string? Segment, Guid? SalesChannelPublicId, Guid? BranchPublicId);

/// <summary><c>PriceListDto</c> de §19.1; <see cref="Specificity"/> es <c>DimensionCount</c>. (nuevo)</summary>
public sealed record PriceListDto(
    Guid PriceListPublicId,
    string Code,
    string Name,
    bool IncludesTaxes,
    PriceListScopeDto Scope,
    string ScopeKey,
    int Specificity,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive,
    int ItemCount);

/// <summary>Un precio de una lista (§19.1). (nuevo)</summary>
public sealed record PriceListItemDto(Guid ItemPublicId, Guid ProductPublicId, string ProductCode, string ProductName, Guid UnitPublicId, string UnitCode, decimal Price);

/// <summary>El detalle de una lista con sus precios paginados. (nuevo)</summary>
public sealed record PriceListDetailDto(PriceListDto List, IReadOnlyList<PriceListItemDto> Items, int Page, int PageSize, long TotalItems);

/// <summary>Un precio que se agrega o reemplaza por (producto, unidad). (nuevo)</summary>
public sealed record PriceListItemInput(Guid ProductPublicId, Guid UnitPublicId, decimal Price);

/// <summary>Lo que dice <c>PUT /{id}/items</c>: cuántos se crearon, cambiaron, quedaron igual y se quitaron. (nuevo)</summary>
public sealed record PriceListItemsResultDto(int Created, int Updated, int Unchanged, int Removed);

/// <summary>Una lista candidata en la resolución (§19.2). (nuevo)</summary>
public sealed record PriceCandidateDto(Guid PriceListPublicId, string Code, IReadOnlyList<string> MatchedDimensions, bool HasProduct);

/// <summary>La lista que ganó (§19.2). (nuevo)</summary>
public sealed record ResolvedPriceListDto(Guid PublicId, string Code, string Name, IReadOnlyList<string> MatchedDimensions);

/// <summary><c>ResolvedPriceDto</c> de §19.2. (nuevo)</summary>
public sealed record ResolvedPriceDto(decimal Price, bool IncludesTaxes, ResolvedPriceListDto PriceList, bool FallbackUsed, IReadOnlyList<PriceCandidateDto> Candidates);

/// <summary>Un tope de descuento de un rol (§19.3); los topes van como fracción. (nuevo)</summary>
public sealed record DiscountCapDto(
    Guid DiscountCapPublicId,
    Guid RolePublicId,
    string RoleCode,
    string RoleName,
    decimal MaxLinePercent,
    decimal MaxDocumentPercent,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

/// <summary>Un rol del que sale el tope efectivo. (nuevo)</summary>
public sealed record DiscountCapRoleDto(Guid RolePublicId, string RoleCode, string RoleName);

/// <summary><c>GET /discount-caps/mine</c>: el mayor entre los roles; sin fila, 0 (§19.3). (nuevo)</summary>
public sealed record MyDiscountCapDto(decimal MaxLinePercent, decimal MaxDocumentPercent, IReadOnlyList<DiscountCapRoleDto> FromRoles);
