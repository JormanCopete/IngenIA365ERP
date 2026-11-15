using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Purchasing;

/// <summary>
/// La equivalencia entre el código con que un proveedor factura un artículo y el producto de la cooperativa
/// (<c>INV_SupplierItemCodes</c>; compra directa desde el XML, decisión del dueño del 2026-11-14). La compra directa la recuerda cuando
/// la persona elige el producto de una línea del XML con «Recordar para este proveedor», y el prellenado del XML la usa antes que el
/// código de barras, la referencia o el código interno. Una sola viva por proveedor y código; <see cref="ProductUnitId"/> nulo es la
/// unidad base. (nuevo)
/// </summary>
public class SupplierItemCode : AuditableEntity
{
    public const int LargoDelCodigo = 60;

    public int SupplierPersonId { get; set; }

    /// <summary>El código del proveedor normalizado (<see cref="Normalizar"/>).</summary>
    public string ItemCode { get; set; } = string.Empty;

    public int ProductId { get; set; }

    /// <summary>El empaque en que factura el proveedor; nulo = la unidad base del producto.</summary>
    public int? ProductUnitId { get; set; }

    /// <summary>Sin espacios a los lados y en mayúsculas: «abc-1 » y «ABC-1» son el mismo código.</summary>
    public static string Normalizar(string? codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant();
}
