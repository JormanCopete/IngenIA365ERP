using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Core.Payments;

/// <summary>
/// Un billete o una moneda para contar el efectivo (<c>COR_CashDenominations</c>; feature 012, I3, T573; FR-099; data-model §16).
/// Único <c>(Currency, Kind, Value)</c> entre vivos, con vigencia; lo siembra <c>CashDenominationsSeeder</c> (Order 85). El
/// arqueo copia el valor a <c>INV_CashCountDenominations.DenominationValue</c>.
/// </summary>
public class CashDenomination : AuditableEntity
{
    public const string MonedaPorDefecto = "COP";

    public string Currency { get; set; } = MonedaPorDefecto;

    public decimal Value { get; set; }

    public CashDenominationKind Kind { get; set; }

    public short DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }
}
