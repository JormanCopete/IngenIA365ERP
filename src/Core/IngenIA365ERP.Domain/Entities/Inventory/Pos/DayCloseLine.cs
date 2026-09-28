using System.Globalization;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Una línea del cierre del día (<c>INV_DayCloseLines</c>; feature 012, I3, T574; FR-099; data-model §15): el total de un medio
/// (adquirente y datáfono nulos) o su detalle por adquirente y datáfono, para cotejar después con los abonos de la red. Único
/// <c>(DayCloseId, DetailKey)</c>, con la clave de <see cref="Clave"/>.
/// </summary>
public class DayCloseLine : AuditableEntity
{
    public int DayCloseId { get; set; }

    public int PaymentMeansId { get; set; }

    public int? CardAcquirerId { get; set; }

    public int? CardTerminalId { get; set; }

    /// <summary><c>M:{medio}|A:{adquirente|-}|T:{datáfono|-}</c>.</summary>
    public string DetailKey { get; set; } = string.Empty;

    public decimal ExpectedAmount { get; set; }

    public decimal CountedAmount { get; set; }

    public decimal DifferenceAmount { get; set; }

    public int PaymentCount { get; set; }

    /// <summary>Lotes del datáfono en el día.</summary>
    public string? BatchNumbersJson { get; set; }

    /// <summary>La clave de detalle de una línea.</summary>
    public static string Clave(int paymentMeansId, int? cardAcquirerId, int? cardTerminalId) =>
        string.Create(CultureInfo.InvariantCulture,
            $"M:{paymentMeansId}|A:{(cardAcquirerId is { } a ? a.ToString(CultureInfo.InvariantCulture) : "-")}|T:{(cardTerminalId is { } t ? t.ToString(CultureInfo.InvariantCulture) : "-")}");
}
