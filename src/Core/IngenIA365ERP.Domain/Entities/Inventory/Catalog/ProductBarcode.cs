using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Código de barras de un producto (<c>INV_ProductBarcodes</c>; feature 012, T201; FR-020, FR-024; data-model §1.8): único
/// en la cooperativa entre los vivos (<c>UK_INV_ProductBarcodes_Barcode</c>), recortado y en mayúsculas.
/// <see cref="ProductUnitId"/> es el empaque que identifica (nulo = la unidad base): la lectura propone esa unidad y su
/// factor. <see cref="IsPrimary"/> es el que imprime la etiqueta.
/// </summary>
public class ProductBarcode : AuditableEntity
{
    public const int MaxLength = 48;

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public string Barcode { get; set; } = string.Empty;

    public int? ProductUnitId { get; set; }

    public ProductUnit? ProductUnit { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>Recortado y en mayúsculas, como se guarda y como se busca.</summary>
    public static string Normalizar(string? codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant();
}
