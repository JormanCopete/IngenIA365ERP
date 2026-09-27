using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// El lote de cierre de un datáfono en una línea que se arquea por total de comprobantes (<c>INV_CashCountTerminalBatches</c>;
/// feature 012, I3, T574; FR-099, FR-101; data-model §15). Único <c>(CashCountLineId, CardTerminalId, BatchNumber)</c>. El lote se
/// asocia a los pagos de la sesión y del datáfono sin editarlos: el informe <c>card-payments</c> los une por sesión y datáfono.
/// </summary>
public class CashCountTerminalBatch : AuditableEntity
{
    public int CashCountLineId { get; set; }

    public int CardTerminalId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>Según el voucher de cierre.</summary>
    public decimal BatchTotal { get; set; }

    public int VoucherCount { get; set; }

    /// <summary>Σ pagos de la sesión con ese medio y ese datáfono.</summary>
    public decimal ExpectedTotal { get; set; }
}
