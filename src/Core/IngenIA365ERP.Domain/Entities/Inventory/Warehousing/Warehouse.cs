using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

/// <summary>
/// Bodega de una sucursal contable (<c>INV_Warehouses</c>; feature 012, T202; FR-032 a FR-034, FR-089 a FR-091;
/// data-model §2.2). <see cref="Code"/> es inmutable (dimensión <c>WarehouseCode</c> de la matriz, T27) y la sucursal
/// no cambia. <see cref="Behavior"/> <b>(nuevo)</b> es copia inmutable del de su tipo: la usa el índice de una sola bodega
/// de tránsito por sucursal y las reglas de clase sin unir tablas. Nace <see cref="WarehouseActivationStatus.NotActivated"/>:
/// sólo admite su saldo inicial hasta que <c>ActivateWarehouseCommand</c> (US4) la active.
/// </summary>
public class Warehouse : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>La sucursal contable (<c>COR_Branches</c>, la de la 009).</summary>
    public int BranchId { get; set; }

    public int WarehouseTypeId { get; set; }

    public WarehouseType? WarehouseType { get; set; }

    public WarehouseBehavior Behavior { get; set; } = WarehouseBehavior.Operational;

    public WarehouseActivationStatus ActivationStatus { get; set; } = WarehouseActivationStatus.NotActivated;

    /// <summary>La víspera de la activación: la fecha del saldo inicial.</summary>
    public DateOnly? CutoffDate { get; set; }

    public DateTime? ActivatedAt { get; set; }

    /// <summary><c>SEC_Users.Id</c> de quien la activó.</summary>
    public int? ActivatedByUserId { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<WarehouseLocation> Locations { get; set; } = new List<WarehouseLocation>();

    public bool EsTransito => Behavior == WarehouseBehavior.Transit;
}
