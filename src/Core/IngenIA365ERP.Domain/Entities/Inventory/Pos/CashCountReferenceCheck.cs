using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// La marca de una referencia cotejada en el arqueo (<c>INV_CashCountReferenceChecks</c>; feature 012, I3, T574; FR-099;
/// data-model §15): líneas por referencia y cotejo opcional de tarjetas. Único <c>(CashCountLineId, DocumentPaymentId)</c>.
/// </summary>
public class CashCountReferenceCheck : AuditableEntity
{
    public int CashCountLineId { get; set; }

    /// <summary><c>INV_DocumentPayments</c>.</summary>
    public int DocumentPaymentId { get; set; }

    public bool IsVerified { get; set; }

    public string? Note { get; set; }
}
