using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationSaleReconciliations</c> (feature 013, K1; data-model §21.5; FR-143, T83): la conciliación por factura de las
/// ventas a crédito acumuladas hasta el corte, una fila por pago de crédito y versión del cuadre. No es hecho: se marca desde la
/// lista con la carga <c>Applied</c> y antes de activar; al recalcular, las marcas se copian a la versión nueva.
/// </summary>
public class MigrationSaleReconciliation : AuditableEntity
{
    public int MigrationLoadId { get; set; }

    public int CheckVersion { get; set; }

    /// <summary>La <c>VentaACreditoRegistrada</c> acumulada (bandeja, sin FK).</summary>
    public Guid MessagePublicId { get; set; }

    public Guid SaleDocumentPublicId { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;

    public Guid CreditPaymentPublicId { get; set; }

    public DateOnly SaleDate { get; set; }

    public decimal Amount { get; set; }

    public int? PersonId { get; set; }

    public SaleReconciliationOutcome Outcome { get; set; }

    /// <summary><c>Migrated</c>: la fila de <c>loans</c> con esa factura.</summary>
    public int? MigrationLoanRowId { get; set; }

    /// <summary><c>CancelledBeforeCutoff</c> cargada por la plantilla <c>sales</c>.</summary>
    public int? MigrationSaleRowId { get; set; }

    /// <summary><c>CancelledBeforeCutoff</c> u <c>OutsideLendingD29</c> marcada desde la lista.</summary>
    public bool MarkedFromList { get; set; }

    /// <summary>Obligatorio con la marca.</summary>
    public string? MarkReason { get; set; }

    public int? MarkedByUserId { get; set; }

    public DateTime? MarkedAt { get; set; }

    /// <summary><c>NotMigratedWithBalance</c> y <c>OutsideLendingD29</c>: la diferencia a aceptar con acta (D17).</summary>
    public int? DifferenceId { get; set; }

    /// <summary>Ajustes acumulados de la venta hasta el corte (informativo).</summary>
    public int AdjustmentsUpToCutoff { get; set; }

    /// <summary>Ajustes acumulados de la venta después del corte (informativo).</summary>
    public int AdjustmentsAfterCutoff { get; set; }

    /// <summary>Escrito al activar: el crédito migrado o la obligación en cero de la cancelada.</summary>
    public int? ResultingLoanId { get; set; }
}
