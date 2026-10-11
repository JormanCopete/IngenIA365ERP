using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Lending.Schedules;

/// <summary>
/// El plan de pagos (feature 013, T29, R37, M8; FR-022, FR-023, SC-017): proyecta la causación suponiendo pagos a tiempo. El interés
/// de cada cuota es el que causa el saldo de capital en su período —<c>saldo × ((1 + r_d)^n − 1)</c> en compuesta o
/// <c>saldo × r_d × n</c> en lineal, con n los días de la convención—: el período empieza en el vencimiento anterior (o el desembolso)
/// y la ventana se reinicia en cada vencimiento, así el interés nunca capitaliza.
///
/// <list type="bullet">
/// <item><b>Cuota fija</b> (<see cref="AmortizationSystem.LevelPayment"/>) por <b>valor presente con los factores reales de cada
/// período</b>: <c>A = (saldo − Σ E_k·v_k) / Σ v_k</c> con <c>v_k = Π 1 / (1 + i_j)</c>; así un primer período irregular y las
/// extraordinarias E_k (primas) entran con su peso exacto. Si el interés de un período supera la cuota (primer período muy largo),
/// esa cuota no amortiza y la cuota se recalcula sobre las restantes, porque el interés no capitaliza.</item>
/// <item><b>Abono constante</b>: <c>(monto − Σ E) / cuotas que amortizan</c> por cuota, más la extraordinaria que toque.</item>
/// <item><b>Pago único</b>: capital e interés del plazo entero al vencimiento (periodicidad <see cref="RepaymentPeriodicity.SinglePayment"/>).</item>
/// <item><b>Gracia a capital</b>: los primeros períodos son cuotas de sólo interés. <b>Gracia total</b>: períodos sin cuota cuyo
/// interés, sin capitalizar (suma de <c>monto × i_j</c>), va en la primera cuota o se distribuye en N cuotas; sus seguros, en la
/// primera.</item>
/// <item>Seguros por su base y periodicidad (D9): base × tarifa en la cuota del período; si la periodicidad de cobro es otra, en
/// proporción a los días del enum.</item>
/// <item><b>Σ capital = monto</b> al centavo: la última cuota paga el saldo y absorbe el residuo, que queda en
/// <see cref="PlanDePagos.ResiduoDeLaUltimaCuota"/> y en la explicación. Si no cuadra es un defecto del programa
/// (<see cref="CodigoCapitalDescuadrado"/>), nunca un dato.</item>
/// </list>
///
/// <para>Sin IO, sólo <c>DateOnly</c> y <c>decimal</c>; sin valores legales. La regeneración por operación (abono, reajuste, nota de
/// Comercial, tramo) la agregan US4 y US5 sobre esta misma clase. (nuevo)</para>
/// </summary>
public static class GeneradorDePlan
{
    /// <summary>Σ capital del plan ≠ monto: nunca debe ocurrir (SC-017).</summary>
    public const string CodigoCapitalDescuadrado = "Lending.Schedule.PrincipalMismatch";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static PlanDePagos Generar(EntradaDePlan e)
    {
        Validar(e);
        var r = e.Redondeo;
        var n = e.Periodos;
        var tasaDiaria = EquivalenciaDeTasas.TasaDiaria(e.TasaEfectivaAnual, e.Convencion, e.Equivalencia, e.Periodicidad);
        var nominal = EquivalenciaDeTasas.NominalDelPeriodo(e.TasaEfectivaAnual, e.Periodicidad);
        var fechas = FechasDeVencimiento.Generar(e.FechaDesembolso, e.Periodicidad, n, e.DiaDePago, e.PrimerVencimiento);

        var desde = new DateOnly[n];
        var dias = new int[n];
        var tasas = new decimal[n];
        for (var k = 0; k < n; k++)
        {
            desde[k] = k == 0 ? e.FechaDesembolso : fechas[k - 1];
            dias[k] = ContadorDeDias.Dias(e.Convencion, desde[k], fechas[k]);
            tasas[k] = EquivalenciaDeTasas.TasaDelPeriodo(tasaDiaria, dias[k], e.Equivalencia);
        }

        var g = e.Gracia == GraceKind.None ? 0 : e.PeriodosDeGracia;
        var graciaTotal = e.Gracia == GraceKind.Total;
        var tratamiento = e.TratamientoDeGracia ?? GraceInterestTreatment.FirstInstallment;
        var amortizan = n - g;

        var extra = new decimal[n];
        foreach (var x in e.Extraordinarias)
            extra[graciaTotal ? x.Cuota - 1 + g : x.Cuota - 1] += r.Concepto(x.Valor);

        var explicacion = new ExplicacionDeCartera()
            .Linea($"Reglas {VersionesDeReglas.Vigente}; convención {e.Convencion}, equivalencia {e.Equivalencia}, redondeo a "
                + $"{ExplicacionDeCartera.Numero(r.Unidad)} {r.Modo} {r.Momento}.")
            .Linea(TextoDeLaTasaDiaria(e, tasaDiaria))
            .Linea($"Nominal del período (informativa): (1 + {ExplicacionDeCartera.Numero(e.TasaEfectivaAnual)})^(1/"
                + $"{EquivalenciaDeTasas.PeriodosPorAnio(e.Periodicidad).ToString(Inv)}) − 1 = {ExplicacionDeCartera.Numero(nominal)}.");

        var a = 0m;
        decimal? cuotaNivelada = null;
        if (e.Sistema == AmortizationSystem.LevelPayment)
        {
            a = Nivelada(e.Monto, g, tasas, extra);
            cuotaNivelada = CuotaRedondeada(r, a);
            explicacion.Linea($"Cuota nivelada por valor presente con los factores reales de cada período: A = {ExplicacionDeCartera.Numero(a)} → "
                + $"{ExplicacionDeCartera.Numero(cuotaNivelada.Value)}.");
        }
        var abono = e.Sistema == AmortizationSystem.ConstantPrincipal ? (e.Monto - extra.Sum()) / amortizan : 0m;

        var saldo = e.Monto;
        var interesGraciaExacto = 0m;
        var seguroGraciaExacto = new decimal[e.Seguros.Count];
        var interesDeGracia = 0m;
        var porcionDeGracia = 0m;
        var graciaRepartida = 0m;
        var residuo = 0m;
        var cuotas = new List<CuotaDelPlan>(amortizan + (graciaTotal ? 0 : g));

        for (var k = 0; k < n; k++)
        {
            if (graciaTotal && k < g)
            {
                var exacto = saldo * tasas[k];
                interesGraciaExacto += exacto;
                for (var s = 0; s < e.Seguros.Count; s++)
                    seguroGraciaExacto[s] += SeguroExacto(e, e.Seguros[s], saldo, dias[k], esPrimerPago: false);
                explicacion.Tramo(new TramoDeCartera(desde[k], fechas[k], dias[k], tasaDiaria, saldo, exacto, "GraceInterest"));
                continue;
            }

            var numero = graciaTotal ? k - g + 1 : k + 1;
            var esUltimo = k == n - 1;
            var saldoInicial = saldo;
            var interes = r.Concepto(saldo * tasas[k]);

            var porcion = 0m;
            if (graciaTotal)
            {
                if (k == g)
                {
                    interesDeGracia = r.Concepto(interesGraciaExacto);
                    porcionDeGracia = tratamiento == GraceInterestTreatment.Distributed
                        ? r.Concepto(interesDeGracia / e.CuotasDeDistribucion!.Value)
                        : interesDeGracia;
                    explicacion.Linea($"Interés de la gracia total, sin capitalizar: {ExplicacionDeCartera.Numero(interesDeGracia)}, "
                        + (tratamiento == GraceInterestTreatment.Distributed
                            ? $"distribuido en {e.CuotasDeDistribucion!.Value.ToString(Inv)} cuotas."
                            : "en la primera cuota."));
                }
                if (tratamiento == GraceInterestTreatment.Distributed)
                {
                    // Cada una de las N lleva su parte al redondeo sellado y la N-ésima lo que falte; ninguna pasa de lo que queda.
                    var d = e.CuotasDeDistribucion!.Value;
                    var pendiente = interesDeGracia - graciaRepartida;
                    porcion = numero < d ? Math.Min(porcionDeGracia, pendiente) : numero == d ? pendiente : 0m;
                    graciaRepartida += porcion;
                }
                else if (k == g)
                {
                    porcion = interesDeGracia;
                }
            }
            var interesTotal = interes + porcion;

            var seguros = new List<SeguroDeCuota>(e.Seguros.Count);
            for (var s = 0; s < e.Seguros.Count; s++)
            {
                var exacto = SeguroExacto(e, e.Seguros[s], saldoInicial, dias[k], esPrimerPago: numero == 1);
                if (graciaTotal && k == g) exacto += seguroGraciaExacto[s];
                seguros.Add(new SeguroDeCuota(e.Seguros[s].Codigo, r.Concepto(exacto), e.Seguros[s].Computa));
            }
            var seguro = seguros.Sum(s => s.Valor);

            var soloInteres = !graciaTotal && k < g;
            decimal capital;
            if (soloInteres)
            {
                capital = 0m;
            }
            else
            {
                var teorico = e.Sistema switch
                {
                    AmortizationSystem.LevelPayment => r.PorCuota
                        ? r.Redondear(a + extra[k] + porcion + seguro) - interesTotal - seguro
                        : CuotaRedondeada(r, a) + extra[k] - interes,
                    AmortizationSystem.ConstantPrincipal => r.PorCuota
                        ? r.Redondear(abono + extra[k] + interesTotal + seguro) - interesTotal - seguro
                        : r.Concepto(abono) + extra[k],
                    _ => e.Monto,
                };
                if (esUltimo)
                {
                    capital = saldo;
                    residuo = capital - teorico;
                }
                else
                {
                    capital = teorico;
                    if (capital < 0m)
                    {
                        explicacion.Linea($"La cuota {numero.ToString(Inv)} no alcanza a cubrir el interés de su período: no amortiza y la cuota "
                            + "se recalcula sobre las restantes (el interés no capitaliza).");
                        capital = 0m;
                        if (e.Sistema == AmortizationSystem.LevelPayment)
                            a = Nivelada(saldo, k + 1, tasas, extra);
                    }
                    if (capital > saldo) capital = saldo;
                }
            }

            saldo -= capital;
            cuotas.Add(new CuotaDelPlan(numero, desde[k], fechas[k], dias[k], tasas[k], saldoInicial, capital, interesTotal, porcion,
                seguros, saldo, soloInteres));
            explicacion.Tramo(new TramoDeCartera(desde[k], fechas[k], dias[k], tasaDiaria, saldoInicial, interes, nameof(LoanConcept.CurrentInterest), numero));
        }

        var sumaDeCapital = cuotas.Sum(c => c.Capital);
        if (sumaDeCapital != e.Monto)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoCapitalDescuadrado,
                $"El capital del plan suma {ExplicacionDeCartera.Numero(sumaDeCapital)} y el monto es {ExplicacionDeCartera.Numero(e.Monto)}.",
                "difference", sumaDeCapital - e.Monto));

        explicacion.Linea($"La última cuota paga el saldo de capital y absorbe el residuo de redondeo: {ExplicacionDeCartera.Numero(residuo)}.");
        return new PlanDePagos(VersionesDeReglas.Vigente, e, tasaDiaria, nominal, cuotaNivelada, interesDeGracia, residuo, cuotas, explicacion);
    }

    /// <summary>
    /// La cuota nivelada exacta que amortiza <paramref name="saldo"/> en los períodos desde <paramref name="desde"/> (base 0) hasta el
    /// último, descontando las extraordinarias con los factores reales: <c>(saldo − Σ E_k·v_k) / Σ v_k</c>.
    /// </summary>
    private static decimal Nivelada(decimal saldo, int desde, IReadOnlyList<decimal> tasas, IReadOnlyList<decimal> extra)
    {
        var v = 1m;
        var sumaDeFactores = 0m;
        var extraordinariasEnValorPresente = 0m;
        for (var j = desde; j < tasas.Count; j++)
        {
            v /= 1m + tasas[j];
            sumaDeFactores += v;
            extraordinariasEnValorPresente += extra[j] * v;
        }
        return (saldo - extraordinariasEnValorPresente) / sumaDeFactores;
    }

    /// <summary>La cuota nivelada como concepto: a la unidad por concepto o por cuota; al centavo por total.</summary>
    private static decimal CuotaRedondeada(RedondeoDeCartera r, decimal a) =>
        r.Momento == RoundingMoment.PerTotal ? r.RedondearAlCentavo(a) : r.Redondear(a);

    /// <summary>Lo que causa un seguro en el período, sin redondear (D9).</summary>
    private static decimal SeguroExacto(EntradaDePlan e, SeguroDelPlan s, decimal saldoInicial, int diasDelPeriodo, bool esPrimerPago)
    {
        var baseDelSeguro = s.Base == InsuranceBase.OutstandingBalance ? saldoInicial : e.Monto;
        decimal factor;
        if (s.PeriodicidadDeCobro == RepaymentPeriodicity.SinglePayment) factor = esPrimerPago ? 1m : 0m;
        else if (s.PeriodicidadDeCobro == e.Periodicidad) factor = 1m;
        else if (e.Periodicidad == RepaymentPeriodicity.SinglePayment) factor = (decimal)diasDelPeriodo / (int)s.PeriodicidadDeCobro;
        else factor = (decimal)(int)e.Periodicidad / (int)s.PeriodicidadDeCobro;
        return baseDelSeguro * s.Tarifa * factor;
    }

    private static string TextoDeLaTasaDiaria(EntradaDePlan e, decimal tasaDiaria)
    {
        var b = ContadorDeDias.Base(e.Convencion).ToString(Inv);
        var ea = ExplicacionDeCartera.Numero(e.TasaEfectivaAnual);
        return e.Equivalencia == RateEquivalence.Linear
            ? $"Tasa diaria lineal (D1): NA_m = m·((1 + {ea})^(1/m) − 1) con m = {EquivalenciaDeTasas.PeriodosPorAnio(e.Periodicidad).ToString(Inv)}; "
              + $"r_d = NA_m / {b} = {ExplicacionDeCartera.Numero(tasaDiaria)}, redondeada a {EquivalenciaDeTasas.DecimalesDeTasa.ToString(Inv)} decimales."
            : $"Tasa diaria: RaizEntera(1 + {ea}, {b}) − 1 = {ExplicacionDeCartera.Numero(tasaDiaria)}, redondeada a "
              + $"{EquivalenciaDeTasas.DecimalesDeTasa.ToString(Inv)} decimales (la única aproximación del motor).";
    }

    private static void Validar(EntradaDePlan e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(e.Redondeo);
        if (e.Monto <= 0m) throw new ArgumentOutOfRangeException(nameof(e), e.Monto, "El monto es mayor que cero.");
        if (decimal.Round(e.Monto, 2) != e.Monto) throw new ArgumentException("El monto va en pesos con centavos.", nameof(e));
        ArgumentOutOfRangeException.ThrowIfLessThan(e.Periodos, 1, nameof(e.Periodos));

        var unico = e.Periodicidad == RepaymentPeriodicity.SinglePayment;
        if (unico != (e.Sistema == AmortizationSystem.Bullet))
            throw new ArgumentException("El pago único va con la periodicidad de pago único, y sólo con ella.", nameof(e));
        if (!Enum.IsDefined(e.Sistema)) throw new ArgumentOutOfRangeException(nameof(e), e.Sistema, "Sistema de amortización desconocido.");

        switch (e.Gracia)
        {
            case GraceKind.None:
                if (e.PeriodosDeGracia != 0) throw new ArgumentException("Sin gracia no hay períodos de gracia.", nameof(e));
                break;
            case GraceKind.Principal or GraceKind.Total:
                if (unico) throw new ArgumentException("El pago único no admite gracia.", nameof(e));
                if (e.PeriodosDeGracia < 1 || e.PeriodosDeGracia >= e.Periodos)
                    throw new ArgumentException("La gracia dura al menos un período y deja al menos una cuota que amortice.", nameof(e));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(e), e.Gracia, "Clase de gracia desconocida.");
        }

        var amortizan = e.Periodos - (e.Gracia == GraceKind.None ? 0 : e.PeriodosDeGracia);
        if (e.Gracia == GraceKind.Total && e.TratamientoDeGracia == GraceInterestTreatment.Distributed
            && (e.CuotasDeDistribucion is not { } d || d < 1 || d > amortizan))
            throw new ArgumentException("El interés de la gracia distribuido necesita el número de cuotas, entre 1 y las que amortizan.", nameof(e));

        var pagos = e.Gracia == GraceKind.Total ? amortizan : e.Periodos;
        foreach (var x in e.Extraordinarias)
        {
            if (unico) throw new ArgumentException("El pago único no admite cuotas extraordinarias.", nameof(e));
            if (x.Valor <= 0m) throw new ArgumentException("Una cuota extraordinaria vale más que cero.", nameof(e));
            if (x.Cuota < 1 || x.Cuota > pagos || (e.Gracia == GraceKind.Principal && x.Cuota <= e.PeriodosDeGracia))
                throw new ArgumentException($"La cuota extraordinaria {x.Cuota.ToString(Inv)} no es una cuota que amortice.", nameof(e));
        }
        if (e.Extraordinarias.Sum(x => x.Valor) >= e.Monto)
            throw new ArgumentException("Las extraordinarias no pueden sumar el monto.", nameof(e));
        foreach (var s in e.Seguros)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(s.Codigo, nameof(e));
            if (s.Tarifa < 0m) throw new ArgumentException($"La tarifa del seguro {s.Codigo} no puede ser negativa.", nameof(e));
        }
    }
}
