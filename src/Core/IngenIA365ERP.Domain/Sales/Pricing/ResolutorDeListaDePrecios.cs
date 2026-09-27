namespace IngenIA365ERP.Domain.Sales.Pricing;

/// <summary>
/// Una dimensión del ámbito de una lista de precios (contracts/api.md §19.2 <c>matchedDimensions</c>). El orden del valor es el
/// del desempate: cliente, segmento, canal, sucursal (T51). No se guarda: la lista guarda sus columnas y su <c>ScopeKey</c>.
/// (nuevo)
/// </summary>
public enum DimensionDePrecio { Person = 1, Segment = 2, Channel = 3, Branch = 4 }

/// <summary>Un precio de una lista: producto, unidad (la base o una alterna de venta) y precio en pesos. (nuevo)</summary>
public sealed record PrecioDeLista(int ProductId, int UnitId, decimal Price);

/// <summary>
/// Una lista que la aplicación le entrega al resolutor con sus dimensiones (nulas = no restringe), su vigencia y sus precios
/// del producto que se busca (bastan ésos). (nuevo)
/// </summary>
public sealed record ListaDePreciosCandidata(
    int PriceListId,
    string Code,
    int? PersonId,
    string? Segment,
    int? SalesChannelId,
    int? BranchId,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool IsActive,
    bool IncludesTaxes,
    IReadOnlyList<PrecioDeLista> Items);

/// <summary>
/// A quién, por dónde y cuándo se vende: la persona (cliente), su segmento (<c>COR_Associates.AssociateClass</c>), el canal (el
/// del punto o el del tipo de documento), la sucursal y la fecha de la operación. Lo nulo no coincide con ninguna lista que
/// exija esa dimensión. (nuevo)
/// </summary>
public sealed record ContextoDePrecio(int? PersonId, string? Segment, int? SalesChannelId, int? BranchId, DateOnly Date);

/// <summary>Una lista aplicable, en el orden de la resolución, con las dimensiones en que coincidió y si trae el producto. (nuevo)</summary>
public sealed record CandidataDePrecio(int PriceListId, string Code, IReadOnlyList<DimensionDePrecio> MatchedDimensions, bool HasProduct);

/// <summary>
/// Lo que resolvió el motor (contracts/api.md §19.2 <c>ResolvedPriceDto</c>). <see cref="Found"/> falso = ninguna lista aplicable
/// trae el producto en esa unidad: la aplicación lo traduce a <c>Inventory.Price.NotFound</c>. <see cref="Price"/> es el de la
/// lista tal cual: con <see cref="IncludesTaxes"/> la línea lo lleva a sin impuestos (data-model §14). (nuevo)
/// </summary>
public sealed record PrecioResuelto(
    bool Found,
    decimal? Price,
    bool IncludesTaxes,
    int? PriceListId,
    string? PriceListCode,
    IReadOnlyList<DimensionDePrecio> MatchedDimensions,
    bool FallbackUsed,
    IReadOnlyList<CandidataDePrecio> Candidates);

/// <summary>
/// El ámbito de una lista de precios normalizado (data-model §14 <c>INV_PriceLists</c>): <c>ScopeKey</c> =
/// <c>P:{PersonId|-}|S:{Segment|-}|C:{SalesChannelId|-}|B:{BranchId|-}</c> y el número de dimensiones no nulas, que ordena la
/// resolución. El segmento se compara en mayúsculas y sin espacios alrededor; en blanco no es dimensión. (nuevo)
/// </summary>
public static class AmbitoDeLista
{
    public const string ClaveGeneral = "P:-|S:-|C:-|B:-";

    public static string? NormalizarSegmento(string? segmento) =>
        string.IsNullOrWhiteSpace(segmento) ? null : segmento.Trim().ToUpperInvariant();

    public static string Clave(int? personId, string? segment, int? salesChannelId, int? branchId) =>
        $"P:{Parte(personId)}|S:{NormalizarSegmento(segment) ?? "-"}|C:{Parte(salesChannelId)}|B:{Parte(branchId)}";

    public static byte Dimensiones(int? personId, string? segment, int? salesChannelId, int? branchId) =>
        (byte)((personId is null ? 0 : 1) + (NormalizarSegmento(segment) is null ? 0 : 1)
            + (salesChannelId is null ? 0 : 1) + (branchId is null ? 0 : 1));

    private static string Parte(int? valor) => valor is { } v ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-";
}

/// <summary>
/// El motor puro que elige la lista de precios de una línea (feature 012, I3, T578; FR-053, T51; data-model §14; contracts/api.md
/// §19.2). Sin IO: recibe las listas candidatas y el contexto.
/// <list type="bullet">
/// <item>candidatas = activas, vigentes a la fecha (los dos extremos incluidos) y con <b>todas</b> sus dimensiones no nulas
/// coincidentes con el contexto;</item>
/// <item>primero la de más dimensiones; en empate, la que tiene la dimensión más fuerte en el orden cliente &gt; segmento &gt;
/// canal &gt; sucursal; la general (sin dimensiones) al final; el código desempata lo demás para que sea determinista;</item>
/// <item>si la primera no trae el producto en esa unidad, la siguiente que lo traiga, con <c>FallbackUsed</c> (F8);</item>
/// <item>si ninguna lo trae, <c>Found = false</c>.</item>
/// </list>
/// Lo usan <c>ResolvePriceQuery</c>, el POS y la factura al agregar la línea, el retiro gravado (la general) y la vista
/// <c>impairment</c>. Lo fijan los casos dorados de <c>Domain.Tests/Sales/Pricing/Casos</c>. (nuevo)
/// </summary>
public static class ResolutorDeListaDePrecios
{
    public static PrecioResuelto Resolver(IEnumerable<ListaDePreciosCandidata> listas, ContextoDePrecio contexto, int productId, int unitId)
    {
        ArgumentNullException.ThrowIfNull(listas);
        ArgumentNullException.ThrowIfNull(contexto);

        var aplicables = listas
            .Where(l => Vigente(l, contexto.Date))
            .Select(l => (Lista: l, Dimensiones: Coincidencias(l, contexto)))
            .Where(x => x.Dimensiones is not null)
            .Select(x => (x.Lista, Dimensiones: x.Dimensiones!))
            .OrderByDescending(x => x.Dimensiones.Count)
            .ThenByDescending(x => Peso(x.Dimensiones))
            .ThenBy(x => x.Lista.Code, StringComparer.Ordinal)
            .ThenBy(x => x.Lista.PriceListId)
            .ToList();

        var candidatas = aplicables
            .Select(x => new CandidataDePrecio(x.Lista.PriceListId, x.Lista.Code, x.Dimensiones, Precio(x.Lista, productId, unitId) is not null))
            .ToList();

        for (var i = 0; i < aplicables.Count; i++)
        {
            var (lista, dimensiones) = aplicables[i];
            if (Precio(lista, productId, unitId) is not { } precio) continue;
            return new PrecioResuelto(true, precio.Price, lista.IncludesTaxes, lista.PriceListId, lista.Code, dimensiones, FallbackUsed: i > 0, candidatas);
        }

        return new PrecioResuelto(false, null, false, null, null, [], false, candidatas);
    }

    /// <summary>Activa y con la fecha dentro de su vigencia (los dos extremos incluidos).</summary>
    public static bool Vigente(ListaDePreciosCandidata lista, DateOnly fecha) =>
        lista.IsActive && lista.ValidFrom <= fecha && (lista.ValidTo is null || lista.ValidTo >= fecha);

    /// <summary>Las dimensiones en que la lista coincide con el contexto, en el orden del desempate; nulo si alguna no coincide.</summary>
    private static IReadOnlyList<DimensionDePrecio>? Coincidencias(ListaDePreciosCandidata lista, ContextoDePrecio contexto)
    {
        var dimensiones = new List<DimensionDePrecio>(4);

        if (lista.PersonId is { } persona)
        {
            if (contexto.PersonId != persona) return null;
            dimensiones.Add(DimensionDePrecio.Person);
        }

        if (AmbitoDeLista.NormalizarSegmento(lista.Segment) is { } segmento)
        {
            if (!string.Equals(AmbitoDeLista.NormalizarSegmento(contexto.Segment), segmento, StringComparison.Ordinal)) return null;
            dimensiones.Add(DimensionDePrecio.Segment);
        }

        if (lista.SalesChannelId is { } canal)
        {
            if (contexto.SalesChannelId != canal) return null;
            dimensiones.Add(DimensionDePrecio.Channel);
        }

        if (lista.BranchId is { } sucursal)
        {
            if (contexto.BranchId != sucursal) return null;
            dimensiones.Add(DimensionDePrecio.Branch);
        }

        return dimensiones;
    }

    /// <summary>Cliente 8, segmento 4, canal 2, sucursal 1: con el mismo número de dimensiones gana la de la dimensión más fuerte.</summary>
    private static int Peso(IReadOnlyList<DimensionDePrecio> dimensiones) =>
        dimensiones.Sum(d => d switch
        {
            DimensionDePrecio.Person => 8,
            DimensionDePrecio.Segment => 4,
            DimensionDePrecio.Channel => 2,
            _ => 1,
        });

    private static PrecioDeLista? Precio(ListaDePreciosCandidata lista, int productId, int unitId) =>
        lista.Items.FirstOrDefault(i => i.ProductId == productId && i.UnitId == unitId);
}
