using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Entities.Inventory.Purchasing;

/// <summary>
/// La porción de un documento de costos adicionales (<c>LandedCost</c>) que le tocó a una línea de recepción
/// (<c>INV_LandedCostAllocations</c>; feature 012, US13, T778; data-model §9.7; FR-046, US13-3): la base según el método, lo
/// asignado, el residuo del redondeo que recibió, la proporción en existencia (D5) y las porciones a inventario y a costo de venta.
/// <para>
/// Nace de <see cref="Prorrateo"/> con <see cref="Desde"/>. <see cref="AllocationMethod"/> <b>(nuevo)</b>: data-model nombra el
/// método pero no dice dónde vive; se guarda por fila para no tocar <c>INV_Documents</c>. Invariantes:
/// <c>ExistingAmount + SoldAmount = AllocatedAmount</c> en cada fila (<see cref="Desde"/>) y Σ <c>AllocatedAmount</c> =
/// <c>Subtotal</c> del documento (<see cref="VerificarSuma"/>). <c>UK (DocumentId, ReceiptLineId)</c> entre vivas.
/// </para>
/// </summary>
public class LandedCostAllocation : AuditableEntity
{
    /// <summary>El documento <c>LandedCost</c>.</summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// (nuevo, I5, T799) El documento, para que el borrador escriba la propuesta del reparto antes de tener Id (T42e). Sólo navegación:
    /// no cambia la tabla.
    /// </summary>
    public Documents.InventoryDocument? Document { get; set; }

    public int ReceiptDocumentId { get; set; }

    public int ReceiptLineId { get; set; }

    public int ProductId { get; set; }

    /// <summary>(nuevo) El método con que se repartió.</summary>
    public LandedCostAllocationMethod AllocationMethod { get; set; }

    /// <summary>Pesos, cantidad, kg o litros de la línea según el método; en <c>Manual</c>, lo digitado.</summary>
    public decimal Basis { get; set; }

    public decimal AllocatedAmount { get; set; }

    /// <summary>0 salvo en la línea que recibió el residuo.</summary>
    public decimal RoundingResidue { get; set; }

    /// <summary><c>mín(1, existencia actual / cantidad recibida)</c> del ámbito (D5).</summary>
    public decimal ExistingRatio { get; set; }

    /// <summary>A inventario.</summary>
    public decimal ExistingAmount { get; set; }

    /// <summary>A costo de venta.</summary>
    public decimal SoldAmount { get; set; }

    /// <summary>La fila de la porción de una línea de recepción.</summary>
    public static LandedCostAllocation Desde(int documentId, int receiptDocumentId, LandedCostAllocationMethod metodo, RepartoDeLinea reparto)
    {
        ArgumentNullException.ThrowIfNull(reparto);
        if (reparto.ExistingAmount + reparto.SoldAmount != reparto.AllocatedAmount)
            throw new InvalidOperationException("La porción a inventario más la de costo de venta tiene que ser lo asignado.");

        return new LandedCostAllocation
        {
            DocumentId = documentId,
            ReceiptDocumentId = receiptDocumentId,
            ReceiptLineId = reparto.ReceiptLineId,
            ProductId = reparto.ProductId,
            AllocationMethod = metodo,
            Basis = reparto.Basis,
            AllocatedAmount = reparto.AllocatedAmount,
            RoundingResidue = reparto.RoundingResidue,
            ExistingRatio = reparto.ExistingRatio,
            ExistingAmount = reparto.ExistingAmount,
            SoldAmount = reparto.SoldAmount,
        };
    }

    /// <summary>Σ <c>AllocatedAmount</c> de las filas vivas de un documento = su <c>Subtotal</c>, exacto.</summary>
    public static void VerificarSuma(IEnumerable<LandedCostAllocation> filas, decimal subtotal)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var suma = filas.Where(f => !f.IsDeleted).Sum(f => f.AllocatedAmount);
        if (suma != subtotal)
            throw new InvalidOperationException($"Lo repartido ({suma}) no es el subtotal del documento ({subtotal}).");
    }
}
