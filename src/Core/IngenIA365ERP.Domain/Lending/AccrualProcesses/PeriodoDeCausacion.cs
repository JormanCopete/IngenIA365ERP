using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.AccrualProcesses;

/// <summary>
/// El rango de días causados que propone un proceso de causación según su periodicidad (2026-11-16, pedido del dueño; feature 024, fase 3), puro:
/// <list type="bullet">
/// <item><b>mensual</b>: del 1 al último día del mes de la referencia;</item>
/// <item><b>quincenal</b>: la primera quincena del 1 al 15 y la segunda del 16 al último día del mes;</item>
/// <item><b>decadal</b>: la primera década del 1 al 10, la segunda del 11 al 20 y la tercera del 21 al último día del mes;</item>
/// <item><b>semanal</b>: de lunes a domingo, la semana de la referencia;</item>
/// <item><b>diaria</b>: el día de la referencia.</item>
/// </list>
/// La persona puede ajustar el rango; <see cref="Validar"/> sólo exige que no pase del largo de su periodicidad (<see cref="DiasMaximos"/>), que quepa en
/// un mes en las que se cuentan por mes, que la quincena o la década sea de su periodicidad y que no sea futuro. Los largos salen del valor de la
/// periodicidad (<see cref="AccrualProcessPeriodicity"/>), nunca de un número escrito. (nuevo)
/// </summary>
public static class PeriodoDeCausacion
{
    public const string CodigoRangoInvalido = "Lending.AccrualProcess.RangeInvalid";

    private static readonly int Semana = (int)AccrualProcessPeriodicity.Weekly;
    private static readonly int Decada = (int)AccrualProcessPeriodicity.TenDay;
    private static readonly int Quincena = (int)AccrualProcessPeriodicity.Biweekly;
    private static readonly int Mes = (int)AccrualProcessPeriodicity.Monthly;

    /// <summary>Los subperíodos de la periodicidad: 2 quincenas, 3 décadas; ninguno en las demás.</summary>
    public static int Subperiodos(AccrualProcessPeriodicity periodicidad) => periodicidad switch
    {
        AccrualProcessPeriodicity.Biweekly => Mes / Quincena,
        AccrualProcessPeriodicity.TenDay => Mes / Decada,
        _ => 0,
    };

    /// <summary>
    /// El largo máximo del rango, en días, de cada periodicidad: un día, una semana, la tercera década (hasta 11 días), la segunda quincena (hasta 16) y
    /// un mes (hasta 31).
    /// </summary>
    public static int DiasMaximos(AccrualProcessPeriodicity periodicidad) => periodicidad switch
    {
        AccrualProcessPeriodicity.Daily => 1,
        AccrualProcessPeriodicity.Weekly => Semana,
        AccrualProcessPeriodicity.TenDay => Decada + 1,
        AccrualProcessPeriodicity.Biweekly => Quincena + 1,
        _ => Mes + 1,
    };

    /// <summary>
    /// El rango propuesto para <paramref name="referencia"/>: el período de la periodicidad que la contiene o, si viene <paramref name="subperiodo"/>
    /// (quincena o década), ese subperíodo del mes de la referencia.
    /// </summary>
    public static (DateOnly Desde, DateOnly Hasta) Proponer(AccrualProcessPeriodicity periodicidad, DateOnly referencia, byte? subperiodo = null)
    {
        var primero = new DateOnly(referencia.Year, referencia.Month, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        switch (periodicidad)
        {
            case AccrualProcessPeriodicity.Daily:
                return (referencia, referencia);
            case AccrualProcessPeriodicity.Weekly:
                var lunes = referencia.AddDays(-(((int)referencia.DayOfWeek + Semana - 1) % Semana));
                return (lunes, lunes.AddDays(Semana - 1));
            case AccrualProcessPeriodicity.Biweekly:
            case AccrualProcessPeriodicity.TenDay:
                var largo = (int)periodicidad;
                var cuantos = Subperiodos(periodicidad);
                var numero = subperiodo is { } s && s >= 1 && s <= cuantos ? s : Math.Min(cuantos, (referencia.Day - 1) / largo + 1);
                var desde = primero.AddDays((numero - 1) * largo);
                return (desde, numero == cuantos ? ultimo : desde.AddDays(largo - 1));
            default:
                return (primero, ultimo);
        }
    }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, HU5; FR-016): el <b>tramo del calendario</b> que contiene <paramref name="fecha"/> (el vencimiento de una cuota o la
    /// fecha valor de un pago) según la periodicidad de pago del crédito, con <c>Causacion.Periodo = PeriodoCalendario</c>:
    /// <list type="bullet">
    /// <item><b>mensual</b>: del 1 al último día del mes;</item>
    /// <item><b>quincenal</b>: del 1 al 15 y del 16 al último día;</item>
    /// <item><b>decadal</b>: del 1 al 10, del 11 al 20 y del 21 al último día;</item>
    /// <item><b>semanal</b>: de lunes a domingo, la semana que la contiene (la misma que propone el proceso);</item>
    /// <item>las periodicidades de más de un mes y el pago único: el mes que la contiene (la herramienta anterior causa por mes).</item>
    /// </list>
    /// Los largos salen del valor de la periodicidad, nunca de un número escrito. Puro. (nuevo)
    /// </summary>
    public static (DateOnly Desde, DateOnly Hasta) TramoQueContiene(DateOnly fecha, RepaymentPeriodicity periodicidad)
    {
        var primero = new DateOnly(fecha.Year, fecha.Month, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        switch (periodicidad)
        {
            case RepaymentPeriodicity.Weekly:
                var lunes = fecha.AddDays(-(((int)fecha.DayOfWeek + Semana - 1) % Semana));
                return (lunes, lunes.AddDays(Semana - 1));
            case RepaymentPeriodicity.Biweekly:
            case RepaymentPeriodicity.TenDay:
                var largo = (int)periodicidad;
                var cuantos = Mes / largo;
                var numero = Math.Min(cuantos, (fecha.Day - 1) / largo + 1);
                var desde = primero.AddDays((numero - 1) * largo);
                return (desde, numero == cuantos ? ultimo : desde.AddDays(largo - 1));
            default:
                return (primero, ultimo);
        }
    }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5): ¿el tramo de <paramref name="vencimiento"/> ya empezó a <paramref name="fecha"/>? Con <c>PeriodoCalendario</c>, lo
    /// causado de una cuota cuyo tramo no ha empezado no es de «la cuota del período»: no se cobra al cancelar ni lo toma un abono. (nuevo)
    /// </summary>
    public static bool TramoEmpezado(DateOnly vencimiento, RepaymentPeriodicity periodicidad, DateOnly fecha) =>
        TramoQueContiene(vencimiento, periodicidad).Desde <= fecha;

    /// <summary>
    /// 2026-11-24 (feature 025, fase 5, FR-018): ¿el día de <paramref name="vencimiento"/> cae en el ciclo de descuento <paramref name="ciclo"/> del mes?
    /// Quincenas del 1 al 15 y del 16 al fin; décadas del 1 al 10, del 11 al 20 y del 21 al fin; semanas del mes de siete días desde el 1 (la quinta, del
    /// 29 al fin). Los largos salen del valor de la periodicidad. Puro. (nuevo)
    /// </summary>
    public static bool EnCicloDeDescuento(DateOnly vencimiento, AccrualDiscountCycle ciclo)
    {
        var dia = vencimiento.Day;
        int largo, numero, cuantos;
        switch (ciclo)
        {
            case AccrualDiscountCycle.FirstFortnight or AccrualDiscountCycle.SecondFortnight:
                largo = Quincena;
                numero = ciclo - AccrualDiscountCycle.FirstFortnight + 1;
                cuantos = Mes / Quincena;
                break;
            case AccrualDiscountCycle.FirstTenDay or AccrualDiscountCycle.SecondTenDay or AccrualDiscountCycle.ThirdTenDay:
                largo = Decada;
                numero = ciclo - AccrualDiscountCycle.FirstTenDay + 1;
                cuantos = Mes / Decada;
                break;
            case >= AccrualDiscountCycle.FirstWeek and <= AccrualDiscountCycle.FifthWeek:
                largo = Semana;
                numero = ciclo - AccrualDiscountCycle.FirstWeek + 1;
                cuantos = AccrualDiscountCycle.FifthWeek - AccrualDiscountCycle.FirstWeek + 1;
                break;
            default:
                return false;
        }
        var desde = (numero - 1) * largo + 1;
        return dia >= desde && (numero == cuantos || dia < desde + largo);
    }

    /// <summary>Lo que no cumple el rango pedido, o nulo si está bien. <paramref name="hoy"/> es la fecha local de la cooperativa.</summary>
    public static ProblemaDeCartera? Validar(AccrualProcessPeriodicity periodicidad, byte? subperiodo, DateOnly desde, DateOnly hasta, DateOnly hoy,
        bool reconocimiento = false)
    {
        if (!Enum.IsDefined(periodicidad))
            return Problema("Elija la periodicidad del proceso: mensual, quincenal, decadal, semanal o diaria.", desde, hasta);
        var subperiodos = Subperiodos(periodicidad);
        if (subperiodos == 0 && subperiodo is not null)
            return Problema("Sólo la quincenal lleva quincena y la decadal, década.", desde, hasta);
        if (subperiodo is { } s && (s < 1 || s > subperiodos))
            return Problema(periodicidad == AccrualProcessPeriodicity.Biweekly
                ? "La quincena es la primera o la segunda."
                : "La década es la primera, la segunda o la tercera.", desde, hasta);
        if (hasta < desde) return Problema("El rango termina antes de empezar.", desde, hasta);
        // 2026-11-16 (feature 024, fase 4): el reconocimiento de la cuota del período elige el ciclo, que puede terminar después de hoy pero tiene que haber
        // empezado; contabilizar lo causado, sólo días ya causados.
        if (reconocimiento && desde > hoy)
            return Problema($"El ciclo que se reconoce tiene que haber empezado: el rango empieza después de hoy ({hoy:yyyy-MM-dd}).", desde, hasta);
        if (!reconocimiento && hasta > hoy)
            return Problema($"El rango no puede pasar de hoy ({hoy:yyyy-MM-dd}): sólo se contabiliza lo ya causado.", desde, hasta);
        var dias = hasta.DayNumber - desde.DayNumber + 1;
        if (dias > DiasMaximos(periodicidad))
            return Problema($"El rango tiene {dias} días y la periodicidad elegida admite hasta {DiasMaximos(periodicidad)}.", desde, hasta);
        if (periodicidad is AccrualProcessPeriodicity.Monthly or AccrualProcessPeriodicity.Biweekly or AccrualProcessPeriodicity.TenDay
            && (desde.Year != hasta.Year || desde.Month != hasta.Month))
            return Problema("En la mensual, la quincenal y la decadal el rango queda dentro de un mismo mes.", desde, hasta);
        return null;
    }

    private static ProblemaDeCartera Problema(string mensaje, DateOnly desde, DateOnly hasta) =>
        new(CodigoRangoInvalido, mensaje, new Dictionary<string, object?> { ["dateFrom"] = desde, ["dateTo"] = hasta });
}
