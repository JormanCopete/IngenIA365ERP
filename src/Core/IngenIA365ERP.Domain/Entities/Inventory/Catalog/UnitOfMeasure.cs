using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Unidad de medida (<c>INV_UnitsOfMeasure</c>; feature 012, T199; FR-017, FR-025; data-model §1.1). <see cref="AllowedDecimals"/>
/// (0..4) son los decimales que admite una cantidad expresada en ella; una vez usada sólo sube. <see cref="DianUnitCode"/>
/// es el código UN/ECE Rec. 20 que exige el documento electrónico. Sembrada (<see cref="IsSeeded"/>) no se elimina: se inactiva.
/// </summary>
public class UnitOfMeasure : AuditableEntity
{
    public const int MaxDecimals = 4;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Lo que imprime la tirilla («und», «kg»).</summary>
    public string? Symbol { get; set; }

    public byte AllowedDecimals { get; set; }

    public string? DianUnitCode { get; set; }

    public bool IsSeeded { get; set; }

    public bool IsActive { get; set; } = true;
}
