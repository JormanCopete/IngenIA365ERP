namespace IngenIA365ERP.Domain.Inventory.Analytics;

/// <summary>
/// La rotación de un producto, una categoría o un grupo contable en un período (feature 012, I6, T959; FR-086): el costo de venta, el
/// inventario promedio, los días del período, la rotación y los días de inventario. <see cref="Rotacion"/> y
/// <see cref="DiasDeInventario"/> son nulos cuando no se pueden medir —«sin dato»—: con inventario promedio cero o negativo no hay
/// rotación, y sin costo de venta no hay días. (nuevo)
/// </summary>
public sealed record IndicadorDeRotacion(decimal CostoDeVenta, decimal InventarioPromedio, int DiasDelPeriodo, decimal? Rotacion, decimal? DiasDeInventario)
{
    /// <summary>Sin inventario promedio positivo no hay rotación que mostrar.</summary>
    public bool SinDato => Rotacion is null;
}

/// <summary>
/// Los indicadores de rotación (feature 012, I6, T959; FR-086; contracts/api.md §27 vista <c>turnover</c>, §28 fichas <c>turnover</c> e
/// <c>inventoryDays</c>): puros, sin IO, en <c>decimal</c> exacto y sin valores fijos (<c>ElComercioNoTieneValoresLegalesFijos</c>). Los
/// usan la vista <c>turnover</c> y el tablero, que no calculan otra cosa. Reglas:
/// <list type="bullet">
/// <item>inventario promedio = el promedio de los saldos al costo observados en el período (el inicial y el de cada cierre);</item>
/// <item>rotación = costo de venta del período ÷ inventario promedio;</item>
/// <item>días de inventario = días del período ÷ rotación, con la rotación <b>exacta</b> (es lo mismo que días × promedio ÷ costo);</item>
/// <item>inventario promedio cero o negativo → «sin dato» (ni rotación ni días), nunca una división por cero; sin costo de venta la rotación
/// es cero y los días no se pueden medir.</item>
/// </list>
/// Rotación y días se muestran a <see cref="Decimales"/> decimales; el promedio, al centavo. (nuevo)
/// </summary>
public static class IndicadoresDeRotacion
{
    /// <summary>Los decimales con que se muestran la rotación y los días (y el promedio, al centavo).</summary>
    public const int Decimales = 2;

    /// <summary>El promedio de los saldos al costo observados en el período; sin saldos, cero.</summary>
    public static decimal InventarioPromedio(IReadOnlyCollection<decimal> saldos)
    {
        ArgumentNullException.ThrowIfNull(saldos);
        return saldos.Count == 0 ? 0m : Redondear(saldos.Sum() / saldos.Count);
    }

    /// <summary>Los días del período, contando el primero y el último.</summary>
    public static int DiasDelPeriodo(DateOnly desde, DateOnly hasta) => hasta.DayNumber - desde.DayNumber + 1;

    public static IndicadorDeRotacion Calcular(decimal costoDeVenta, decimal inventarioPromedio, int diasDelPeriodo)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(diasDelPeriodo);
        if (inventarioPromedio <= 0m)
            return new IndicadorDeRotacion(costoDeVenta, inventarioPromedio, diasDelPeriodo, null, null);

        var rotacion = costoDeVenta / inventarioPromedio;
        decimal? dias = costoDeVenta > 0m ? Redondear(diasDelPeriodo * inventarioPromedio / costoDeVenta) : null;
        return new IndicadorDeRotacion(costoDeVenta, inventarioPromedio, diasDelPeriodo, Redondear(rotacion), dias);
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, Decimales, MidpointRounding.AwayFromZero);
}
