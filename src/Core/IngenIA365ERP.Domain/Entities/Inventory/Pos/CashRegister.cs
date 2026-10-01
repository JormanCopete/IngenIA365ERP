using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Una caja de un punto de venta (<c>INV_CashRegisters</c>; feature 012, I3, T574; FR-058, FR-067; data-model §15). Su bodega es
/// de la sucursal del punto (<c>Inventory.CashRegister.WarehouseBranchMismatch</c>). Los tipos de documento que expide van en
/// <see cref="DocumentTypes"/>, un rol por caja. El datáfono por defecto vive aquí y no en Core.
/// </summary>
public class CashRegister : AuditableEntity
{
    /// <summary>Anchos de tirilla admitidos.</summary>
    public static readonly IReadOnlySet<short> AnchosDeTirilla = new HashSet<short> { 58, 80 };

    public int PointOfSaleId { get; set; }

    public PointOfSale? PointOfSale { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Propuesta: la del punto; de la sucursal del punto.</summary>
    public int WarehouseId { get; set; }

    /// <summary><c>COR_CardTerminals</c>: el que se propone al cobrar con tarjeta.</summary>
    public int? DefaultCardTerminalId { get; set; }

    /// <summary>Placa o serial de la caja que pide el documento equivalente POS.</summary>
    public string? DianCashRegisterPlate { get; set; }

    /// <summary>Tipo de caja del catálogo DIAN (<c>CatalogoDian</c>).</summary>
    public string? DianCashRegisterTypeCode { get; set; }

    public short ReceiptWidthMm { get; set; } = 80;

    public byte PrintCopies { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public ICollection<CashRegisterDocumentType> DocumentTypes { get; set; } = new List<CashRegisterDocumentType>();
}
