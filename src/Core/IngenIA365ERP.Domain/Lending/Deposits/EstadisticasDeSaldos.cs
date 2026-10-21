namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>El resumen de una serie de valores: promedio, mediana, máximo y mínimo (cero, cero, cero, cero si está vacía). (nuevo)</summary>
public sealed record ResumenDeSerie(int Cantidad, decimal Total, decimal Promedio, decimal Mediana, decimal Maximo, decimal Minimo);

/// <summary>
/// Las cifras de saldo y de movimientos que piden los reportes regulatorios de captaciones (feature 014, US17, T220): la serie de saldos al cierre de cada día
/// de un rango a partir de los movimientos con fecha valor (el saldo del formato de saldos diarios del SICSES y el promedio día del formato de aportes) y el
/// resumen de una serie (promedio, mediana, máximo, mínimo) que pide la exógena 1019 para el saldo y los movimientos crédito y débito. Motor puro: no
/// redondea; quien escribe el archivo decide la unidad. (nuevo)
/// </summary>
public static class EstadisticasDeSaldos
{
    /// <summary>
    /// El saldo al cierre de cada día de <paramref name="desde"/> a <paramref name="hasta"/> (ambos incluidos): lo anterior a <paramref name="desde"/> forma
    /// el saldo inicial y cada movimiento cuenta desde su fecha valor.
    /// </summary>
    public static IReadOnlyList<(DateOnly Dia, decimal Saldo)> SerieDiaria(IEnumerable<(DateOnly Fecha, decimal Monto)> movimientos, DateOnly desde, DateOnly hasta)
    {
        ArgumentNullException.ThrowIfNull(movimientos);
        if (hasta < desde) return [];
        var lista = movimientos.ToList();
        var saldo = lista.Where(m => m.Fecha < desde).Sum(m => m.Monto);
        var porDia = lista.Where(m => m.Fecha >= desde && m.Fecha <= hasta).GroupBy(m => m.Fecha).ToDictionary(g => g.Key, g => g.Sum(m => m.Monto));
        var serie = new List<(DateOnly, decimal)>(hasta.DayNumber - desde.DayNumber + 1);
        for (var d = desde; d <= hasta; d = d.AddDays(1))
        {
            saldo += porDia.GetValueOrDefault(d);
            serie.Add((d, saldo));
        }
        return serie;
    }

    /// <summary>El resumen de la serie; la mediana de una cantidad par es el promedio de los dos del medio.</summary>
    public static ResumenDeSerie Resumir(IEnumerable<decimal> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        var orden = valores.OrderBy(v => v).ToList();
        if (orden.Count == 0) return new ResumenDeSerie(0, 0m, 0m, 0m, 0m, 0m);
        var total = orden.Sum();
        var medio = orden.Count / 2;
        var mediana = orden.Count % 2 == 1 ? orden[medio] : (orden[medio - 1] + orden[medio]) / 2m;
        return new ResumenDeSerie(orden.Count, total, total / orden.Count, mediana, orden[^1], orden[0]);
    }

    /// <summary>A pesos enteros, la mitad hacia arriba (lo que piden los formatos de la DIAN).</summary>
    public static decimal APesos(decimal valor) => decimal.Round(valor, 0, MidpointRounding.AwayFromZero);
}
