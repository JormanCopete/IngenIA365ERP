using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Purchasing;

/// <summary>
/// Los datos del documento del proveedor, 1:1 con la factura o la nota del proveedor (<c>INV_SupplierInvoiceDetails</c>;
/// feature 012, T335; data-model §9.2; FR-050): prefijo, número y CUFE con que el proveedor la emitió, fechas, forma de pago
/// y si es electrónica. Nace con el borrador y se reescribe mientras lo sea; confirmado, lo único que cambia es
/// <see cref="IsReleased"/>, que se enciende cuando el registro se anula o se descarta y deja libre el número para
/// registrarlo otra vez. La unicidad (proveedor, clase, prefijo, número) y la del CUFE son entre los no liberados. El
/// documento soporte (I4) no la usa: lo emite la cooperativa.
/// </summary>
public class SupplierInvoiceDetail : AuditableEntity
{
    public const int LargoDelPrefijo = 10;
    public const int LargoDelNumero = 20;
    public const int LargoDelCufe = 96;

    /// <summary>Forma de pago de contado (texto de <c>SupplierDocumentV1.PaymentForm</c>).</summary>
    public const string Contado = "Cash";

    /// <summary>Forma de pago a crédito (texto de <c>SupplierDocumentV1.PaymentForm</c>).</summary>
    public const string Credito = "Credit";

    public int DocumentId { get; set; }

    /// <summary>El documento (navegación: el borrador nuevo todavía no tiene Id cuando nace su detalle).</summary>
    public Documents.InventoryDocument? Document { get; set; }

    /// <summary>Copia de la clase del documento, para el índice único.</summary>
    public DocumentClass DocumentClass { get; set; }

    /// <summary>Copia de <c>CounterpartyPersonId</c>, para el índice único.</summary>
    public int SupplierPersonId { get; set; }

    /// <summary><c>''</c> si el proveedor numera sin prefijo.</summary>
    public string SupplierPrefix { get; set; } = string.Empty;

    public string SupplierNumber { get; set; } = string.Empty;

    /// <summary>CUFE (factura) o CUDE (nota), 96 hexadecimales en minúscula; obligatorio si <see cref="IsElectronic"/>.</summary>
    public string? Cufe { get; set; }

    public DateOnly IssueDate { get; set; }

    /// <summary>Obligatoria a crédito; nunca antes de <see cref="IssueDate"/>.</summary>
    public DateOnly? DueDate { get; set; }

    public bool IsCredit { get; set; }

    public bool IsElectronic { get; set; }

    /// <summary>Sólo <c>SupplierNote</c>: débito (verdadero) o crédito; da el signo de «FacturaProveedorRegistrada».</summary>
    public bool IsDebitNote { get; set; }

    /// <summary>El documento se anuló o se descartó: su número vuelve a estar libre.</summary>
    public bool IsReleased { get; set; }

    /// <summary>La forma de pago como la dice el contrato (<see cref="Contado"/> o <see cref="Credito"/>).</summary>
    public string PaymentForm => IsCredit ? Credito : Contado;

    /// <summary>El número como lo imprimió el proveedor (prefijo y número).</summary>
    public string NumeroVisible => $"{SupplierPrefix}{SupplierNumber}";

    /// <summary>Normaliza el CUFE: sin espacios y en minúscula; vacío = nulo.</summary>
    public static string? NormalizarCufe(string? cufe) =>
        string.IsNullOrWhiteSpace(cufe) ? null : cufe.Trim().ToLowerInvariant();

    /// <summary>Normaliza prefijo y número: sin espacios alrededor y en mayúscula.</summary>
    public static string NormalizarNumero(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}
