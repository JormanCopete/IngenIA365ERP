namespace IngenIA365ERP.Shared.Services.Inventario;

// Feature 012, I6, US17 (T969; contracts/api.md §28): el tablero de inventario tal como lo devuelve GET /api/inventory/dashboard.

/// <summary>A dónde lleva una ficha: la página y su query.</summary>
public sealed record EnlaceDeFichaDto
{
    public string Page { get; init; } = "";
    public IReadOnlyDictionary<string, string> Query { get; init; } = new Dictionary<string, string>();

    /// <summary>La ruta con su query, lista para navegar.</summary>
    public string Ruta => Query.Count == 0
        ? Page
        : $"{Page}?{string.Join('&', Query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}"))}";
}

/// <summary>Una ficha: <see cref="Unit"/> es Money, Quantity, Days, Percent o Count; <see cref="Severity"/>, Info, Warning o Critical.</summary>
public sealed record FichaDelTableroDto
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public decimal? Value { get; init; }
    public decimal? PreviousValue { get; init; }
    public string Unit { get; init; } = "";
    public string? Severity { get; init; }
    public EnlaceDeFichaDto Link { get; init; } = new();
}

/// <summary>Una sucursal o bodega del alcance.</summary>
public sealed record ElementoDelTableroDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record AlcanceDelTableroDto
{
    public IReadOnlyList<ElementoDelTableroDto> Branches { get; init; } = [];
    public IReadOnlyList<ElementoDelTableroDto> Warehouses { get; init; } = [];
}

/// <summary>Un tipo de alerta levantado sin destinatario activo (se enrutó al administrador de la empresa).</summary>
public sealed record SinDestinatarioDto
{
    public string AlertTypeCode { get; init; } = "";
}

public sealed record TableroDeInventarioDto
{
    public DateOnly AsOf { get; init; }
    public AlcanceDelTableroDto Scope { get; init; } = new();
    public IReadOnlyList<FichaDelTableroDto> Tiles { get; init; } = [];
    public IReadOnlyList<SinDestinatarioDto> WithoutRecipient { get; init; } = [];
}
