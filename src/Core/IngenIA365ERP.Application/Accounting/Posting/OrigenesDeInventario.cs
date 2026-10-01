namespace IngenIA365ERP.Application.Accounting.Posting;

/// <summary>
/// Los <c>SourceType</c> con que un comprobante de origen <c>INV</c> nombra lo que lo originó (feature 012, I2;
/// contracts/contabilidad.md §3.1, §5.3 y §6; decisiones-transversales §2.16). Viajan en
/// <see cref="AccountingOrigin"/> y los traducen a pantalla <c>EnlacesDeOrigen</c> (Application y Shared). (nuevo)
/// </summary>
public static class OrigenesDeInventario
{
    /// <summary>Un comprobante por documento: <c>SourcePublicId</c> es el documento de inventario.</summary>
    public const string Documento = "InventoryDocument";

    /// <summary>Un comprobante resumido: <c>SourcePublicId</c> es el lote (<c>COR_IntegrationBatches</c>); sus documentos están en <c>ACC_InventoryPostings</c>.</summary>
    public const string LoteResumido = "InventoryPostingBatch";

    /// <summary>Un ajuste de costo atado al producto y no a un documento (reclasificación).</summary>
    public const string Producto = "InventoryProduct";
}
