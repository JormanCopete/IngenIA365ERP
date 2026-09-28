using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Lo contado de una denominación en una línea de conteo físico (<c>INV_CashCountDenominations</c>; feature 012, I3, T574;
/// data-model §15). Único <c>(CashCountLineId, CashDenominationId)</c>; el valor es copia del catálogo y la suma de
/// <see cref="Amount"/> iguala lo contado cuando se contó por denominaciones.
/// </summary>
public class CashCountDenomination : AuditableEntity
{
    public int CashCountLineId { get; set; }

    public int CashDenominationId { get; set; }

    /// <summary>Copia de <c>COR_CashDenominations.Value</c>.</summary>
    public decimal DenominationValue { get; set; }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }
}
