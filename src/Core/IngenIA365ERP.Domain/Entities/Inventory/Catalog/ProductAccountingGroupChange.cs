using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Un cambio del grupo contable de un producto (<c>INV_ProductAccountingGroupChanges</c>; feature 012, T201; FR-027;
/// data-model §1.10): desde <see cref="EffectiveDate"/>, la existencia (<see cref="Quantity"/>, <see cref="Value"/>,
/// desglosada por bodega en <see cref="DetailJson"/>) pasa de un grupo al otro. Lo escribe sólo
/// <c>ChangeProductAccountingGroupCommand</c> (US3) y es el origen de «GrupoContableReclasificado». Es un <b>hecho</b>
/// (<see cref="IHechoInmutable"/>): una equivocación se corrige con otro cambio, no editando éste.
/// </summary>
public class ProductAccountingGroupChange : AuditableEntity, IHechoInmutable
{
    public int ProductId { get; init; }

    public Product? Product { get; init; }

    public int FromAccountingGroupId { get; init; }

    public int ToAccountingGroupId { get; init; }

    public DateOnly EffectiveDate { get; init; }

    public decimal Quantity { get; init; }

    public decimal Value { get; init; }

    /// <summary><c>[{ warehouseId, warehouseCode, quantity, value }]</c>, el desglose que viaja en el mensaje.</summary>
    public string DetailJson { get; init; } = "[]";

    public string Reason { get; init; } = string.Empty;
}
