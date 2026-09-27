using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Un punto de venta (<c>INV_PointsOfSale</c>; feature 012, I3, T574; FR-058; data-model §15). Fija el canal de sus ventas.
/// <see cref="Code"/> no cambia una vez creado (dimensión <c>PointOfSaleCode</c> de la matriz, T27). Un punto sin POS
/// (<see cref="PosEnabled"/> falso) igual tiene cajas y sesiones para el cobro de oficina en efectivo (T50). Con una sesión
/// abierta no se desactiva (<c>Inventory.PointOfSale.HasOpenSessions</c>).
/// </summary>
public class PointOfSale : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>La sucursal contable (<c>COR_Branches</c>).</summary>
    public int BranchId { get; set; }

    public int SalesChannelId { get; set; }

    public SalesChannel? SalesChannel { get; set; }

    public bool PosEnabled { get; set; }

    /// <summary>De la misma sucursal.</summary>
    public int DefaultWarehouseId { get; set; }

    public Warehouse? DefaultWarehouse { get; set; }

    /// <summary>La ubicación que pide el documento equivalente POS.</summary>
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<CashRegister> CashRegisters { get; set; } = new List<CashRegister>();
}
