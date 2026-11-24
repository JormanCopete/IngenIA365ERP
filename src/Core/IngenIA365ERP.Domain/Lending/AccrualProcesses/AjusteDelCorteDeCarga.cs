using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Lending.AccrualProcesses;

/// <summary>
/// Lo que el recálculo de la carga desde el corte propone para un crédito cargado (2026-11-24, feature 025, fase 3, HU1; FR-003): la primera cuota con
/// vencimiento posterior al corte, el inicio de su período (el vencimiento anterior o el desembolso), los días que el corte le dejó por fuera, el capital
/// no vencido al corte, el interés de su período completo, lo que ya tiene —lo causado desde el corte hasta su vencimiento más lo que trajo en la carga—,
/// el ajuste (la diferencia, al centavo, si pasa de un peso; si no, cero) y la cuota donde se cobra: la primera sin vencer a la fecha de firma. (nuevo)
/// </summary>
public sealed record PropuestaDeAjusteDelCorte(
    int CuotaDelCorte,
    DateOnly InicioDelPeriodo,
    DateOnly Vencimiento,
    int DiasFaltantes,
    decimal CapitalAlCorte,
    decimal InteresDelPeriodo,
    decimal YaCausado,
    decimal Cargado,
    decimal Ajuste,
    int? CuotaDestino);

/// <summary>
/// El recálculo de la carga desde el corte (2026-11-24, feature 025, fase 3, HU1 + HU3; D1, D2), puro:
/// <list type="bullet">
/// <item><b>La tasa</b> (<see cref="MensualRedondaCercana"/>): la mensual vencida con dos decimales en porcentaje (cuatro en fracción) más cercana a la E.A.
/// sellada, si su E.A. derivada difiere menos de 0,005 puntos (23,87 % E.A. → 1,80 % MV); si no, ninguna. Con ella,
/// <see cref="ConTasaMensual"/> arma la entrada de la reconstrucción como quedaría desde el corte: base MV, 30/360 y lineal.</item>
/// <item><b>El interés del período</b> (<see cref="Calcular"/>): el de la primera cuota posterior al corte sobre el capital no vencido al corte, del inicio de
/// su período a su vencimiento con la tasa y la equivalencia de la entrada; menos lo que esa cuota causa desde el corte hasta su vencimiento (la
/// reconstrucción proyectada, sin pagos futuros) y lo que trajo en la carga. Lo que falte pasa de un peso es el ajuste, y va a la primera cuota sin
/// vencer a la fecha de firma: sin mora, porque esa cuota todavía no vence.</item>
/// </list>
/// Sin literales legales: la tolerancia y los decimales son de la regla del dueño (D2), no de una norma. (nuevo)
/// </summary>
public static class AjusteDelCorteDeCarga
{
    /// <summary>Los decimales de la mensual redonda como fracción: dos en porcentaje (1,80 % = 0,0180).</summary>
    public const int DecimalesDeLaMensualRedonda = 4;

    /// <summary>La diferencia máxima, en E.A. como fracción, entre la sellada y la de la mensual redonda: 0,005 puntos (D2).</summary>
    public static decimal Tolerancia { get; } = new(5, 0, 0, false, 5);

    /// <summary>
    /// La mensual vencida redonda (cuatro decimales en fracción) más cercana a <paramref name="efectivaAnual"/> si su E.A. derivada está a menos de
    /// <see cref="Tolerancia"/>; nula si no hay o si la tasa no es positiva.
    /// </summary>
    public static decimal? MensualRedondaCercana(decimal efectivaAnual)
    {
        if (efectivaAnual <= 0m) return null;
        var exacta = EquivalenciaDeTasas.NominalDelPeriodo(efectivaAnual, RepaymentPeriodicity.Monthly);
        var redonda = decimal.Round(exacta, DecimalesDeLaMensualRedonda, MidpointRounding.AwayFromZero);
        if (redonda <= 0m) return null;
        return Math.Abs(EquivalenciaDeTasas.EfectivaDeMensual(redonda) - efectivaAnual) < Tolerancia ? redonda : null;
    }

    /// <summary>
    /// La entrada de la reconstrucción con la corriente en mensual vencida <paramref name="mensual"/>: base MV, la E.A. derivada a seis decimales,
    /// 30/360 y lineal (FR-008). La mora, el redondeo y lo demás no cambian.
    /// </summary>
    public static EntradaDeReconstruccion ConTasaMensual(EntradaDeReconstruccion entrada, decimal mensual)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        return entrada with
        {
            Condiciones = entrada.Condiciones with
            {
                TasaEfectivaAnual = EquivalenciaDeTasas.EfectivaInformativaDeMensual(mensual),
                TasaMensual = mensual,
                Convencion = DayCountConvention.Thirty360,
                Equivalencia = RateEquivalence.Linear,
            },
        };
    }

    /// <summary>
    /// Lo que se propone para el crédito de <paramref name="entrada"/> (ya con la tasa que regirá desde el corte) con corte <paramref name="corte"/>,
    /// firmado <paramref name="hoy"/>; <paramref name="desembolso"/> es el inicio del período de la cuota 1 si es la primera del crédito. Nula si el plan no
    /// tiene cuota posterior al corte.
    /// </summary>
    public static PropuestaDeAjusteDelCorte? Calcular(EntradaDeReconstruccion entrada, DateOnly corte, DateOnly hoy, DateOnly? desembolso = null)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var c = entrada.Condiciones;
        var alCorte = ReconstruccionDeCredito.Reconstruir(entrada, corte);
        var q = alCorte.Plan?.Cuotas.FirstOrDefault(x => x.Vencimiento > corte);
        if (q is null || alCorte.Plan is null) return null;

        var inicio = InicioDelPeriodo(alCorte.Plan.Cuotas, q.Numero, q.Desde, q.Vencimiento, corte, c.Periodicidad, desembolso);
        var capital = alCorte.Cuotas.Where(x => x.Vencimiento > corte).Sum(x => x.CapitalPendiente);
        var diaria = EquivalenciaDeTasas.TasaDiaria(TasaPactada.De(c.TasaEfectivaAnual, c.TasaMensual), c.Convencion, c.Equivalencia, c.Periodicidad);
        var periodo = c.Redondeo.Concepto(capital * EquivalenciaDeTasas.TasaDelPeriodo(diaria, ContadorDeDias.Dias(c.Convencion, inicio, q.Vencimiento),
            c.Equivalencia));
        var faltantes = inicio < corte ? ContadorDeDias.Dias(c.Convencion, inicio, corte) : 0;

        // Lo que la cuota causa desde el corte hasta su vencimiento (proyectado si vence después de hoy) y lo que trajo en la carga.
        var proyectado = ReconstruccionDeCredito.Reconstruir(entrada, q.Vencimiento, corte.AddDays(1));
        var causado = proyectado.Libro.Movimientos
            .Where(m => m.Clase == LoanTransactionKind.Accrual && m.Cuota == q.Numero && m.Dia > corte && m.Dia <= q.Vencimiento
                        && m.Concepto is LoanConcept.CurrentInterest or LoanConcept.CurrentInterestInSuspense)
            .Sum(m => m.Monto);
        var cargado = entrada.Operaciones.Where(o => o.Clase == LoanOperationKind.OpeningBalance).SelectMany(o => o.Saldos)
            .Where(s => s.Cuota == q.Numero && s.Concepto is LoanConcept.CurrentInterest or LoanConcept.CurrentInterestInSuspense)
            .Sum(s => s.Monto);

        var falta = c.Redondeo.Concepto(periodo - causado - cargado);
        var ajuste = falta > 1m ? falta : 0m;
        var enHoy = ReconstruccionDeCredito.Reconstruir(entrada, hoy);
        var destino = enHoy.Plan?.Cuotas.FirstOrDefault(x => x.Vencimiento > hoy)?.Numero;
        return new PropuestaDeAjusteDelCorte(q.Numero, inicio, q.Vencimiento, faltantes, capital, periodo, causado, cargado, ajuste, destino);
    }

    /// <summary>
    /// El inicio del período de la cuota <paramref name="numero"/>: el vencimiento de la anterior si el plan la trae; si no, su <c>Desde</c> cargado si es
    /// anterior al corte; si es la cuota 1, el desembolso; si no, su vencimiento menos un período de la periodicidad (los meses enteros con el calendario,
    /// lo demás en días).
    /// </summary>
    public static DateOnly InicioDelPeriodo(IReadOnlyList<Schedules.CuotaDelPlan> cuotas, int numero, DateOnly desde, DateOnly vencimiento, DateOnly corte,
        RepaymentPeriodicity periodicidad, DateOnly? desembolso)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        if (cuotas.FirstOrDefault(x => x.Numero == numero - 1) is { } anterior) return anterior.Vencimiento;
        if (desde < corte) return desde;
        if (numero == 1 && desembolso is { } d && d < vencimiento) return d;
        return VencimientoAnterior(vencimiento, periodicidad);
    }

    // ------------------------------------------------------------------------------------- fase 4: la carga con período completo --

    /// <summary>
    /// 2026-11-24 (feature 025, fase 4, HU4; FR-014): las cuotas de un plan migrado con el inicio del período de la primera posterior al corte
    /// (<c>Desde</c>) cuando la carga causa completo el período en curso: el vencimiento de la cuota cargada anterior (el mismo que ya le pone
    /// <see cref="Schedules.GeneradorDePlan.DesdeMigracion"/>); si no la hay y es la cuota 1, el desembolso (si es anterior a su vencimiento); si no, su
    /// vencimiento menos un período de la periodicidad (<see cref="VencimientoAnterior"/>). Las demás cuotas quedan como vinieron. Es la misma regla de
    /// <see cref="InicioDelPeriodo"/>: con eso el plan muestra el período completo, el interés lo sigue causando el motor desde el corte y lo anterior entra
    /// como ajuste del corte el día de la activación. (nuevo)
    /// </summary>
    public static IReadOnlyList<Schedules.CuotaMigrada> ConPeriodoCompleto(IReadOnlyList<Schedules.CuotaMigrada> cuotas, DateOnly corte,
        RepaymentPeriodicity periodicidad, DateOnly? desembolso)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        var ordenadas = cuotas.OrderBy(c => c.Numero).ToList();
        var q = ordenadas.FirstOrDefault(c => c.Vencimiento > corte);
        if (q is null || q.Desde is { } dado && dado < corte) return ordenadas;
        var anterior = ordenadas.LastOrDefault(c => c.Numero < q.Numero);
        var inicio = anterior is not null ? anterior.Vencimiento
            : q.Numero == 1 && desembolso is { } d && d < q.Vencimiento ? d
            : VencimientoAnterior(q.Vencimiento, periodicidad);
        if (inicio >= corte) return ordenadas;
        return ordenadas.Select(c => ReferenceEquals(c, q) ? c with { Desde = inicio } : c).ToList();
    }

    /// <summary>
    /// 2026-11-24 (feature 025, fase 4, HU4; FR-014, FR-015): lo que la opción «el período en curso al corte se causa completo» suma a un crédito cargado,
    /// sin reconstrucción —la vista previa de la carga, antes de que nazca—: sobre el plan migrado (<see cref="Schedules.GeneradorDePlan.DesdeMigracion"/>
    /// con las cuotas de <see cref="ConPeriodoCompleto"/>), la primera cuota posterior al corte, el interés de su período completo sobre su saldo inicial con
    /// la tasa pactada del plan, lo que el plan causa desde el corte (su interés) y lo que trajo en la carga (<paramref name="cargado"/>). El ajuste es la
    /// diferencia redondeada como un concepto si pasa de un peso, como en <see cref="Calcular"/>; si no, cero. La cuota destino es la misma cuota: vence
    /// después del corte, así que el día de la activación todavía no está vencida. Nula si el plan no tiene cuota posterior al corte. La activación no usa
    /// esto sino <see cref="Calcular"/> sobre la reconstrucción (que además deja fuera al crédito con la causación suspendida); para un crédito al día dan
    /// lo mismo. (nuevo)
    /// </summary>
    public static PropuestaDeAjusteDelCorte? PeriodoCompleto(Schedules.PlanDePagos plan, DateOnly corte, decimal cargado)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var q = plan.Cuotas.FirstOrDefault(x => x.Vencimiento > corte);
        if (q is null) return null;
        var e = plan.Entrada;
        var inicio = q.Desde < corte ? q.Desde : corte;
        var periodo = e.Redondeo.Concepto(q.SaldoInicial
            * EquivalenciaDeTasas.TasaDelPeriodo(plan.TasaDiaria, ContadorDeDias.Dias(e.Convencion, inicio, q.Vencimiento), e.Equivalencia));
        var faltantes = inicio < corte ? ContadorDeDias.Dias(e.Convencion, inicio, corte) : 0;
        var falta = e.Redondeo.Concepto(periodo - q.Interes - cargado);
        return new PropuestaDeAjusteDelCorte(q.Numero, inicio, q.Vencimiento, faltantes, q.SaldoInicial, periodo, q.Interes, cargado,
            falta > 1m ? falta : 0m, q.Numero);
    }

    /// <summary>El vencimiento un período antes de <paramref name="vencimiento"/>: meses con el calendario, lo demás en días (del valor del enum).</summary>
    public static DateOnly VencimientoAnterior(DateOnly vencimiento, RepaymentPeriodicity periodicidad)
    {
        var dias = (int)periodicidad;
        if (dias <= 0) return vencimiento;
        return dias % ContadorDeDias.MesComercial == 0 ? vencimiento.AddMonths(-(dias / ContadorDeDias.MesComercial)) : vencimiento.AddDays(-dias);
    }
}
