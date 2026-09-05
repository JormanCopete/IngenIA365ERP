namespace IngenIA365ERP.Domain.Payroll.Calculation;

/// <summary>
/// Calendario comercial de la nómina colombiana: todo mes tiene 30 días. Quien ingresa
/// el 10 se liquida por 21 días, sea febrero o marzo; el 31 no existe y el último día
/// del mes es el 30. Es una convención de proporción, no un valor legal: por eso vive
/// aquí y no en un parámetro (FR-008).
/// </summary>
public static class CalendarConventions
{
    public const int DaysPerMonth = 30;

    /// <summary>Índice comercial (1..30) de una fecha como INICIO de un tramo.</summary>
    public static int StartIndex(DateTime date) => Math.Min(date.Day, DaysPerMonth);

    /// <summary>
    /// Índice comercial (1..30) de una fecha como FIN de un tramo: el último día del
    /// mes cuenta como 30 aunque el mes tenga 28 ó 31.
    /// </summary>
    public static int EndIndex(DateTime date) =>
        date.Day == DateTime.DaysInMonth(date.Year, date.Month) ? DaysPerMonth : Math.Min(date.Day, DaysPerMonth);

    /// <summary>
    /// Días comerciales entre dos fechas inclusive. Cruza meses sumando 30 por cada mes
    /// completo intermedio. Devuelve 0 si <paramref name="to"/> es anterior a
    /// <paramref name="from"/>.
    /// </summary>
    public static int Days(DateTime from, DateTime to)
    {
        from = from.Date; to = to.Date;
        if (to < from) return 0;

        var monthsApart = (to.Year - from.Year) * 12 + (to.Month - from.Month);
        if (monthsApart == 0) return Math.Max(0, EndIndex(to) - StartIndex(from) + 1);

        var first = DaysPerMonth - StartIndex(from) + 1;      // desde from hasta fin de su mes
        var last = EndIndex(to);                              // desde el 1 hasta to
        var middle = (monthsApart - 1) * DaysPerMonth;        // meses completos intermedios
        return first + middle + last;
    }

    /// <summary>Intersección de dos rangos inclusive; nula si no se tocan.</summary>
    public static (DateTime From, DateTime To)? Overlap(DateTime aFrom, DateTime aTo, DateTime bFrom, DateTime bTo)
    {
        var from = aFrom > bFrom ? aFrom : bFrom;
        var to = aTo < bTo ? aTo : bTo;
        return to < from ? null : (from.Date, to.Date);
    }
}
