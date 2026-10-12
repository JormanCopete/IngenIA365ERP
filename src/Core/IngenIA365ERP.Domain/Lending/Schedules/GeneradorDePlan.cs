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
                seguros, saldo, soloInteres) { Extraordinaria = extra[k] });
            explicacion.Tramo(new TramoDeCartera(desde[k], fechas[k], dias[k], tasaDiaria, saldoInicial, interes, nameof(LoanConcept.CurrentInterest), numero));
        }

        var sumaDeCapital = cuotas.Sum(c => c.Capital);
        if (sumaDeCapital != e.Monto)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoCapitalDescuadrado,
                $"El capital del plan suma {ExplicacionDeCartera.Numero(sumaDeCapital)} y el monto es {ExplicacionDeCartera.Numero(e.Monto)}.",
                "difference", sumaDeCapital - e.Monto));

        explicacion.Linea($"La última cuota paga el saldo de capital y absorbe el residuo de redondeo: {ExplicacionDeCartera.Numero(residuo)}.");
        return new PlanDePagos(VersionesDeReglas.Vigente, e, tasaDiaria, nominal, cuotaNivelada, interesDeGracia, residuo, cuotas, explicacion)
        {
            PeriodosDeGracia = graciaTotal ? Enumerable.Range(0, g).Select(k => new PeriodoDeGracia(desde[k], fechas[k])).ToList() : [],
        };
    }

    /// <summary>No quedan cuotas que venzan después de la fecha y el hecho cambia el capital (US4).</summary>
    public const string CodigoSinCuotasPorRegenerar = "Lending.Schedule.NothingToRegenerate";

    /// <summary>Regenerar dentro de una gracia total: no está soportado en K1 (US4).</summary>
    public const string CodigoRegeneracionEnGraciaTotal = "Lending.Schedule.RegenerationInTotalGrace";

    /// <summary>
    /// La <b>regeneración</b> del plan por un hecho con fecha valor (US4, T0486; D12, FR-067, R37): desde la primera cuota que vence
    /// después de la fecha, conservando su número y sus fechas; las anteriores quedan como estaban.
    /// <list type="bullet">
    /// <item>La primera cuota regenerada causa en tramos: lo que ya corrió hasta la fecha con la base y la tasa anteriores, y desde la
    /// fecha con la base cambiada (más el tramo, menos el abono) y la tasa nueva (reajuste). Su interés es el redondeo de la suma exacta
    /// de sus tramos, como lo causa la causación (T25).</item>
    /// <item>Cuota fija: la nivelada que lleva el saldo a cero con ese primer interés ya fijo y los factores reales de las siguientes
    /// (las de sólo interés de una gracia a capital no amortizan); abono constante: el capital pendiente entre las que amortizan; pago
    /// único: el saldo al vencimiento.</item>
    /// <item>El seguro de la primera regenerada no cambia (su período ya empezó); el de las siguientes se recalcula sobre su saldo
    /// inicial, o sobre lo desembolsado si su base es el monto (D9).</item>
    /// <item>Σ capital = lo desembolsado menos lo abonado y la última absorbe el residuo; si no cuadra,
    /// <see cref="CodigoCapitalDescuadrado"/>.</item>
    /// </list>
    /// US5 (T0536; FR-075, SC-017) agrega la elección del deudor ante un abono extraordinario (<see cref="EntradaDeRegeneracion.Eleccion"/>):
    /// <list type="bullet">
    /// <item><see cref="PrepaymentChoice.ReduceInstallment"/> (y la regeneración sin elección): la de arriba, mismas cuotas y valor nuevo.</item>
    /// <item><see cref="PrepaymentChoice.ReduceTerm"/>: en cuota fija se conserva la cuota nivelada sellada y se amortiza con ella hasta que el
    /// saldo se acaba; la que lo termina paga el saldo (absorbe el residuo) y las siguientes desaparecen. En los demás sistemas (y en un plan
    /// migrado, que no tiene nivelada) se conserva el capital de cada cuota y el abono se quita desde la última. Siempre queda la primera
    /// regenerada, que ya causa.</item>
    /// </list>
    /// </summary>
    public static PlanDePagos Regenerar(PlanDePagos anterior, EntradaDeRegeneracion r) => Regenerar(anterior, r, null);

    /// <summary>
    /// La regeneración con el capital que los pagos ya movieron en cada cuota (US5): lo que la reconstrucción lleva fuera del plan en las
    /// cuotas que vencen después de la fecha se consolida en el cambio de capital, para que el plan nuevo cuadre con el capital pendiente
    /// (quien llama deja esos ajustes en cero). Sin ajustes es <see cref="Regenerar(PlanDePagos, EntradaDeRegeneracion)"/>.
    /// </summary>
    /// <param name="ajustesDeCapital">Por número de cuota, el capital pendiente menos el del plan (negativo si se pagó).</param>
    public static PlanDePagos Regenerar(PlanDePagos anterior, EntradaDeRegeneracion r, IReadOnlyDictionary<int, decimal>? ajustesDeCapital)
    {
        ArgumentNullException.ThrowIfNull(anterior);
        ArgumentNullException.ThrowIfNull(r);
        if (ajustesDeCapital is { Count: > 0 })
        {
            var consolidado = anterior.Cuotas.Where(c => c.Vencimiento > r.Fecha).Sum(c => ajustesDeCapital.GetValueOrDefault(c.Numero));
            if (consolidado != 0m) r = r with { CambioDeCapital = r.CambioDeCapital + consolidado };
        }
        var e = anterior.Entrada;
        var red = e.Redondeo;
        var eaNueva = r.NuevaTasaEfectivaAnual ?? e.TasaEfectivaAnual;
        var rdNueva = r.NuevaTasaEfectivaAnual is { } ea
            ? EquivalenciaDeTasas.TasaDiaria(ea, e.Convencion, e.Equivalencia, e.Periodicidad)
            : anterior.TasaDiaria;
        var nominal = EquivalenciaDeTasas.NominalDelPeriodo(eaNueva, e.Periodicidad);
        var cuotas = anterior.Cuotas;
        var k = -1;
        for (var j = 0; j < cuotas.Count; j++)
            if (cuotas[j].Vencimiento > r.Fecha) { k = j; break; }

        var explicacion = new ExplicacionDeCartera().Agregar(anterior.Explicacion);
        if (k < 0)
        {
            if (r.CambioDeCapital != 0m)
                throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoSinCuotasPorRegenerar,
                    $"No quedan cuotas que venzan después del {ExplicacionDeCartera.Fecha(r.Fecha)}: el capital no se puede repartir.", "date", r.Fecha));
            explicacion.Linea($"Desde el {ExplicacionDeCartera.Fecha(r.Fecha)} la tasa es {ExplicacionDeCartera.Numero(eaNueva)} EA; no quedan cuotas por regenerar.");
            return anterior with { Entrada = e with { TasaEfectivaAnual = eaNueva }, TasaDiaria = rdNueva, TasaNominalDelPeriodo = nominal, Explicacion = explicacion };
        }
        if (anterior.PeriodosDeGracia.Count > 0 && r.Fecha < cuotas[0].Desde)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoRegeneracionEnGraciaTotal,
                "Un tramo, un abono o un reajuste dentro de la gracia total no se regenera en K1.", "date", r.Fecha));

        var x = cuotas[k];
        var restante = 0m;
        for (var j = k; j < cuotas.Count; j++) restante += cuotas[j].Capital;
        if (restante != x.SaldoInicial)
            throw new ArgumentException(
                $"La regeneración del {ExplicacionDeCartera.Fecha(r.Fecha)} es anterior a otra ya aplicada: los hechos se regeneran en el orden de su fecha valor.",
                nameof(r));
        var tramos = new List<TramoDeInteres>();
        var i0 = 0m;
        foreach (var t in x.TramosDeInteres(anterior.TasaDiaria))
        {
            if (t.Desde >= r.Fecha) continue;
            var hasta = t.Hasta < r.Fecha ? t.Hasta : r.Fecha;
            tramos.Add(t with { Hasta = hasta });
            if (hasta > t.Desde)
                i0 += t.Base * EquivalenciaDeTasas.TasaDelPeriodo(t.TasaDiaria, ContadorDeDias.Dias(e.Convencion, t.Desde, hasta), e.Equivalencia);
        }
        var despues = x.SaldoInicial + r.CambioDeCapital;
        if (despues < 0m)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoCapitalDescuadrado,
                "El abono supera el capital que falta por vencer.", "difference", despues));
        tramos.Add(new TramoDeInteres(r.Fecha, x.Vencimiento, despues, rdNueva));
        var i1 = i0 + despues * EquivalenciaDeTasas.TasaDelPeriodo(rdNueva, ContadorDeDias.Dias(e.Convencion, r.Fecha, x.Vencimiento), e.Equivalencia);

        var n = cuotas.Count;
        var tasas = cuotas.Select(c => EquivalenciaDeTasas.TasaDelPeriodo(rdNueva, c.Dias, e.Equivalencia)).ToArray();
        var montoTotal = cuotas.Take(k).Sum(c => c.Capital) + despues;
        var entrada = e with { Monto = montoTotal, TasaEfectivaAnual = eaNueva };

        if (r.Eleccion == PrepaymentChoice.ReduceTerm && despues < x.SaldoInicial)
            return ReducirPlazo(anterior, r, k, tramos, i1, despues, tasas, montoTotal, entrada, rdNueva, nominal, eaNueva, explicacion);

        var a = e.Sistema == AmortizationSystem.LevelPayment ? NiveladaDesde(despues, k, i1, cuotas, tasas) : 0m;
        var amortizan = 0;
        var extrasRestantes = 0m;
        for (var j = k; j < n; j++)
        {
            if (!cuotas[j].SoloInteres) amortizan++;
            extrasRestantes += cuotas[j].Extraordinaria;
        }
        var abono = e.Sistema == AmortizationSystem.ConstantPrincipal ? (despues - extrasRestantes) / amortizan : 0m;

        var nuevas = new List<CuotaDelPlan>(n);
        nuevas.AddRange(cuotas.Take(k));
        var saldo = despues;
        var residuo = 0m;
        for (var j = k; j < n; j++)
        {
            var y = cuotas[j];
            var esUltimo = j == n - 1;
            var saldoInicial = saldo;
            var interes = red.Concepto(j == k ? i1 : saldo * tasas[j]);
            var porcion = y.InteresDeGracia;
            var interesTotal = interes + porcion;
            IReadOnlyList<SeguroDeCuota> seguros = j == k
                ? y.Seguros
                : e.Seguros.Select(s => new SeguroDeCuota(s.Codigo, red.Concepto(SeguroExacto(entrada, s, saldoInicial, y.Dias, y.Numero == 1)), s.Computa)).ToList();
            var seguro = seguros.Sum(s => s.Valor);

            decimal capital;
            if (y.SoloInteres)
            {
                capital = 0m;
            }
            else
            {
                var teorico = e.Sistema switch
                {
                    AmortizationSystem.LevelPayment => red.PorCuota
                        ? red.Redondear(a + y.Extraordinaria + porcion + seguro) - interesTotal - seguro
                        : CuotaRedondeada(red, a) + y.Extraordinaria - interes,
                    AmortizationSystem.ConstantPrincipal => red.PorCuota
                        ? red.Redondear(abono + y.Extraordinaria + interesTotal + seguro) - interesTotal - seguro
                        : red.Concepto(abono) + y.Extraordinaria,
                    _ => saldo,
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
                        capital = 0m;
                        if (e.Sistema == AmortizationSystem.LevelPayment) a = NiveladaDesde(saldo, j + 1, null, cuotas, tasas);
                    }
                    if (capital > saldo) capital = saldo;
                }
            }
            saldo -= capital;
            nuevas.Add(new CuotaDelPlan(y.Numero, y.Desde, y.Vencimiento, y.Dias, tasas[j], j == k ? despues : saldoInicial, capital,
                interesTotal, porcion, seguros, saldo, y.SoloInteres)
            {
                Extraordinaria = y.Extraordinaria,
                Tramos = j == k ? tramos : null,
            });
        }

        var suma = nuevas.Sum(c => c.Capital);
        if (suma != montoTotal)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoCapitalDescuadrado,
                $"El capital del plan regenerado suma {ExplicacionDeCartera.Numero(suma)} y el saldo es {ExplicacionDeCartera.Numero(montoTotal)}.",
                "difference", suma - montoTotal));

        explicacion.Linea($"Regenerado el {ExplicacionDeCartera.Fecha(r.Fecha)} desde la cuota {x.Numero.ToString(Inv)}: cambio de capital "
            + $"{ExplicacionDeCartera.Numero(r.CambioDeCapital)}, tasa {ExplicacionDeCartera.Numero(eaNueva)} EA; la cuota {x.Numero.ToString(Inv)} "
            + $"causa en {tramos.Count.ToString(Inv)} tramos ({ExplicacionDeCartera.Numero(i1)} exacto); residuo en la última: {ExplicacionDeCartera.Numero(residuo)}.");
        return new PlanDePagos(anterior.RulesVersion, entrada, rdNueva, nominal,
            e.Sistema == AmortizationSystem.LevelPayment ? CuotaRedondeada(red, a) : null,
            anterior.InteresDeGracia, residuo, nuevas, explicacion)
        {
            PeriodosDeGracia = anterior.PeriodosDeGracia,
            PrimeraCuotaRegenerada = x.Numero,
        };
    }

    /// <summary>
    /// «Reducir el plazo» (US5, T0536; FR-075): desde la cuota <paramref name="k"/> (que causa en sus <paramref name="tramos"/>), en cuota fija
    /// con la nivelada sellada hasta acabar el saldo; si no, conservando el capital de cada cuota y quitando el abono desde la última.
    /// </summary>
    private static PlanDePagos ReducirPlazo(PlanDePagos anterior, EntradaDeRegeneracion r, int k, List<TramoDeInteres> tramos, decimal i1,
        decimal despues, IReadOnlyList<decimal> tasas, decimal montoTotal, EntradaDePlan entrada, decimal rdNueva, decimal nominal, decimal eaNueva,
        ExplicacionDeCartera explicacion)
    {
        var e = anterior.Entrada;
        var red = e.Redondeo;
        var cuotas = anterior.Cuotas;
        var n = cuotas.Count;
        var x = cuotas[k];
        var nivelada = e.Sistema == AmortizationSystem.LevelPayment ? anterior.CuotaNivelada : null;

        // Sin nivelada: el capital de cada cuota se conserva y el abono sale desde la última.
        var capitales = cuotas.Select(c => c.Capital).ToArray();
        var ultima = n - 1;
        if (nivelada is null)
        {
            var quitar = x.SaldoInicial - despues;
            for (var j = n - 1; j >= k && quitar > 0m; j--)
            {
                var t = Math.Min(capitales[j], quitar);
                capitales[j] -= t;
                quitar -= t;
            }
            while (ultima > k && capitales[ultima] == 0m) ultima--;
        }

        var nuevas = new List<CuotaDelPlan>(n);
        nuevas.AddRange(cuotas.Take(k));
        var saldo = despues;
        var residuo = 0m;
        for (var j = k; j <= ultima; j++)
        {
            var y = cuotas[j];
            var saldoInicial = saldo;
            var interes = red.Concepto(j == k ? i1 : saldo * tasas[j]);
            var porcion = y.InteresDeGracia;
            var interesTotal = interes + porcion;
            IReadOnlyList<SeguroDeCuota> seguros = j == k
                ? y.Seguros
                : e.Seguros.Select(s => new SeguroDeCuota(s.Codigo, red.Concepto(SeguroExacto(entrada, s, saldoInicial, y.Dias, y.Numero == 1)), s.Computa)).ToList();
            var seguro = seguros.Sum(s => s.Valor);

            decimal capital;
            var termina = false;
            if (y.SoloInteres)
            {
                capital = 0m;
            }
            else if (nivelada is { } a)
            {
                var teorico = red.PorCuota
                    ? red.Redondear(a + y.Extraordinaria + porcion + seguro) - interesTotal - seguro
                    : CuotaRedondeada(red, a) + y.Extraordinaria - interes;
                if (j == n - 1 || teorico >= saldo)
                {
                    capital = saldo;
                    residuo = capital - teorico;
                    termina = true;
                }
                else
                {
                    capital = teorico < 0m ? 0m : teorico;
                }
            }
            else
            {
                capital = Math.Min(capitales[j], saldo);
                if (j == ultima) capital = saldo;
            }
            saldo -= capital;
            nuevas.Add(new CuotaDelPlan(y.Numero, y.Desde, y.Vencimiento, y.Dias, tasas[j], j == k ? despues : saldoInicial, capital,
                interesTotal, porcion, seguros, saldo, y.SoloInteres)
            {
                Extraordinaria = y.Extraordinaria,
                Tramos = j == k ? tramos : null,
            });
            if (termina) break;
        }

        var suma = nuevas.Sum(c => c.Capital);
        if (suma != montoTotal)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoCapitalDescuadrado,
                $"El capital del plan regenerado suma {ExplicacionDeCartera.Numero(suma)} y el saldo es {ExplicacionDeCartera.Numero(montoTotal)}.",
                "difference", suma - montoTotal));

        var quitadas = n - nuevas.Count;
        explicacion.Linea($"Abono del {ExplicacionDeCartera.Fecha(r.Fecha)} con «reducir el plazo» desde la cuota {x.Numero.ToString(Inv)}: "
            + $"cambio de capital {ExplicacionDeCartera.Numero(r.CambioDeCapital)}; "
            + (nivelada is { } nv ? $"se conserva la cuota de {ExplicacionDeCartera.Numero(nv)}" : "se conserva el capital de cada cuota")
            + $" y salen {quitadas.ToString(Inv)} cuotas del final; residuo en la última: {ExplicacionDeCartera.Numero(residuo)}.");
        return new PlanDePagos(anterior.RulesVersion, entrada with { Periodos = Math.Max(1, e.Periodos - quitadas) }, rdNueva, nominal,
            nivelada ?? anterior.CuotaNivelada, anterior.InteresDeGracia, residuo, nuevas, explicacion)
        {
            PeriodosDeGracia = anterior.PeriodosDeGracia,
            PrimeraCuotaRegenerada = x.Numero,
        };
    }

    /// <summary>
    /// El plan de un crédito <b>migrado</b> (US4, T0486; D5, FR-140): el capital por cuota cargado se conserva —si no suma el saldo de
    /// capital, la última absorbe la diferencia y queda en la explicación; si la dejaría negativa, <see cref="CodigoCapitalDescuadrado"/>—
    /// y el interés de cada período lo pone el motor desde el corte (lo anterior viene como saldo inicial por concepto). Las cuotas que
    /// vencieron antes del corte no causan en el plan.
    /// </summary>
    public static PlanDePagos DesdeMigracion(EntradaDePlanMigrado m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.Cuotas.Count == 0) throw new ArgumentException("Un plan migrado tiene al menos una cuota.", nameof(m));
        var red = m.Redondeo;
        var rd = EquivalenciaDeTasas.TasaDiaria(m.TasaEfectivaAnual, m.Convencion, m.Equivalencia, m.Periodicidad);
        var filas = m.Cuotas.OrderBy(c => c.Numero).ToList();
        var capitales = filas.Select(c => c.Capital).ToArray();
        var residuo = m.SaldoDeCapital - capitales.Sum();
        capitales[^1] += residuo;
        if (capitales[^1] < 0m)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoCapitalDescuadrado,
                $"El capital de las cuotas cargadas supera el saldo de capital al corte en {ExplicacionDeCartera.Numero(-residuo)}.",
                "difference", -residuo));

        var cuotas = new List<CuotaDelPlan>(filas.Count);
        DateOnly? anterior = null;
        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            var desde = f.Desde ?? anterior ?? m.Corte;
            var saldoInicial = 0m;
            for (var j = i; j < capitales.Length; j++) saldoInicial += capitales[j];
            var inicio = desde > m.Corte ? desde : m.Corte;
            var dias = f.Vencimiento > desde ? ContadorDeDias.Dias(m.Convencion, desde, f.Vencimiento) : 0;
            var causa = f.Vencimiento > m.Corte;
            var interes = causa
                ? red.Concepto(saldoInicial * EquivalenciaDeTasas.TasaDelPeriodo(rd, ContadorDeDias.Dias(m.Convencion, inicio, f.Vencimiento), m.Equivalencia))
                : 0m;
            cuotas.Add(new CuotaDelPlan(f.Numero, desde, f.Vencimiento, dias, EquivalenciaDeTasas.TasaDelPeriodo(rd, dias, m.Equivalencia), saldoInicial,
                capitales[i], interes, 0m, [], saldoInicial - capitales[i], false)
            {
                Tramos = causa ? [new TramoDeInteres(inicio, f.Vencimiento, saldoInicial, rd)] : [],
            });
            anterior = f.Vencimiento;
        }

        var explicacion = new ExplicacionDeCartera()
            .Linea($"Plan migrado al corte del {ExplicacionDeCartera.Fecha(m.Corte)} (D5): se conserva el capital de cada cuota cargada; el interés lo "
                + "causa el motor desde el corte.")
            .Linea($"La última cuota absorbe la diferencia entre el saldo de capital y las cuotas cargadas: {ExplicacionDeCartera.Numero(residuo)}.");
        var entrada = new EntradaDePlan(m.SaldoDeCapital, m.Corte, filas.Count, m.Periodicidad, m.Sistema, m.TasaEfectivaAnual, m.Convencion,
            m.Equivalencia, red);
        return new PlanDePagos(VersionesDeReglas.Vigente, entrada, rd, EquivalenciaDeTasas.NominalDelPeriodo(m.TasaEfectivaAnual, m.Periodicidad),
            null, 0m, residuo, cuotas, explicacion);
    }

    /// <summary>
    /// La causación que proyecta el plan suponiendo pagos a tiempo (US4, T0486; T25): por cuota, el interés corriente causado hasta
    /// <paramref name="fecha"/> —el redondeo de la suma exacta de sus tramos hasta esa fecha; a su vencimiento es exactamente el interés
    /// de la cuota— y el seguro de las cuotas ya vencidas. El interés de una gracia total va a la primera cuota con su propio redondeo,
    /// como la cobra el plan. Es lo que esperan las <c>consultas</c> «pagando a tiempo» de los casos dorados.
    /// </summary>
    public static IReadOnlyList<CausadoDeCuota> ProyectarCausacion(PlanDePagos plan, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var e = plan.Entrada;
        var red = e.Redondeo;
        var salida = new List<CausadoDeCuota>();
        foreach (var c in plan.Cuotas)
        {
            var exacto = 0m;
            foreach (var t in c.TramosDeInteres(plan.TasaDiaria))
            {
                var hasta = t.Hasta < fecha ? t.Hasta : fecha;
                if (hasta > t.Desde)
                    exacto += t.Base * EquivalenciaDeTasas.TasaDelPeriodo(t.TasaDiaria, ContadorDeDias.Dias(e.Convencion, t.Desde, hasta), e.Equivalencia);
            }
            var valor = red.Concepto(exacto);
            if (c.Numero == 1 && plan.PeriodosDeGracia.Count > 0)
            {
                var gracia = 0m;
                foreach (var p in plan.PeriodosDeGracia)
                {
                    var hasta = p.Hasta < fecha ? p.Hasta : fecha;
                    if (p.Desde < fecha)
                        gracia += e.Monto * EquivalenciaDeTasas.TasaDelPeriodo(plan.TasaDiaria, ContadorDeDias.Dias(e.Convencion, p.Desde, hasta), e.Equivalencia);
                }
                valor += red.Concepto(gracia);
            }
            if (valor != 0m) salida.Add(new CausadoDeCuota(c.Numero, LoanConcept.CurrentInterest, valor));
            if (c.Seguro != 0m && fecha >= c.Vencimiento) salida.Add(new CausadoDeCuota(c.Numero, LoanConcept.Insurance, c.Seguro));
        }
        return salida;
    }

    /// <summary>
    /// La nivelada de una regeneración: el saldo tras cada cuota es <c>α + β·A</c> (las de sólo interés no lo mueven; la primera, con su
    /// interés <paramref name="interesDeLaPrimera"/> ya fijo si se da) y A es la que deja el último en cero.
    /// </summary>
    private static decimal NiveladaDesde(decimal saldo, int desde, decimal? interesDeLaPrimera, IReadOnlyList<CuotaDelPlan> cuotas, IReadOnlyList<decimal> tasas)
    {
        var alfa = saldo;
        var beta = 0m;
        for (var j = desde; j < cuotas.Count; j++)
        {
            if (cuotas[j].SoloInteres) continue;
            if (j == desde && interesDeLaPrimera is { } i1)
            {
                alfa += i1;
            }
            else
            {
                alfa *= 1m + tasas[j];
                beta *= 1m + tasas[j];
            }
            alfa -= cuotas[j].Extraordinaria;
            beta -= 1m;
        }
        return -alfa / beta;
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
