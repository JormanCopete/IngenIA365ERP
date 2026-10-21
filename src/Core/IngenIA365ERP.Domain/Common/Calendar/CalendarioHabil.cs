namespace IngenIA365ERP.Domain.Common.Calendar;

/// <summary>
/// Calendario de días hábiles, puro (feature 013, T27, T40, R35; CH34). No sabe qué días son festivos ni si el sábado
/// es hábil: recibe la lista de días no hábiles (los <c>FESTIVO</c> nacionales replicados, que le pasa
/// <c>ICalendarioHabil</c>; nunca los festivos «Manual» de Nómina, pregunta E5) y los días de descanso de la semana
/// (para la fecha exigible del art. 829 sólo el domingo, pregunta D2; para los plazos del titular, sábado y domingo).
///
/// <list type="bullet">
/// <item><see cref="EsHabil"/>: ni día de descanso ni no hábil.</item>
/// <item><see cref="SiguienteHabil"/>: la misma fecha si es hábil; si no, la primera hábil que sigue (la fecha exigible
/// del art. 829).</item>
/// <item><see cref="Contar"/>: días hábiles de <c>a</c> excluida a <c>b</c> incluida, como <c>ContadorDeDias</c>.</item>
/// <item><see cref="SumarHabiles"/>: la fecha que queda <c>n</c> días hábiles después, contando desde el día siguiente.</item>
/// </list>
/// La lista cubre los años que el llamador necesita; una fecha fuera de ella se trata como si no hubiera festivos.
/// </summary>
public sealed class CalendarioHabil
{
    private readonly HashSet<DateOnly> _noHabiles;
    private readonly HashSet<DayOfWeek> _descanso;

    public CalendarioHabil(IEnumerable<DateOnly> noHabiles, IEnumerable<DayOfWeek> diasDeDescanso)
    {
        ArgumentNullException.ThrowIfNull(noHabiles);
        ArgumentNullException.ThrowIfNull(diasDeDescanso);
        _noHabiles = noHabiles.ToHashSet();
        _descanso = diasDeDescanso.ToHashSet();
        if (_descanso.Count >= 7)
            throw new ArgumentException("Una semana sin días hábiles no es un calendario.", nameof(diasDeDescanso));
    }

    /// <summary>Los días de descanso de la semana con que se armó.</summary>
    public IReadOnlyCollection<DayOfWeek> DiasDeDescanso => _descanso;

    public bool EsHabil(DateOnly fecha) => !_descanso.Contains(fecha.DayOfWeek) && !_noHabiles.Contains(fecha);

    /// <summary>La misma fecha si es hábil; si no, la primera hábil después de ella.</summary>
    public DateOnly SiguienteHabil(DateOnly fecha)
    {
        var d = fecha;
        while (!EsHabil(d)) d = d.AddDays(1);
        return d;
    }

    /// <summary>Días hábiles en (<paramref name="a"/>, <paramref name="b"/>]: <c>a</c> excluida, <c>b</c> incluida.</summary>
    public int Contar(DateOnly a, DateOnly b)
    {
        if (b < a) throw new ArgumentException($"El final ({b:yyyy-MM-dd}) es anterior al inicio ({a:yyyy-MM-dd}).", nameof(b));
        var n = 0;
        for (var d = a.AddDays(1); d <= b; d = d.AddDays(1))
            if (EsHabil(d)) n++;
        return n;
    }

    /// <summary>
    /// La fecha hábil en que se completan <paramref name="dias"/> días hábiles contados desde el día siguiente a
    /// <paramref name="desde"/> (un plazo de n días hábiles a partir del recibo). Con cero, la misma fecha.
    /// </summary>
    public DateOnly SumarHabiles(DateOnly desde, int dias)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dias);
        var d = desde;
        var contados = 0;
        while (contados < dias)
        {
            d = d.AddDays(1);
            if (EsHabil(d)) contados++;
        }
        return d;
    }
}
