using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting.Inventory;

/// <summary>
/// Qué tipo de comprobante y qué documento cruce usa una operación de Inventario (<c>ACC_InventoryVoucherMappings</c>;
/// feature 012, T28, T478; data-model §20; contracts/contabilidad.md §2.6 y §9). Una fila por operación y, como
/// excepción, por operación y código de tipo de documento de Inventario. El tipo debe ser <c>Usage = Module</c>,
/// <c>ModuleCode = INV</c> y activo; lo valida quien la guarda (<c>SetInventoryVoucherMappingCommand</c>) y quien la usa.
///
/// <para>
/// Sin vigencia: un cambio vale para lo que se contabilice después y queda en el diff de auditoría. Por eso tipo y cruce
/// se cambian en su sitio; la operación y el tipo de documento, que forman <see cref="MappingKey"/>, no.
/// </para>
/// </summary>
public class InventoryVoucherMapping : AuditableEntity
{
    /// <summary>Lo que va en la clave cuando la fila vale para todo tipo de documento de la operación.</summary>
    public const string Cualquiera = "*";

    /// <summary>Para EF.</summary>
    private InventoryVoucherMapping() { }

    public InventoryVoucherMapping(string operation, string? inventoryDocumentTypeCode, int voucherTypeId, int? crossDocumentTypeId = null)
    {
        if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("La operación es obligatoria.", nameof(operation));
        Operation = operation.Trim();
        InventoryDocumentTypeCode = string.IsNullOrWhiteSpace(inventoryDocumentTypeCode) ? null : inventoryDocumentTypeCode.Trim().ToUpperInvariant();
        VoucherTypeId = voucherTypeId;
        CrossDocumentTypeId = crossDocumentTypeId;
        MappingKey = ClaveDe(Operation, InventoryDocumentTypeCode);
    }

    /// <summary>Código de <c>OperacionesDeInventario</c>.</summary>
    public string Operation { get; private set; } = string.Empty;

    /// <summary>Excepción por tipo de documento de Inventario; nulo = toda la operación.</summary>
    public string? InventoryDocumentTypeCode { get; private set; }

    public int VoucherTypeId { get; set; }

    public VoucherType? VoucherType { get; set; }

    /// <summary>Cruce de las líneas de cuentas por cobrar y por pagar (<c>FV</c>, <c>NC</c>, <c>ND</c>, <c>FC</c>).</summary>
    public int? CrossDocumentTypeId { get; set; }

    public CrossDocumentType? CrossDocumentType { get; set; }

    /// <summary><c>{Operation}|{InventoryDocumentTypeCode|*}</c>; único entre las vivas.</summary>
    public string MappingKey { get; private set; } = string.Empty;

    public static string ClaveDe(string operation, string? inventoryDocumentTypeCode) =>
        operation.Trim() + "|" + (string.IsNullOrWhiteSpace(inventoryDocumentTypeCode) ? Cualquiera : inventoryDocumentTypeCode.Trim().ToUpperInvariant());
}
