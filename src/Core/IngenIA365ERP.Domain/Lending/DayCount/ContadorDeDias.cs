using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.DayCount;

/// <summary>
/// Los días que causan entre dos fechas según la convención sellada de la línea (feature 013, T27, R35, M6; FR-010; D3). Cuenta
/// <b>de <c>a</c> excluida a <c>b</c> incluida</b>: el día del desembolso no causa y el del vencimiento sí, de modo que dos tramos
/// seguidos (a..b y b..c) suman exactamente a..c y ningún día se cuenta dos veces.
///
/// <list type="bullet">
/// <item><see cref="DayCountConvention.Thirty360"/>: mes comercial de 30 días. El 31 <b>no causa</b> (vale como 30) y el último
/// día de febrero <b>completa 30</b> (28 o 29 valen como 30): del 31 de enero al 28 de febrero hay 30 días; del 30 al 31 de
/// enero, cero. Año de 360.</item>
/// <item><see cref="DayCountConvention.Actual365"/>: días reales con base <b>fija</b> de 365: el 29 de febrero causa como cualquier
/// otro día (D3), y en 2028 un año real tiene 366 días de interés sobre la base 365.</item>
/// <item><see cref="DayCountConvention.Actual360"/>: días reales sobre base 360: con la misma EA, la efectiva resultante sube
/// (lo que vigila <c>ControlDeUsura</c>).</item>
/// </list>
///
/// <para>Puro: sin IO ni reloj; las fechas llegan por parámetro (<c>LaCarteraUsaLaFechaLocal</c>). No reutiliza
/// <c>CalendarConventions</c> ni <c>DiasHabiles</c> de Nómina, que prorratean de forma inclusiva (T27). La fecha exigible de un
/// vencimiento en día no hábil (art. 829) no es asunto de este contador: es de <c>CalendarioHabil</c>. (nuevo)</para>
/// </summary>
public static class ContadorDeDias
{
    /// <summary>Días del año comercial (30/360 y real/360).</summary>
    public const int AnioComercial = 360;

    /// <summary>Días del año real de base fija (real/365, D3; también el año sobre el que se anualiza la usura).</summary>
    public const int AnioReal = 365;

    /// <summary>Días del mes comercial: el valor de <see cref="RepaymentPeriodicity.Monthly"/>.</summary>
    public const int MesComercial = (int)RepaymentPeriodicity.Monthly;

    /// <summary>Los días de <paramref name="a"/> (excluida) a <paramref name="b"/> (incluida) en la convención.</summary>
    /// <exception cref="ArgumentException">Si <paramref name="b"/> es anterior a <paramref name="a"/>.</exception>
    public static int Dias(DayCountConvention convencion, DateOnly a, DateOnly b)
    {
        if (b < a)
            throw new ArgumentException($"El tramo termina ({b:yyyy-MM-dd}) antes de empezar ({a:yyyy-MM-dd}).", nameof(b));

        return convencion switch
        {
            DayCountConvention.Thirty360 => Treinta360(a, b),
            DayCountConvention.Actual365 or DayCountConvention.Actual360 => b.DayNumber - a.DayNumber,
            _ => throw new ArgumentOutOfRangeException(nameof(convencion), convencion, "Convención de días desconocida."),
        };
    }

    /// <summary>
    /// La base B de la tasa diaria: 360 en 30/360 y real/360; 365 en real/365 (R34). La tasa diaria es
    /// <c>RaizEntera(1 + EA, B) − 1</c>.
    /// </summary>
    public static int Base(DayCountConvention convencion) => convencion switch
    {
        DayCountConvention.Thirty360 or DayCountConvention.Actual360 => AnioComercial,
        DayCountConvention.Actual365 => AnioReal,
        _ => throw new ArgumentOutOfRangeException(nameof(convencion), convencion, "Convención de días desconocida."),
    };

    /// <summary>
    /// Cuántos días de la convención tiene un año calendario, que es sobre lo que se mide la efectiva frente a la usura: 360 en
    /// 30/360 y 365 en las dos reales (D3: base fija, aunque el año sea bisiesto). En real/360 el año tiene 365 días que causan
    /// sobre una base de 360: por eso su efectiva supera la EA pactada.
    /// </summary>
    public static int DiasDelAnio(DayCountConvention convencion) => convencion switch
    {
        DayCountConvention.Thirty360 => AnioComercial,
        DayCountConvention.Actual365 or DayCountConvention.Actual360 => AnioReal,
        _ => throw new ArgumentOutOfRangeException(nameof(convencion), convencion, "Convención de días desconocida."),
    };

    /// <summary>¿Es el último día de febrero (28, o 29 en bisiesto)?</summary>
    public static bool EsFinDeFebrero(DateOnly fecha) =>
        fecha.Month == 2 && fecha.Day == DateTime.DaysInMonth(fecha.Year, fecha.Month);

    private static int Treinta360(DateOnly a, DateOnly b) =>
        AnioComercial * (b.Year - a.Year) + MesComercial * (b.Month - a.Month) + (DiaComercial(b) - DiaComercial(a));

    /// <summary>El día en el mes comercial: el 31 y el fin de febrero valen 30.</summary>
    private static int DiaComercial(DateOnly fecha) =>
        fecha.Day > MesComercial || EsFinDeFebrero(fecha) ? MesComercial : fecha.Day;
}
