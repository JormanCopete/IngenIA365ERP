using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;

namespace IngenIA365ERP.Domain.Lending.Schedules;

/// <summary>
/// Los vencimientos de un plan por periodicidad (feature 013, T27, T29, R35, M6; FR-022). Los días de cada periodicidad salen del
/// <b>valor del enum</b> (<see cref="RepaymentPeriodicity"/>: 7, 10, 15, 30…), nunca de un literal (CH39). Tres familias:
///
/// <list type="bullet">
/// <item><b>Por meses</b> (mensual, bimestral, trimestral, semestral, anual: días múltiplo de 30): cada vencimiento es el día de
/// pago del mes que toca; <b>si el mes no lo tiene, el último día</b> (día 31 → 30 de abril, 28 o 29 de febrero).</item>
/// <item><b>Partes del mes</b> (quincenal y decadal: 30 se divide exacto): el mes se parte en 30 / días ranuras, desde el día de
/// pago reducido a la primera parte (día 15 quincenal → 15 y 30; día 5 decadal → 5, 15 y 25); la última ranura que el mes no
/// tiene va a su último día (30 de febrero → 28).</item>
/// <item><b>Semanal</b> (7 no divide al mes): cada vencimiento, los días de la periodicidad después del anterior.</item>
/// </list>
///
/// <para>
/// El primer vencimiento es <paramref name="primerVencimiento"/> si viene (el período irregular queda entre el desembolso y esa
/// fecha); si no y hay día de pago, la primera fecha de pago que no caiga antes del desembolso más un período (el primer período
/// dura entre uno y dos períodos: nunca menos de uno); sin día de pago, el desembolso más un período. El pago único vence en
/// <paramref name="primerVencimiento"/>, que es obligatorio. Puro: los vencimientos no miran el calendario hábil; la fecha exigible
/// del art. 829 (<see cref="FechaExigible"/>, US4) la da <see cref="CalendarioHabil"/> con los festivos que recibe. (nuevo)
/// </para>
/// </summary>
public static class FechasDeVencimiento
{
    private const int MesComercial = ContadorDeDias.MesComercial;

    /// <summary>Los <paramref name="periodos"/> vencimientos del plan, en orden.</summary>
    public static IReadOnlyList<DateOnly> Generar(
        DateOnly desembolso, RepaymentPeriodicity periodicidad, int periodos, int? diaDePago = null, DateOnly? primerVencimiento = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(periodos, 1);
        if (diaDePago is { } d && (d < 1 || d > 31))
            throw new ArgumentOutOfRangeException(nameof(diaDePago), diaDePago, "El día de pago va de 1 a 31.");
        if (primerVencimiento is { } pv && pv <= desembolso)
            throw new ArgumentException("El primer vencimiento es posterior al desembolso.", nameof(primerVencimiento));

        var dias = (int)periodicidad;
        if (periodicidad == RepaymentPeriodicity.SinglePayment)
        {
            if (periodos != 1) throw new ArgumentException("El pago único tiene un solo vencimiento.", nameof(periodos));
            return [primerVencimiento ?? throw new ArgumentException("El pago único necesita su fecha de vencimiento.", nameof(primerVencimiento))];
        }
        if (dias <= 0 || !Enum.IsDefined(periodicidad))
            throw new ArgumentOutOfRangeException(nameof(periodicidad), periodicidad, "Periodicidad desconocida.");

        if (dias % MesComercial == 0) return PorMeses(desembolso, dias / MesComercial, periodos, diaDePago, primerVencimiento);
        if (MesComercial % dias == 0) return PartesDelMes(desembolso, dias, periodos, diaDePago, primerVencimiento);
        return PorDias(desembolso, dias, periodos, primerVencimiento);
    }

    /// <summary>El día <paramref name="dia"/> del mes, o el último si el mes no lo tiene.</summary>
    public static DateOnly EnElDia(int anio, int mes, int dia) => new(anio, mes, Math.Min(dia, DateTime.DaysInMonth(anio, mes)));

    /// <summary>
    /// El calendario del art. 829 del Código de Comercio (US4, T0485; D2, FR-070): no hábiles sólo los domingos y los festivos
    /// <b>nacionales</b> que recibe (los replicados, nunca los «Manual» de Nómina); el sábado es hábil. La regla no se configura.
    /// </summary>
    public static CalendarioHabil CalendarioDelArticulo829(IEnumerable<DateOnly> festivosNacionales) =>
        new(festivosNacionales, [DayOfWeek.Sunday]);

    /// <summary>
    /// La fecha exigible de un vencimiento (US4, T0485; FR-070): el mismo día si es hábil; si no, el hábil siguiente. Hasta esa fecha
    /// se paga sin mora; el capital de la cuota vence el día siguiente (T30) y el interés corriente de esos días va a la cuota
    /// siguiente.
    /// </summary>
    public static DateOnly FechaExigible(DateOnly vencimiento, CalendarioHabil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return calendario.SiguienteHabil(vencimiento);
    }

    /// <summary>Las fechas exigibles de unos vencimientos, en el mismo orden.</summary>
    public static IReadOnlyList<DateOnly> FechasExigibles(IEnumerable<DateOnly> vencimientos, CalendarioHabil calendario)
    {
        ArgumentNullException.ThrowIfNull(vencimientos);
        return vencimientos.Select(v => FechaExigible(v, calendario)).ToList();
    }

    private static List<DateOnly> PorMeses(DateOnly desembolso, int meses, int periodos, int? diaDePago, DateOnly? primerVencimiento)
    {
        DateOnly primero;
        int dia;
        if (primerVencimiento is { } pv)
        {
            primero = pv;
            dia = diaDePago ?? pv.Day;
        }
        else if (diaDePago is { } d)
        {
            dia = d;
            var limite = desembolso.AddMonths(meses);
            primero = EnElDia(limite.Year, limite.Month, dia);
            if (primero < limite)
            {
                var siguiente = limite.AddMonths(1);
                primero = EnElDia(siguiente.Year, siguiente.Month, dia);
            }
        }
        else
        {
            dia = desembolso.Day;
            var limite = desembolso.AddMonths(meses);
            primero = EnElDia(limite.Year, limite.Month, dia);
        }

        var fechas = new List<DateOnly>(periodos) { primero };
        for (var k = 1; k < periodos; k++)
        {
            var mes = new DateOnly(primero.Year, primero.Month, 1).AddMonths(meses * k);
            fechas.Add(EnElDia(mes.Year, mes.Month, dia));
        }
        return fechas;
    }

    private static List<DateOnly> PartesDelMes(DateOnly desembolso, int dias, int periodos, int? diaDePago, DateOnly? primerVencimiento)
    {
        var dia = diaDePago ?? primerVencimiento?.Day ?? desembolso.Day;
        var desfase = (dia - 1) % dias + 1;
        var ranuras = MesComercial / dias;

        DateOnly anterior;
        var fechas = new List<DateOnly>(periodos);
        if (primerVencimiento is { } pv)
        {
            fechas.Add(pv);
            anterior = pv;
        }
        else
        {
            var limite = desembolso.AddDays(dias);
            var primero = Ranuras(new DateOnly(limite.Year, limite.Month, 1), desfase, dias, ranuras).First(f => f >= limite);
            fechas.Add(primero);
            anterior = primero;
        }

        foreach (var f in Ranuras(new DateOnly(anterior.Year, anterior.Month, 1), desfase, dias, ranuras))
        {
            if (fechas.Count == periodos) break;
            if (f <= anterior) continue;
            fechas.Add(f);
            anterior = f;
        }
        return fechas;
    }

    /// <summary>Las ranuras de pago desde el mes de <paramref name="desde"/> en adelante, sin fin.</summary>
    private static IEnumerable<DateOnly> Ranuras(DateOnly desde, int desfase, int dias, int ranuras)
    {
        for (var mes = desde; ; mes = mes.AddMonths(1))
            for (var j = 0; j < ranuras; j++)
                yield return EnElDia(mes.Year, mes.Month, desfase + j * dias);
    }

    private static List<DateOnly> PorDias(DateOnly desembolso, int dias, int periodos, DateOnly? primerVencimiento)
    {
        var primero = primerVencimiento ?? desembolso.AddDays(dias);
        var fechas = new List<DateOnly>(periodos);
        for (var k = 0; k < periodos; k++) fechas.Add(primero.AddDays(dias * k));
        return fechas;
    }
}
