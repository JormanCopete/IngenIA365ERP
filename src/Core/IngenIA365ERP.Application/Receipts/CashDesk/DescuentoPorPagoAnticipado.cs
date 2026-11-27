namespace IngenIA365ERP.Application.Receipts.CashDesk;

/// <summary>
/// 2026-11-26 (pedido del dueño; COOFLOPAL): el descuento de intereses por pago anticipado. Al anticipar una cuota completa de un período que no ha empezado,
/// la cooperativa descuenta en caja una fracción del interés de la cuota (<c>Pagos.DescuentoPorPagoAnticipado</c>, por línea; COOFLOPAL la llamaba
/// «INTERES×1,4÷1,8−INTERES»). El cajero decide si lo aplica.
/// <list type="bullet">
/// <item>Cartera recibe la cuota <b>completa</b>: el descuento no toca el libro, la causación ni el estado de cuenta del crédito.</item>
/// <item>El asociado paga la cuota menos el descuento: el pago a Cartera es la porción en medios más el descuento.</item>
/// <item>El descuento queda en la aplicación del recibo (<c>RCD_ReceiptApplications.AdvanceDiscount</c>) y se contabiliza en el comprobante del recibo contra
/// el rol «Descuento por pago anticipado» (la cuenta la elige la contadora: un fondo, un gasto o el ingreso al débito).</item>
/// </list>
/// Lo admite <see cref="ReglasDeLaCaja"/>; esto sólo lo calcula. Puro. (nuevo)
/// </summary>
public static class DescuentoPorPagoAnticipado
{
    /// <summary>El interés del plan de la cuota por la fracción (de 0 a 1), al peso; cero sin interés o sin fracción.</summary>
    public static decimal Calcular(decimal interes, decimal fraccion) =>
        interes <= 0m || fraccion <= 0m ? 0m : Math.Round(interes * Math.Min(fraccion, 1m), 0, MidpointRounding.AwayFromZero);
}
