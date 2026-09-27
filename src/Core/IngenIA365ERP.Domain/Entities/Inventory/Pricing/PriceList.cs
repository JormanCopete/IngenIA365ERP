using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Sales.Pricing;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pricing;

/// <summary>
/// Una lista de precios (<c>INV_PriceLists</c>; feature 012, I3, T576; FR-053, T51; data-model §14). Su ámbito son dimensiones
/// anulables —persona (cliente), segmento (<c>COR_Associates.AssociateClass</c>, validado contra los existentes), canal y
/// sucursal— normalizadas en <see cref="ScopeKey"/> (único con <see cref="ValidFrom"/> entre vivas; dos listas del mismo ámbito no
/// se cruzan en el tiempo: <c>Inventory.PriceList.Overlaps</c>). El ámbito, el código e <see cref="IncludesTaxes"/> no cambian
/// (<c>Inventory.PriceList.ScopeLocked</c>): un cambio programado es otra lista del mismo ámbito. La resuelve
/// <c>ResolutorDeListaDePrecios</c>; los documentos no dependen de ella después de confirmar.
/// </summary>
public class PriceList : AuditableEntity
{
    public const string MonedaPorDefecto = "COP";

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IncludesTaxes { get; set; }

    /// <summary><c>COR_People</c>: ámbito cliente.</summary>
    public int? PersonId { get; private set; }

    /// <summary>Ámbito segmento (máximo 4, en mayúsculas).</summary>
    public string? Segment { get; private set; }

    /// <summary><c>INV_SalesChannels</c>: ámbito canal.</summary>
    public int? SalesChannelId { get; private set; }

    /// <summary><c>COR_Branches</c>: ámbito sucursal.</summary>
    public int? BranchId { get; private set; }

    /// <summary><c>P:{PersonId|-}|S:{Segment|-}|C:{SalesChannelId|-}|B:{BranchId|-}</c>; la general es <c>P:-|S:-|C:-|B:-</c>.</summary>
    public string ScopeKey { get; private set; } = AmbitoDeLista.ClaveGeneral;

    /// <summary>Cuántas dimensiones no nulas (0..4); ordena la resolución.</summary>
    public byte DimensionCount { get; private set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>COP (FR-018).</summary>
    public string Currency { get; set; } = MonedaPorDefecto;

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public ICollection<PriceListItem> Items { get; set; } = new List<PriceListItem>();

    /// <summary>Fija el ámbito y deriva <see cref="ScopeKey"/> y <see cref="DimensionCount"/>. Sólo al crear.</summary>
    public void FijarAmbito(int? personId, string? segment, int? salesChannelId, int? branchId)
    {
        PersonId = personId;
        Segment = AmbitoDeLista.NormalizarSegmento(segment);
        SalesChannelId = salesChannelId;
        BranchId = branchId;
        ScopeKey = AmbitoDeLista.Clave(personId, segment, salesChannelId, branchId);
        DimensionCount = AmbitoDeLista.Dimensiones(personId, segment, salesChannelId, branchId);
    }
}
