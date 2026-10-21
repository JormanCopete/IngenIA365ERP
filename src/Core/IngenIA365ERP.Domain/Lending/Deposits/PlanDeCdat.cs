using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Las condiciones selladas de un CDAT con que se arma su plan (feature 014, US6, T166; data-model §3.3; FR-023, FR-067): el capital, la tasa efectiva anual
/// aplicada (tabla más margen), el día en que empieza a correr el plazo, el plazo en días, cada cuánto se pagan los intereses (<c>SinglePayment</c> = al
/// vencimiento), si se pagan vencidos o anticipados, la convención de días (que da la base B: 360 o 365) y el redondeo sellado de la línea. (nuevo)
/// </summary>
public sealed record CondicionesDeCdat(
    decimal Capital,
    decimal TasaEfectivaAnual,
    DateOnly Inicio,
    int PlazoDias,
    RepaymentPeriodicity Periodicidad,
    InterestTiming Modalidad,
    DayCountConvention Convencion,
    RedondeoDeCartera Redondeo);

/// <summary>
/// Un período del plan de intereses (data-model §3.4): (<see cref="Desde"/>, <see cref="Hasta"/>] con sus días en la convención, la tasa del período
/// <c>i_p = (1 + r_d)^n − 1</c> (o la anticipada equivalente <c>i_a = i_v / (1 + i_v)</c>), el interés redondeado con el redondeo sellado y la fecha en que se
/// paga: vencido, el fin del período o, si no es hábil, el siguiente hábil sin interés adicional (D19); anticipado, el inicio del período (el primero, el día de
/// la constitución). (nuevo)
/// </summary>
public sealed record PeriodoDeCdat(int Numero, DateOnly Desde, DateOnly Hasta, DateOnly FechaDePago, int Dias, decimal TasaDelPeriodo, decimal Interes, bool EsUltimo);

/// <summary>
/// El plan de intereses de una versión de condiciones de un CDAT (FR-023, FR-069): el vencimiento (inicio + plazo en días), la fecha de pago del vencimiento
/// (el siguiente hábil si cae en no hábil, D19), el plazo de emisión que decide la cuenta contable del capital (FR-073), la tasa diaria sellada y los
/// períodos. La suma de los intereses de los períodos es el interés del plan al centavo. (nuevo)
/// </summary>
public sealed record PlanDeCdatCalculado(
    CondicionesDeCdat Condiciones,
    DateOnly Vencimiento,
    DateOnly FechaDePagoAlVencimiento,
    IssueTermBand PlazoDeEmision,
    decimal TasaDiaria,
    IReadOnlyList<PeriodoDeCdat> Periodos)
{
    public decimal InteresTotal => Periodos.Sum(p => p.Interes);

    /// <summary>El período que contiene <paramref name="dia"/> (Desde, Hasta]; nulo si el día no es del plazo.</summary>
    public PeriodoDeCdat? PeriodoDe(DateOnly dia) => Periodos.FirstOrDefault(p => dia > p.Desde && dia <= p.Hasta);
}

/// <summary>Cómo se penaliza una redención anticipada (data-model §2.1): reliquidar a la tasa de penalidad o descontar una fracción del interés causado. (nuevo)</summary>
public sealed record PenalidadDeCdat(EarlyRedemptionPenaltyMode Modo, decimal? TasaDePenalidad, decimal? FraccionDeDescuento);

/// <summary>
/// La redención calculada de un CDAT a una fecha (FR-071; contabilidad.md §6 de la 014): los días corridos de la versión vigente, lo causado y no pagado, lo ya
/// pagado del plan, el interés reliquidado a la tasa de penalidad (si el modo es reliquidar), el <b>ajuste de la causación a la baja</b> (≤ 0; la reliquidación
/// devuelve gasto, no es ingreso por penalidad), la penalidad sobre el interés (el descuento) y sobre el capital (lo pagado de más que se recupera), el ajuste
/// de redondeo y el interés bruto que se paga, a la unidad sellada. Sin penalidad (al vencimiento o en la gracia) sólo se redondea. (nuevo)
/// </summary>
public sealed record RedencionDeCdatCalculada(
    int DiasCorridos,
    decimal InteresCausado,
    decimal InteresPagado,
    decimal? InteresReliquidado,
    decimal AjusteDeCausacion,
    decimal PenalidadSobreInteres,
    decimal PenalidadSobreCapital,
    decimal AjustePorRedondeo,
    decimal InteresAPagar,
    string Explicacion)
{
    /// <summary>Lo que se le cobra al titular por redimir antes: el ajuste (en valor absoluto) más las penalidades.</summary>
    public decimal CostoDeLaRedencion => -AjusteDeCausacion + PenalidadSobreInteres + PenalidadSobreCapital;
}

/// <summary>
/// El motor del CDAT (feature 014, US6, T166; research R11; FR-023, FR-067 a FR-073; investigación financiera §3.2 y §3.8). Puro: sin IO, sin reloj y sin
/// valores legales (la tasa, la convención, el redondeo y el calendario hábil llegan por parámetro). Reutiliza <see cref="EquivalenciaDeTasas"/> (la tasa
/// diaria compuesta de la base, a 18 decimales), <see cref="ContadorDeDias"/> y <see cref="CalendarioHabil"/>.
///
/// <list type="bullet">
/// <item><b>Plan</b> (<see cref="Generar"/>): el plazo se cuenta en días calendario desde el inicio y se parte en períodos de los días de la periodicidad
/// (mensual 30, trimestral 90…; el último absorbe lo que sobre); con pago al vencimiento, un período. El interés de un período de n días es
/// <c>C × ((1 + r_d)^n − 1)</c> con <c>r_d = (1 + EA)^(1/B) − 1</c>, es decir <c>i_p = (1 + EA)^(n/B) − 1</c> (FR-023); anticipado,
/// <c>C × i_v / (1 + i_v)</c>. Cada período se redondea con la unidad y el modo sellados (investigación §3.2: 10.000.000 a 180 días al 10 % EA: 481.244 en
/// base 365 y 488.088 en base 360 al vencimiento; 79.741 por mes vencido; 79.111 anticipado).</item>
/// <item><b>Causación</b> (<see cref="CausadoAcumulado"/>): dentro de un período lo causado a k días es <c>Seis(C × ((1 + r_d)^k − 1) × f)</c> con f = 1 vencido
/// o <c>1 / (1 + i_v)</c> anticipado, y el último día absorbe la diferencia de redondeo contra el plan (R11): la causación de cada período suma exactamente su
/// interés.</item>
/// <item><b>Gracia</b> (<see cref="FinDeLaGracia"/>): en días hábiles desde el vencimiento (D17).</item>
/// <item><b>Plazo de emisión</b> (<see cref="PlazoDeEmision"/>): el rango del CUIF 2110 por el plazo pactado en días con meses de 30 (menos de 6 meses; de 6 a
/// 12 inclusive; más de 12 y menos de 18; 18 o más).</item>
/// <item><b>Redención</b> (<see cref="Redimir"/>): reliquidación a la tasa de penalidad sobre los días corridos o descuento de una fracción del interés
/// causado (D18, P17).</item>
/// </list>
/// (nuevo)
/// </summary>
public static class PlanDeCdat
{
    /// <summary>El plazo fuera del rango de la línea (US6-1, api.md §5).</summary>
    public const string CodigoPlazoFueraDeRango = "Lending.TermDeposit.TermOutOfRange";

    /// <summary>La periodicidad o la modalidad de intereses que la línea no admite (api.md §5).</summary>
    public const string CodigoPeriodicidadNoAdmitida = "Lending.TermDeposit.PeriodicityNotAllowed";

    /// <summary>Meses de 30 días: el valor de <see cref="RepaymentPeriodicity.Monthly"/> (nunca un literal, CH39).</summary>
    private const int DiasDelMes = (int)RepaymentPeriodicity.Monthly;

    /// <summary>Seis meses: el valor de <see cref="RepaymentPeriodicity.SemiAnnual"/>.</summary>
    private const int SeisMeses = (int)RepaymentPeriodicity.SemiAnnual;

    /// <summary>Doce meses: el valor de <see cref="RepaymentPeriodicity.Annual"/>.</summary>
    private const int DoceMeses = (int)RepaymentPeriodicity.Annual;

    /// <summary>Dieciocho meses: un año comercial más seis meses.</summary>
    private const int DieciochoMeses = DoceMeses + SeisMeses;

    /// <summary>La tasa diaria compuesta de la EA en la base de la convención (R05): la que sella la versión de condiciones.</summary>
    public static decimal TasaDiaria(decimal efectivaAnual, DayCountConvention convencion) =>
        EquivalenciaDeTasas.TasaDiaria(efectivaAnual, convencion, RateEquivalence.Compound, RepaymentPeriodicity.SinglePayment);

    /// <summary>
    /// El plazo de emisión (FR-073; CUIF 2110 —211005 menos de 6 meses, 211010 de 6 a 12 meses, 211015 más de 12 y menos de 18, 211020 18 meses o más—) por
    /// el plazo pactado en días, con meses de 30 días. Pendiente de validar por la contadora (research E128).
    /// </summary>
    public static IssueTermBand PlazoDeEmision(int plazoDias)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(plazoDias, 1);
        if (plazoDias < SeisMeses) return IssueTermBand.UnderSixMonths;
        if (plazoDias <= DoceMeses) return IssueTermBand.SixToTwelve;
        if (plazoDias < DieciochoMeses) return IssueTermBand.TwelveToEighteen;
        return IssueTermBand.EighteenOrMore;
    }

    /// <summary>
    /// El vencimiento de un plazo: el inicio más los días del plazo (calendario). La fecha de pago es la misma o, si no es hábil, la siguiente hábil sin días
    /// adicionales de interés (D19).
    /// </summary>
    public static DateOnly Vencimiento(DateOnly inicio, int plazoDias) => inicio.AddDays(plazoDias);

    /// <summary>
    /// El último día de la gracia (FR-070, D17): <paramref name="diasHabiles"/> días hábiles contados desde el día siguiente al vencimiento; sin gracia, el
    /// mismo vencimiento. Pasado este día sin instrucción, rige la prórroga automática o el tratamiento de la línea.
    /// </summary>
    public static DateOnly FinDeLaGracia(DateOnly vencimiento, int diasHabiles, CalendarioHabil calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return diasHabiles <= 0 ? vencimiento : calendario.SumarHabiles(vencimiento, diasHabiles);
    }

    /// <summary>Nulo si el plazo cabe en el rango de la línea (US6-1); si no, el problema con el rango vigente.</summary>
    public static ProblemaDeCartera? ValidarPlazo(int plazoDias, int? minimo, int? maximo)
    {
        if (plazoDias < 1 || (minimo is { } min && plazoDias < min) || (maximo is { } max && plazoDias > max))
            return ProblemaDeCartera.Con(CodigoPlazoFueraDeRango,
                $"El plazo de {plazoDias} días está fuera del rango de la línea ({minimo?.ToString() ?? "1"} a {maximo?.ToString() ?? "sin máximo"} días).",
                "termDays", plazoDias);
        return null;
    }

    /// <summary>Nulo si la línea admite la periodicidad y la modalidad de intereses pedidas (FR-028); con pago al vencimiento sólo hay modalidad vencida.</summary>
    public static ProblemaDeCartera? ValidarPeriodicidad(RepaymentPeriodicity periodicidad, InterestTiming modalidad, int plazoDias,
        IReadOnlyCollection<RepaymentPeriodicity> admitidas, IReadOnlyCollection<InterestTiming> modalidades)
    {
        if (admitidas.Count > 0 && !admitidas.Contains(periodicidad))
            return ProblemaDeCartera.Con(CodigoPeriodicidadNoAdmitida, $"La línea no paga intereses con periodicidad «{periodicidad}».", "interestPeriodicity",
                periodicidad.ToString());
        if (modalidades.Count > 0 && !modalidades.Contains(modalidad))
            return ProblemaDeCartera.Con(CodigoPeriodicidadNoAdmitida, $"La línea no paga intereses «{modalidad}».", "timing", modalidad.ToString());
        if (periodicidad == RepaymentPeriodicity.SinglePayment && modalidad == InterestTiming.Advance)
            return ProblemaDeCartera.Con(CodigoPeriodicidadNoAdmitida, "Los intereses al vencimiento se pagan vencidos: no hay pago anticipado al vencimiento.",
                "timing", modalidad.ToString());
        if (periodicidad != RepaymentPeriodicity.SinglePayment && (int)periodicidad > plazoDias)
            return ProblemaDeCartera.Con(CodigoPeriodicidadNoAdmitida,
                $"La periodicidad «{periodicidad}» es más larga que el plazo de {plazoDias} días: elija una menor o el pago al vencimiento.", "interestPeriodicity",
                periodicidad.ToString());
        return null;
    }

    /// <summary>El plan de intereses de unas condiciones (ver la clase). El calendario da las fechas de pago hábiles (D19).</summary>
    public static PlanDeCdatCalculado Generar(CondicionesDeCdat c, CalendarioHabil calendario)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(calendario);
        if (c.Capital <= 0m) throw new ArgumentOutOfRangeException(nameof(c), c.Capital, "El capital de un CDAT es mayor que cero.");
        ArgumentOutOfRangeException.ThrowIfLessThan(c.PlazoDias, 1);
        var vencimiento = Vencimiento(c.Inicio, c.PlazoDias);
        var r = TasaDiaria(c.TasaEfectivaAnual, c.Convencion);
        var paso = c.Periodicidad == RepaymentPeriodicity.SinglePayment ? c.PlazoDias : (int)c.Periodicidad;
        var periodos = new List<PeriodoDeCdat>();
        var desde = c.Inicio;
        var numero = 0;
        while (desde < vencimiento)
        {
            numero++;
            var hasta = desde.AddDays(paso) < vencimiento ? desde.AddDays(paso) : vencimiento;
            var ultimo = hasta == vencimiento;
            var dias = ContadorDeDias.Dias(c.Convencion, desde, hasta);
            var vencida = EquivalenciaDeTasas.TasaDelPeriodo(r, dias, RateEquivalence.Compound);
            var tasa = c.Modalidad == InterestTiming.Advance ? vencida / (1m + vencida) : vencida;
            var interes = c.Redondeo.Redondear(c.Capital * tasa);
            var pago = c.Modalidad == InterestTiming.Advance ? calendario.SiguienteHabil(desde) : calendario.SiguienteHabil(hasta);
            periodos.Add(new PeriodoDeCdat(numero, desde, hasta, pago, dias, EquivalenciaDeTasas.Redondear(tasa), interes, ultimo));
            desde = hasta;
        }
        return new PlanDeCdatCalculado(c, vencimiento, calendario.SiguienteHabil(vencimiento), PlazoDeEmision(c.PlazoDias), r, periodos);
    }

    /// <summary>
    /// Lo causado del período que contiene <paramref name="dia"/> desde su inicio hasta el día inclusive, a seis decimales (R11): <c>Seis(C × ((1 + r_d)^k − 1) ×
    /// f)</c> con k los días corridos en la convención y, el último día del período, su interés del plan exacto. Cero si el día no es del plazo.
    /// </summary>
    public static decimal CausadoAcumulado(PlanDeCdatCalculado plan, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var p = plan.PeriodoDe(dia);
        if (p is null) return 0m;
        if (dia == p.Hasta) return p.Interes;
        var k = ContadorDeDias.Dias(plan.Condiciones.Convencion, p.Desde, dia);
        var vencida = EquivalenciaDeTasas.TasaDelPeriodo(plan.TasaDiaria, k, RateEquivalence.Compound);
        var exacto = plan.Condiciones.Capital * vencida * FactorDeModalidad(plan, p);
        var acumulado = InteresDeCaptacion.Seis(exacto);
        // El acumulado nunca pasa del interés del período: el redondeo hacia abajo del plan lo absorbe el último día.
        return acumulado > p.Interes ? p.Interes : acumulado;
    }

    /// <summary>Lo causado el día <paramref name="dia"/>: la diferencia de acumulados con la víspera dentro del mismo período (el primer día, todo su acumulado).</summary>
    public static decimal CausadoDelDia(PlanDeCdatCalculado plan, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var p = plan.PeriodoDe(dia);
        if (p is null) return 0m;
        var previo = dia.AddDays(-1) > p.Desde ? CausadoAcumulado(plan, dia.AddDays(-1)) : 0m;
        return CausadoAcumulado(plan, dia) - previo;
    }

    /// <summary>
    /// Lo causado en (<paramref name="desdeExcluido"/>, <paramref name="hasta"/>], a seis decimales por día: la suma de los acumulados de los períodos que la
    /// ventana cubre (un período completo cuenta su interés del plan).
    /// </summary>
    public static decimal CausadoEntre(PlanDeCdatCalculado plan, DateOnly desdeExcluido, DateOnly hasta)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var total = 0m;
        foreach (var p in plan.Periodos)
        {
            if (p.Hasta <= desdeExcluido || p.Desde >= hasta) continue;
            var fin = hasta < p.Hasta ? hasta : p.Hasta;
            var inicio = desdeExcluido > p.Desde ? desdeExcluido : p.Desde;
            total += (fin > p.Desde ? CausadoAcumulado(plan, fin) : 0m) - (inicio > p.Desde ? CausadoAcumulado(plan, inicio) : 0m);
        }
        return total;
    }

    /// <summary>
    /// La redención a <paramref name="fecha"/> (FR-071, D18): con <paramref name="penalidad"/> nula (al vencimiento, en la gracia o sin penalidad) se paga lo
    /// causado redondeado; con reliquidación, el interés de la versión se recalcula a la tasa de penalidad sobre los días corridos y lo causado baja hasta lo
    /// reliquidado menos lo ya pagado (si lo pagado lo supera, ese exceso se recupera del capital); con descuento, se descuenta la fracción de lo causado. La
    /// penalidad nunca sube el interés.
    /// </summary>
    public static RedencionDeCdatCalculada Redimir(CondicionesDeCdat c, DateOnly fecha, decimal interesCausadoNoPagado, decimal interesPagado,
        PenalidadDeCdat? penalidad)
    {
        ArgumentNullException.ThrowIfNull(c);
        var dias = fecha > c.Inicio ? ContadorDeDias.Dias(c.Convencion, c.Inicio, fecha) : 0;
        decimal? reliquidado = null;
        decimal ajuste = 0m, sobreInteres = 0m, sobreCapital = 0m;
        string explicacion;
        if (penalidad is null)
        {
            explicacion = $"Sin penalidad: se paga lo causado ({interesCausadoNoPagado:N2}) a la unidad sellada.";
        }
        else if (penalidad.Modo == EarlyRedemptionPenaltyMode.PenaltyRate)
        {
            var tasa = penalidad.TasaDePenalidad ?? 0m;
            var r = TasaDiaria(tasa, c.Convencion);
            var debido = c.Redondeo.Redondear(c.Capital * EquivalenciaDeTasas.TasaDelPeriodo(r, dias, RateEquivalence.Compound));
            reliquidado = debido;
            var queda = debido - interesPagado;
            if (queda >= interesCausadoNoPagado)
            {
                explicacion = $"Reliquidado a {tasa:P4} EA por {dias} días: {debido:N2}, que no baja lo causado; no hay penalidad.";
            }
            else if (queda >= 0m)
            {
                ajuste = queda - interesCausadoNoPagado;
                explicacion = $"Reliquidado a {tasa:P4} EA por {dias} días: {debido:N2}; pagado {interesPagado:N2}; lo causado baja {-ajuste:N2}.";
            }
            else
            {
                ajuste = -interesCausadoNoPagado;
                sobreCapital = -queda;
                explicacion = $"Reliquidado a {tasa:P4} EA por {dias} días: {debido:N2}; pagado {interesPagado:N2}: lo causado se anula y {sobreCapital:N2} pagados de "
                              + "más se descuentan del capital.";
            }
        }
        else
        {
            var fraccion = penalidad.FraccionDeDescuento ?? 0m;
            sobreInteres = c.Redondeo.Redondear(interesCausadoNoPagado * fraccion);
            if (sobreInteres > interesCausadoNoPagado) sobreInteres = c.Redondeo.Redondear(interesCausadoNoPagado);
            explicacion = $"Descuento de {fraccion:P2} de lo causado ({interesCausadoNoPagado:N2}): {sobreInteres:N2}.";
        }
        var neto = interesCausadoNoPagado + ajuste - sobreInteres;
        var aPagar = neto <= 0m ? 0m : c.Redondeo.Redondear(neto);
        return new RedencionDeCdatCalculada(dias, interesCausadoNoPagado, interesPagado, reliquidado, ajuste, sobreInteres, sobreCapital, aPagar - neto, aPagar,
            explicacion);
    }

    private static decimal FactorDeModalidad(PlanDeCdatCalculado plan, PeriodoDeCdat p)
    {
        if (plan.Condiciones.Modalidad != InterestTiming.Advance) return 1m;
        var vencida = EquivalenciaDeTasas.TasaDelPeriodo(plan.TasaDiaria, p.Dias, RateEquivalence.Compound);
        return 1m / (1m + vencida);
    }
}
