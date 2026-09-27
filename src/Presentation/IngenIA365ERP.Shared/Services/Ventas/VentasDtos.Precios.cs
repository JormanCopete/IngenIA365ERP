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

public sealed record TopeRequest(Guid RolePublicId, decimal MaxLinePercent, decimal MaxDocumentPercent, DateOnly ValidFrom, DateOnly? ValidTo, string Reason);
