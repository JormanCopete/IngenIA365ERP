using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;

namespace IngenIA365ERP.Domain.Entities.Inventory.Purchasing;

/// <summary>
/// El resultado del cruce a tres vías de una línea de la factura del proveedor (<c>INV_PurchaseMatchLines</c>; feature 012, US13,
/// T777; data-model §9.6; FR-050): lo ordenado, lo recibido no facturado y lo facturado (en unidad base), los tres precios, las
/// diferencias, si excede la tolerancia vigente y con qué valores (<see cref="ToleranceJson"/>), y —si excede— su estado y la
/// solicitud de aprobación de la excepción (sujeto <c>PurchaseMatchException</c>, <c>SourceType = PurchaseMatchLine</c>).
/// <para>
/// Se calcula en cada intento de confirmar la factura con <see cref="CruceDeCompra"/> y nace con <see cref="Registrar"/>. No es un
/// hecho inmutable: las filas de un intento anterior se dan de baja lógica al recalcular. Una línea que excede nace
/// <see cref="PurchaseMatchStatus.Held"/> y sólo desde ahí se aprueba o se rechaza; una retenida por cantidad (facturar más de lo
/// recibido) no se aprueba (<see cref="CruceDeCompra.CodigoCantidadNoAprobable"/>, T796). Las transiciones inválidas lanzan
/// <see cref="InvalidOperationException"/>: el comando las revisa antes con su código.
/// </para>
/// </summary>
public class PurchaseMatchLine : AuditableEntity
{
    public const int LargoDeToleranceJson = CruceDeCompra.LargoMaximoDeToleranceJson;
    public const int LargoDeReasons = 40;

    /// <summary>La factura del proveedor.</summary>
    public int InvoiceDocumentId { get; set; }

    public int InvoiceLineId { get; set; }

    /// <summary>La línea de la orden; nula sin orden.</summary>
    public int? OrderLineId { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal ReceivedNotInvoicedQuantity { get; set; }

    public decimal InvoicedQuantity { get; set; }

    public decimal? OrderedUnitPrice { get; set; }

    public decimal? ReceivedUnitCost { get; set; }

    public decimal? InvoicedUnitPrice { get; set; }

    /// <summary>Lo facturado de más sobre lo recibido no facturado (0 si no).</summary>
    public decimal QuantityDifference { get; set; }

    public decimal PriceDifferenceAmount { get; set; }

    /// <summary>Fracción (0,02 = 2 %).</summary>
    public decimal PriceDifferenceRate { get; set; }

    /// <summary>Según <c>Compras.Tolerancia*</c> y <c>Compras.ReglaDeTolerancia</c> vigentes a la fecha.</summary>
    public bool ExceedsTolerance { get; set; }

    /// <summary>Los valores de tolerancia que se usaron.</summary>
    public string ToleranceJson { get; set; } = string.Empty;

    /// <summary>La solicitud de la excepción (política «Diferencia de cruce»).</summary>
    public Guid? ApprovalRequestPublicId { get; private set; }

    /// <summary>(nuevo) Nulo si la línea no excede.</summary>
    public PurchaseMatchStatus? Status { get; private set; }

    /// <summary>(nuevo) Motivos de la retención separados por coma: <c>Quantity</c>, <c>Price</c>. Nulo si no excede.</summary>
    public string? Reasons { get; set; }

    public bool EstaRetenida => Status == PurchaseMatchStatus.Held;

    /// <summary>¿Se puede aprobar por excepción? No si está retenida por cantidad (T796).</summary>
    public bool Aprobable => Reasons is null || !Reasons.Split(',').Contains(CruceDeCompra.RazonCantidad, StringComparer.Ordinal);

    /// <summary>La fila de una línea cruzada, tal como la dejó <see cref="CruceDeCompra.Cruzar"/>.</summary>
    public static PurchaseMatchLine Registrar(int invoiceDocumentId, int invoiceLineId, int? orderLineId, LineaAlCruce linea, ResultadoDelCruce resultado)
    {
        ArgumentNullException.ThrowIfNull(linea);
        ArgumentNullException.ThrowIfNull(resultado);
        return new PurchaseMatchLine
        {
            InvoiceDocumentId = invoiceDocumentId,
            InvoiceLineId = invoiceLineId,
            OrderLineId = orderLineId,
            OrderedQuantity = linea.Ordenado ?? 0m,
            ReceivedNotInvoicedQuantity = linea.RecibidoNoFacturado,
            InvoicedQuantity = linea.Facturado,
            OrderedUnitPrice = linea.PrecioOrdenado,
            ReceivedUnitCost = linea.CostoRecibido,
            InvoicedUnitPrice = linea.PrecioFacturado,
            QuantityDifference = resultado.QuantityDifference,
            PriceDifferenceAmount = resultado.PriceDifferenceAmount,
            PriceDifferenceRate = resultado.PriceDifferenceRate,
            ExceedsTolerance = resultado.ExceedsTolerance,
            ToleranceJson = resultado.ToleranceJson,
            Reasons = resultado.Reasons,
            Status = resultado.Status,
        };
    }

    /// <summary>Anota la solicitud de aprobación de la excepción; sólo mientras está retenida.</summary>
    public void AsignarSolicitud(Guid approvalRequestPublicId)
    {
        if (!EstaRetenida) throw new InvalidOperationException("Sólo una línea retenida pide aprobación.");
        ApprovalRequestPublicId = approvalRequestPublicId;
    }

    /// <summary>Retenida → aprobada. Una retenida por cantidad no se aprueba.</summary>
    public void Aprobar()
    {
        if (!EstaRetenida) throw new InvalidOperationException("Sólo una línea retenida se aprueba.");
        if (!Aprobable)
            throw new InvalidOperationException($"{CruceDeCompra.CodigoCantidadNoAprobable}: facturar más de lo recibido no se aprueba por excepción.");
        Status = PurchaseMatchStatus.Approved;
    }

    /// <summary>Retenida → rechazada (la factura vuelve a borrador).</summary>
    public void Rechazar()
    {
        if (!EstaRetenida) throw new InvalidOperationException("Sólo una línea retenida se rechaza.");
        Status = PurchaseMatchStatus.Rejected;
    }
}
