using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>
/// Lo que un CDAT migrado deja calcular a su plan (feature 014, US6, pendiente de la fase 10; plantillas-y-archivos.md §4.3; data-model §7.1; research E148):
/// las condiciones de la versión vigente al corte con la convención y el redondeo de la versión de la línea al corte, el plan que regenera
/// <see cref="PlanDeCdat"/> desde su inicio vigente, los períodos que ya pagó el sistema anterior (fecha de pago ≤ corte) y lo que el plan espera de la columna
/// <c>interesCausadoNoPagado</c>: lo causado hasta el corte menos lo pagado de los períodos ya terminados —vencido: lo causado y no pagado; anticipado: lo
/// causado del período en curso—. El saldo del interés por pagar con que nace el CDAT es esa columna menos lo pagado por anticipado de los períodos que no han
/// terminado (en un CDAT anticipado queda negativo: el titular ya recibió lo que todavía no se causa, contabilidad.md §6). Puro. (nuevo)
/// </summary>
public static class CdatMigrado
{
    /// <summary>La diferencia máxima entre el interés causado que trae la fila y el que regenera el plan (§4.3: «más de un peso»).</summary>
    public const decimal Tolerancia = 1m;

    public const string CodigoPlanDistinto = "Lending.TermDeposit.ScheduleMismatch";

    /// <summary>Un calendario sin festivos (sábado y domingo no hábiles): basta cuando sólo importan los intereses, que no dependen de las fechas de pago.</summary>
    public static CalendarioHabil SinFestivos { get; } = new([], [DayOfWeek.Saturday, DayOfWeek.Sunday]);

    public static CondicionesDeCdat Condiciones(MigrationTermDepositRow r, DepositLineVersion linea) =>
        new(r.PrincipalAmount, r.AppliedAnnualRate, r.StartDate, r.TermDays, r.InterestPeriodicity, r.InterestTiming, linea.DayCount, linea.Redondeo());

    /// <summary>Los períodos pagados al corte por el sistema anterior: los que tienen fecha de pago a más tardar el corte (como los lee el ciclo del CDAT).</summary>
    public static IReadOnlyList<PeriodoDeCdat> Pagados(PlanDeCdatCalculado plan, DateOnly corte) => plan.Periodos.Where(p => p.FechaDePago <= corte).ToList();

    /// <summary>Lo causado del plan hasta el corte (los períodos terminados enteros; el que está en curso, su acumulado).</summary>
    public static decimal CausadoAlCorte(PlanDeCdatCalculado plan, DateOnly corte) =>
        plan.Periodos.Where(p => p.Desde < corte).Sum(p => p.Hasta <= corte ? p.Interes : PlanDeCdat.CausadoAcumulado(plan, corte));

    /// <summary>Lo que el plan espera de la columna <c>interesCausadoNoPagado</c> (ver la clase).</summary>
    public static decimal EsperadoAlCorte(PlanDeCdatCalculado plan, DateOnly corte) =>
        CausadoAlCorte(plan, corte) - Pagados(plan, corte).Where(p => p.Hasta <= corte).Sum(p => p.Interes);

    /// <summary>
    /// El saldo del interés por pagar con que nace el CDAT: la columna menos lo pagado de los períodos que no han terminado (sólo en anticipado). Con un plan
    /// sin festivos: los intereses no dependen de las fechas de pago, y en anticipado se pagan al empezar el período.
    /// </summary>
    public static decimal SaldoDeInteres(MigrationTermDepositRow r, DepositLineVersion? linea, DateOnly corte)
    {
        if (r.InterestTiming != InterestTiming.Advance || linea is null) return r.InterestPayable;
        var plan = PlanDeCdat.Generar(Condiciones(r, linea), SinFestivos);
        return r.InterestPayable - plan.Periodos.Where(p => p.Numero <= r.PaidPeriods && p.Hasta > corte).Sum(p => p.Interes);
    }
}
