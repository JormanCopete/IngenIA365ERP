using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;

namespace IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

/// <summary>
/// Mínimo, máximo y punto de reorden de un producto en una bodega operativa (<c>INV_ReorderPolicies</c>; feature 012,
/// T203; FR-035; data-model §2.4). Nada de lo calculado se guarda: la posición (disponible + en tránsito hacia la bodega +
/// por recibir) la lee <c>PosicionDeReposicion</c> (US2) y las alertas las levanta US17.
/// </summary>
public class ReorderPolicy : AuditableEntity
{
    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public decimal MinimumQuantity { get; set; }

    public decimal MaximumQuantity { get; set; }

    public decimal ReorderPoint { get; set; }

    /// <summary>La regla de la política: <c>0 ≤ mínimo ≤ punto de reorden ≤ máximo</c>.</summary>
    public static bool EsCoherente(decimal minimo, decimal puntoDeReorden, decimal maximo) =>
        minimo >= 0 && minimo <= puntoDeReorden && puntoDeReorden <= maximo;

    /// <summary>Fija los tres valores si son coherentes; si no, no toca nada y devuelve falso.</summary>
    public bool Fijar(decimal minimo, decimal puntoDeReorden, decimal maximo)
    {
        if (!EsCoherente(minimo, puntoDeReorden, maximo)) return false;
        MinimumQuantity = minimo;
        ReorderPoint = puntoDeReorden;
        MaximumQuantity = maximo;
        return true;
    }
}
