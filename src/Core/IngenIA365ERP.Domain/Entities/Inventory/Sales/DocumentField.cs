using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Sales;

/// <summary>El tipo de dato de un campo adicional de venta. (nuevo)</summary>
public enum DocumentFieldType
{
    Text = 1,
    Number = 2,
    Date = 3,
}

/// <summary>
/// Un campo adicional de venta que define la cooperativa (<c>INV_DocumentFieldDefinitions</c>; 2026-11-15, pedido del dueño: en COOFLOPAL la
/// placa del vehículo). Con su nombre, su tipo y su tamaño; si es obligatorio; en qué documentos se pide (ventas —factura de oficina y POS— y
/// cotizaciones) y si se imprime en la factura (va en las notas del documento electrónico, que la DIAN recibe y la representación gráfica
/// imprime). Cambiarlo no toca lo ya registrado: cada documento sella su copia (<see cref="DocumentFieldValue"/>). (nuevo)
/// </summary>
public class DocumentFieldDefinition : AuditableEntity
{
    public const int LargoDelNombre = 60;
    public const int TamanoMaximo = 200;

    /// <summary>La nomenclatura de la cooperativa (<c>CodigoDeCatalogo</c>), única.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Lo que ve la persona y lo que se imprime («Placa»).</summary>
    public string Name { get; set; } = string.Empty;

    public DocumentFieldType DataType { get; set; } = DocumentFieldType.Text;

    /// <summary>Caracteres máximos del valor (1 a <see cref="TamanoMaximo"/>).</summary>
    public int MaxLength { get; set; } = 20;

    public bool IsRequired { get; set; }

    /// <summary>Se pide en las ventas: factura de oficina, ciclo comercial y POS.</summary>
    public bool ShowInSales { get; set; } = true;

    public bool ShowInQuotes { get; set; }

    /// <summary>Se imprime en la factura (notas del documento electrónico).</summary>
    public bool PrintOnInvoice { get; set; } = true;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// El valor de un campo adicional en un documento (<c>INV_DocumentFieldValues</c>; 2026-11-15). Sella el código, el nombre y si se imprime al
/// guardarlo: cambiar la definición después no altera lo ya facturado. Se escribe sólo con el documento en borrador. (nuevo)
/// </summary>
public class DocumentFieldValue : AuditableEntity
{
    /// <summary><c>INV_Documents</c>.</summary>
    public int DocumentId { get; set; }

    public int DefinitionId { get; set; }

    public DocumentFieldDefinition? Definition { get; set; }

    public string FieldCode { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool PrintOnInvoice { get; set; }

    public int SortOrder { get; set; }
}
