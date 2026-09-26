using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Una línea del documento genérico (<c>INV_DocumentLines</c>; feature 012, T17; data-model §5.4). Id <c>int</c>: a 5.000
/// documentos diarios de 10 líneas quedan más de cien años antes del tope. La cantidad va siempre positiva (el signo
/// lo pone la clase). Al confirmar sólo se escriben <see cref="UnitCost"/>/<see cref="TotalCost"/> de las salidas y la
/// <see cref="LocationId"/> por defecto, en la misma transacción; después la línea es tan fija como su documento
/// (lo hace cumplir <c>ApplicationDbContext.SaveChangesAsync</c>). <c>ListPrice</c>/<c>PriceListId</c> los agrega I3.
/// </summary>
public class InventoryDocumentLine : AuditableEntity
{
    public int DocumentId { get; set; }

    public InventoryDocument? Document { get; set; }

    public int LineNumber { get; set; }

    public int ProductId { get; set; }

    /// <summary>La unidad base del producto o una de sus alternas (<c>INV_UnitsOfMeasure</c>).</summary>
    public int UnitId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Copiado de la unidad al guardar; 1 en la base.</summary>
    public decimal Factor { get; set; } = 1m;

    public decimal QuantityBase { get; set; }

    /// <summary><c>Quantity × Factor − QuantityBase</c>, visible (FR-017).</summary>
    public decimal RoundingQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>Copia de <c>INV_PriceLists.IncludesTaxes</c> de la lista aplicada; se usa desde I3.</summary>
    public bool ListPriceIncludesTaxes { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal NetAmount { get; set; }

    public decimal? UnitCost { get; set; }

    public decimal? TotalCost { get; set; }

    public int? LocationId { get; set; }

    public int? ToLocationId { get; set; }

    /// <summary>Sin FK hasta I6 (data-model §3.0).</summary>
    public int? LotId { get; set; }

    /// <summary>Sin FK hasta I6 (data-model §3.0).</summary>
    public int? SerialId { get; set; }

    public int? AdjustmentCauseId { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Nota del proveedor (US9, T342; api.md §14.5): la línea cambia el precio de lo recibido y deja un ajuste de costo
    /// <c>PriceDifference</c> sobre la recepción. Falso en toda otra clase. (nuevo)
    /// </summary>
    public bool AffectsCost { get; set; }
}
