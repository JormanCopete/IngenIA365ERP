using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Tarifa de una cuenta de impuesto con vigencia (feature 009, FR-065): la línea usa la vigente a su fecha.</summary>
public class AccountTaxRate : AuditableEntity
{
    public int AccountId { get; set; }
    public ChartOfAccount? Account { get; set; }
    public DateOnly ValidFrom { get; set; }

    /// <summary>Fracción, no porcentaje: 0.04 es el 4 %.</summary>
    public decimal Rate { get; set; }
}
