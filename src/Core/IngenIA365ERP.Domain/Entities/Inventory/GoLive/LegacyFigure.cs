using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.GoLive;

/// <summary>
/// Una cifra de SOLIDO (<c>INV_LegacyFigures</c>; feature 012, T305; FR-090, FR-091, SC-018; data-model §6.5): existencia y
/// valor de un producto en una bodega a una fecha, tal como vienen del sistema anterior. <b>Sólo informativa</b>: nunca mueve
/// existencia ni costo (FR-001); alimenta la activación, la conciliación y los comparativos <c>legacy-comparison-*</c>. Cada
/// importación es un lote (<see cref="ImportBatchPublicId"/>); un lote nuevo de la misma (fecha, bodega) da de baja lógica al
/// anterior. Los códigos se guardan crudos y los Id se resuelven si existen: un producto que no está en el catálogo nuevo
/// queda sin <see cref="ProductId"/> y con su <see cref="AccountingGroupId"/>, para sumar al valorizado por grupo.
/// </summary>
public class LegacyFigure : AuditableEntity
{
    public const int LargoDelCodigoDeProducto = 40;
    public const int LargoDelCodigoDeBodega = 20;
    public const int LargoDelArchivo = 260;

    /// <summary>El lote de <c>ImportLegacyFiguresCommand</c>.</summary>
    public Guid ImportBatchPublicId { get; set; }

    /// <summary>Fecha de la cifra (saldo) o fin del rango (movimiento).</summary>
    public DateOnly AsOfDate { get; set; }

    /// <summary>Con valor, la fila trae movimientos del rango para el comparativo de kardex.</summary>
    public DateOnly? FromDate { get; set; }

    public string ProductCodeRaw { get; set; } = string.Empty;

    public string WarehouseCodeRaw { get; set; } = string.Empty;

    public int? ProductId { get; set; }

    public int? WarehouseId { get; set; }

    /// <summary>
    /// <b>(nuevo)</b> El grupo contable de una fila sin producto en el catálogo nuevo (plantillas §15: la fila exige
    /// <c>grupoContable</c>); con producto, el que vino en el archivo si vino.
    /// </summary>
    public int? AccountingGroupId { get; set; }

    /// <summary>Saldo a <see cref="AsOfDate"/>, en unidad base de SOLIDO; puede ser negativo.</summary>
    public decimal? Quantity { get; set; }

    public decimal? Value { get; set; }

    public decimal? QuantityIn { get; set; }

    public decimal? QuantityOut { get; set; }

    public decimal? ValueIn { get; set; }

    public decimal? ValueOut { get; set; }

    public string SourceFileName { get; set; } = string.Empty;
}
