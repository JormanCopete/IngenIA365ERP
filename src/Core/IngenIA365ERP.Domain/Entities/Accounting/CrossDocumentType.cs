using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Tipo de documento cruce (factura de venta, de compra, cuenta de cobro, nota, contrato, pagaré…). Sembrado y ampliable.</summary>
public class CrossDocumentType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsSeeded { get; set; }
}
