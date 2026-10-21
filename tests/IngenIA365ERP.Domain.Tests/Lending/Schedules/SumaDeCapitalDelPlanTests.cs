using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Schedules;

/// <summary>
/// Feature 013, T0332 (propiedad, SC-017; T29, R37): para montos, plazos, sistemas, convenciones, equivalencias, periodicidades,
/// redondeos, extraordinarias, gracias y seguros al azar —con semilla fija, para que una falla se reproduzca— la suma del capital del
/// plan es el monto al centavo, la última cuota absorbe el residuo y deja el saldo en cero, ningún capital es negativo y
/// <c>Lending.Schedule.PrincipalMismatch</c> nunca sale del generador. El control de usura corre sobre cada plan sin fallar.
/// </summary>
public class SumaDeCapitalDelPlanTests
{
    private const int Semilla = 20261010;
    private const int Planes = 600;

    private static readonly RepaymentPeriodicity[] Periodicidades =
    [
        RepaymentPeriodicity.Weekly, RepaymentPeriodicity.TenDay, RepaymentPeriodicity.Biweekly, RepaymentPeriodicity.Monthly,
        RepaymentPeriodicity.Bimonthly, RepaymentPeriodicity.Quarterly, RepaymentPeriodicity.SemiAnnual, RepaymentPeriodicity.Annual,
    ];

    [Fact]
    public void La_suma_del_capital_es_el_monto_al_centavo_en_planes_al_azar()
    {
        var azar = new Random(Semilla);
        var casos = 0;
        using var _ = new AssertionScope();
        for (var i = 0; i < Planes; i++)
        {
            var entrada = EntradaAlAzar(azar);
            PlanDePagos plan;
            try
            {
                plan = GeneradorDePlan.Generar(entrada);
            }
            catch (ReglaDeCarteraException ex) when (ex.Codigo == GeneradorDePlan.CodigoCapitalDescuadrado)
            {
                throw new Xunit.Sdk.XunitException($"Plan {i}: el generador descuadró el capital ({ex.Message}) con {entrada}.");
            }

            var donde = $"plan {i} ({entrada.Sistema}, {entrada.Periodicidad}, {entrada.Periodos} períodos, {entrada.Convencion}, "
                + $"{entrada.Equivalencia}, {entrada.Redondeo.Unidad} {entrada.Redondeo.Modo} {entrada.Redondeo.Momento}, gracia {entrada.Gracia})";
            plan.TotalCapital.Should().Be(entrada.Monto, donde);
            plan.Cuotas[^1].SaldoFinal.Should().Be(0m, donde);
            plan.Cuotas.Should().OnlyContain(c => c.Capital >= 0m && c.Interes >= 0m && c.SaldoFinal >= 0m, donde);
            plan.Cuotas.Select(c => c.Vencimiento).Should().BeInAscendingOrder(donde);
            plan.Cuotas.Should().HaveCount(entrada.Gracia == GraceKind.Total ? entrada.Periodos - entrada.PeriodosDeGracia : entrada.Periodos, donde);
            plan.Cuotas.Sum(c => c.InteresDeGracia).Should().Be(plan.InteresDeGracia, $"{donde}: la gracia total se cobra entera, ni más ni menos");
            plan.Explicacion.Lineas.Should().Contain(l => l.Contains("absorbe el residuo"), donde);

            if (i % 10 == 0)
            {
                var usura = ControlDeUsura.Evaluar(plan, new UsuraCertificada(0.40m, Guid.Empty, "CONSUMO", entrada.FechaDesembolso));
                usura.Veredicto.Should().NotBe(VeredictoDeUsura.SinUsura, donde);
            }
            casos++;
        }
        casos.Should().Be(Planes);
    }

    /// <summary>
    /// US4 (T0468; D12, FR-067, SC-017): planes <b>regenerados</b> al azar —tramos, reajustes de tasa y abonos, uno o dos seguidos, desde
    /// la primera cuota que vence después del hecho—: Σ capital = el saldo anterior más el cambio al centavo, la última absorbe el residuo,
    /// la numeración y las fechas se conservan, las cuotas ya vencidas no cambian y nada queda negativo. La gracia total no se regenera
    /// en K1 (<see cref="GeneradorDePlan.CodigoRegeneracionEnGraciaTotal"/>).
    /// </summary>
    [Fact]
    public void Los_planes_regenerados_suman_el_saldo_y_conservan_la_numeracion()
    {
        var azar = new Random(Semilla + 4);
        var regenerados = 0;
        using var _ = new AssertionScope();
        for (var i = 0; i < Planes; i++)
        {
            var entrada = EntradaAlAzar(azar);
            if (entrada.Gracia == GraceKind.Total) continue;
            var plan = GeneradorDePlan.Generar(entrada);
            var hechos = azar.Next(1, 3);
            var desde = entrada.FechaDesembolso;
            for (var h = 0; h < hechos; h++)
            {
                // Los hechos se regeneran en el orden de su fecha valor, como los aplica la reconstrucción.
                var ultimo = plan.Cuotas[^1].Vencimiento;
                var fecha = desde.AddDays(azar.Next(0, Math.Max(1, ultimo.DayNumber - desde.DayNumber)));
                desde = fecha;
                var k = plan.Cuotas.ToList().FindIndex(c => c.Vencimiento > fecha);
                if (k < 0) break;
                var clase = azar.Next(0, 3);
                var regeneracion = clase switch
                {
                    0 => new EntradaDeRegeneracion(fecha) { CambioDeCapital = azar.Next(10_000, 20_000_000) + azar.Next(0, 100) / 100m },
                    1 => new EntradaDeRegeneracion(fecha) { NuevaTasaEfectivaAnual = azar.Next(0, 400_000) / 1_000_000m },
                    _ => new EntradaDeRegeneracion(fecha) { CambioDeCapital = -decimal.Round(plan.Cuotas[k].SaldoInicial * azar.Next(1, 50) / 100m, 2) },
                };
                var donde = $"plan {i}, hecho {h} ({clase}) el {fecha:yyyy-MM-dd} ({entrada.Sistema}, {entrada.Periodicidad}, {entrada.Convencion}, "
                    + $"{entrada.Redondeo.Unidad} {entrada.Redondeo.Modo} {entrada.Redondeo.Momento}, gracia {entrada.Gracia})";
                PlanDePagos nuevo;
                try
                {
                    nuevo = GeneradorDePlan.Regenerar(plan, regeneracion);
                }
                catch (ReglaDeCarteraException ex) when (ex.Codigo == GeneradorDePlan.CodigoCapitalDescuadrado)
                {
                    throw new Xunit.Sdk.XunitException($"{donde}: la regeneración descuadró el capital ({ex.Message}).");
                }

                nuevo.TotalCapital.Should().Be(plan.TotalCapital + regeneracion.CambioDeCapital, donde);
                nuevo.Cuotas[^1].SaldoFinal.Should().Be(0m, donde);
                nuevo.Cuotas.Select(c => (c.Numero, c.Vencimiento)).Should().Equal(plan.Cuotas.Select(c => (c.Numero, c.Vencimiento)), $"{donde}: numeración y fechas");
                nuevo.Cuotas.Take(k).Should().Equal(plan.Cuotas.Take(k), $"{donde}: las cuotas que ya vencieron no cambian");
                nuevo.Cuotas.Should().OnlyContain(c => c.Capital >= 0m && c.Interes >= 0m && c.SaldoFinal >= 0m, donde);
                nuevo.Cuotas.Where(c => c.SoloInteres).Should().OnlyContain(c => c.Capital == 0m, $"{donde}: la gracia a capital no amortiza");
                nuevo.PrimeraCuotaRegenerada.Should().Be(plan.Cuotas[k].Numero, donde);
                nuevo.Cuotas[k].Tramos.Should().NotBeNullOrEmpty($"{donde}: la primera regenerada causa en tramos");
                // Lo que proyecta la causación con pagos a tiempo a su vencimiento es exactamente su interés (T25).
                MotorDeCartera.ProyectarCausacion(nuevo, nuevo.Cuotas[k].Vencimiento)
                    .Where(c => c.Cuota == nuevo.Cuotas[k].Numero && c.Concepto == LoanConcept.CurrentInterest).Sum(c => c.Valor)
                    .Should().Be(nuevo.Cuotas[k].Interes, $"{donde}: el interés de la cuota regenerada es el de sus tramos");
                nuevo.Explicacion.Lineas.Should().Contain(l => l.StartsWith("Regenerado el", StringComparison.Ordinal), donde);
                plan = nuevo;
                regenerados++;
            }
        }
        regenerados.Should().BeGreaterThan(Planes / 2);
    }

    /// <summary>
    /// US5 (T0536; FR-075, SC-017): un abono extraordinario con la elección del deudor regenera el plan con Σ capital = saldo al centavo y la
    /// numeración y las fechas conservadas. «Reducir la cuota» conserva todas las cuotas; «reducir el plazo» quita cuotas del final y, en
    /// cuota fija, conserva la cuota nivelada sellada (capital + interés = la nivelada más la extraordinaria) en todas menos la última, que
    /// absorbe el residuo.
    /// </summary>
    [Fact]
    public void Un_abono_con_eleccion_suma_el_saldo_y_reducir_el_plazo_conserva_la_cuota()
    {
        var azar = new Random(Semilla + 5);
        var abonos = 0;
        var cuotasQuitadas = 0;
        using var _ = new AssertionScope();
        for (var i = 0; i < Planes; i++)
        {
            var entrada = EntradaAlAzar(azar);
            if (entrada.Gracia == GraceKind.Total) continue;
            var plan = GeneradorDePlan.Generar(entrada);
            var ultimo = plan.Cuotas[^1].Vencimiento;
            var fecha = entrada.FechaDesembolso.AddDays(azar.Next(0, Math.Max(1, ultimo.DayNumber - entrada.FechaDesembolso.DayNumber)));
            var k = plan.Cuotas.ToList().FindIndex(c => c.Vencimiento > fecha);
            if (k < 0) continue;
            var abono = decimal.Round(plan.Cuotas[k].SaldoInicial * azar.Next(1, 95) / 100m, 2);
            if (abono <= 0m) continue;
            foreach (var eleccion in new[] { PrepaymentChoice.ReduceTerm, PrepaymentChoice.ReduceInstallment })
            {
                var donde = $"plan {i}, abono de {abono} el {fecha:yyyy-MM-dd} con {eleccion} ({entrada.Sistema}, {entrada.Periodicidad}, "
                    + $"{entrada.Redondeo.Unidad} {entrada.Redondeo.Modo} {entrada.Redondeo.Momento}, gracia {entrada.Gracia})";
                PlanDePagos nuevo;
                try
                {
                    nuevo = GeneradorDePlan.Regenerar(plan, new EntradaDeRegeneracion(fecha) { CambioDeCapital = -abono, Eleccion = eleccion });
                }
                catch (ReglaDeCarteraException ex) when (ex.Codigo == GeneradorDePlan.CodigoCapitalDescuadrado)
                {
                    throw new Xunit.Sdk.XunitException($"{donde}: la regeneración descuadró el capital ({ex.Message}).");
                }

                nuevo.TotalCapital.Should().Be(plan.TotalCapital - abono, donde);
                nuevo.Cuotas[^1].SaldoFinal.Should().Be(0m, donde);
                nuevo.Cuotas.Take(k).Should().Equal(plan.Cuotas.Take(k), $"{donde}: las cuotas que ya vencieron no cambian");
                nuevo.Cuotas.Should().OnlyContain(c => c.Capital >= 0m && c.Interes >= 0m && c.SaldoFinal >= 0m, donde);
                nuevo.Cuotas.Select(c => (c.Numero, c.Vencimiento)).Should().Equal(
                    plan.Cuotas.Take(nuevo.Cuotas.Count).Select(c => (c.Numero, c.Vencimiento)), $"{donde}: numeración y fechas");
                nuevo.Cuotas.Count.Should().BeGreaterThan(k, $"{donde}: la primera regenerada queda");
                MotorDeCartera.ProyectarCausacion(nuevo, nuevo.Cuotas[k].Vencimiento)
                    .Where(c => c.Cuota == nuevo.Cuotas[k].Numero && c.Concepto == LoanConcept.CurrentInterest).Sum(c => c.Valor)
                    .Should().Be(nuevo.Cuotas[k].Interes, $"{donde}: la cuota del abono causa en sus tramos");
                if (eleccion == PrepaymentChoice.ReduceInstallment)
                {
                    nuevo.Cuotas.Should().HaveCount(plan.Cuotas.Count, $"{donde}: reducir la cuota conserva las cuotas");
                    continue;
                }
                nuevo.Cuotas.Count.Should().BeLessThanOrEqualTo(plan.Cuotas.Count, donde);
                cuotasQuitadas += plan.Cuotas.Count - nuevo.Cuotas.Count;
                if (entrada.Sistema == AmortizationSystem.LevelPayment && entrada.Redondeo.Momento != RoundingMoment.PerInstallment)
                {
                    nuevo.CuotaNivelada.Should().Be(plan.CuotaNivelada, $"{donde}: reducir el plazo conserva la cuota");
                    foreach (var c in nuevo.Cuotas.Skip(k).SkipLast(1).Where(c => !c.SoloInteres && c.Capital > 0m))
                        (c.Capital + c.Interes).Should().Be(plan.CuotaNivelada!.Value + c.Extraordinaria, $"{donde}: cuota {c.Numero}");
                }
            }
            abonos++;
        }
        abonos.Should().BeGreaterThan(Planes / 3);
        cuotasQuitadas.Should().BeGreaterThan(Planes / 3, "reducir el plazo quita cuotas");
    }

    [Fact]
    public void Un_hecho_anterior_a_otra_regeneracion_o_un_tramo_sin_cuotas_por_vencer_se_rechazan()
    {
        var plan = GeneradorDePlan.Generar(new EntradaDePlan(1_200_000m, new DateOnly(2027, 1, 31), 6, RepaymentPeriodicity.Monthly,
            AmortizationSystem.LevelPayment, 0.24m, DayCountConvention.Thirty360, RateEquivalence.Compound, RedondeoDeCartera.AlCentavo));
        var conTramo = GeneradorDePlan.Regenerar(plan, new EntradaDeRegeneracion(new DateOnly(2027, 4, 10)) { CambioDeCapital = 500_000m });
        var antes = () => GeneradorDePlan.Regenerar(conTramo, new EntradaDeRegeneracion(new DateOnly(2027, 2, 10)) { CambioDeCapital = 1_000m });
        antes.Should().Throw<ArgumentException>("los hechos se regeneran en el orden de su fecha valor");
        var tarde = () => GeneradorDePlan.Regenerar(plan, new EntradaDeRegeneracion(new DateOnly(2027, 8, 1)) { CambioDeCapital = 1_000m });
        tarde.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(GeneradorDePlan.CodigoSinCuotasPorRegenerar);
        GeneradorDePlan.Regenerar(plan, new EntradaDeRegeneracion(new DateOnly(2027, 8, 1)) { NuevaTasaEfectivaAnual = 0.2m }).Cuotas
            .Should().Equal(plan.Cuotas, "un reajuste sin cuotas por vencer sólo cambia la tasa");
    }

    [Fact]
    public void Un_tramo_dentro_de_la_gracia_total_no_se_regenera_en_K1()
    {
        var entrada = new EntradaDePlan(4_000_000m, new DateOnly(2027, 3, 1), 10, RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment,
            0.19m, DayCountConvention.Thirty360, RateEquivalence.Compound, RedondeoDeCartera.AlCentavo)
        { Gracia = GraceKind.Total, PeriodosDeGracia = 2 };
        var plan = GeneradorDePlan.Generar(entrada);
        plan.PeriodosDeGracia.Should().HaveCount(2);
        var regenerar = () => GeneradorDePlan.Regenerar(plan, new EntradaDeRegeneracion(new DateOnly(2027, 3, 20)) { CambioDeCapital = 1_000m });
        regenerar.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(GeneradorDePlan.CodigoRegeneracionEnGraciaTotal);
    }

    /// <summary>
    /// US4 (T0468; D5, FR-140): el plan <b>migrado</b> conserva el número y el capital de cada cuota cargada; si no suman el saldo de
    /// capital al corte, la última absorbe la diferencia (visible en la explicación) y Σ capital = saldo al centavo.
    /// </summary>
    [Fact]
    public void El_plan_migrado_conserva_el_capital_por_cuota_y_suma_el_saldo()
    {
        var azar = new Random(Semilla + 5);
        using var _ = new AssertionScope();
        for (var i = 0; i < 300; i++)
        {
            var corte = new DateOnly(2027, 1, 1).AddDays(azar.Next(0, 700));
            var cuantas = azar.Next(1, 30);
            var primera = azar.Next(1, 5);
            var vencimientos = FechasDeVencimiento.Generar(corte.AddMonths(-azar.Next(0, 4)), RepaymentPeriodicity.Monthly, cuantas);
            var cuotas = vencimientos.Select((v, j) => new CuotaMigrada(primera + j, v, azar.Next(1, 3_000_000) + azar.Next(0, 100) / 100m)).ToList();
            var cargado = cuotas.Sum(c => c.Capital);
            var residuo = azar.Next(-100, 101) / 100m;
            if (cuotas[^1].Capital + residuo < 0m) residuo = 0m;
            var plan = GeneradorDePlan.DesdeMigracion(new EntradaDePlanMigrado(corte, cargado + residuo, cuotas, RepaymentPeriodicity.Monthly,
                AmortizationSystem.LevelPayment, azar.Next(0, 400_000) / 1_000_000m, (DayCountConvention)azar.Next(1, 4), RateEquivalence.Compound,
                RedondeoDeCartera.AlCentavo));

            var donde = $"plan migrado {i}";
            plan.TotalCapital.Should().Be(cargado + residuo, donde);
            plan.Cuotas.Select(c => c.Numero).Should().Equal(cuotas.Select(c => c.Numero), $"{donde}: la numeración se conserva");
            plan.Cuotas.Take(cuotas.Count - 1).Select(c => c.Capital).Should().Equal(cuotas.Take(cuotas.Count - 1).Select(c => c.Capital), $"{donde}: capital por cuota");
            plan.Cuotas[^1].Capital.Should().Be(cuotas[^1].Capital + residuo, $"{donde}: la última absorbe");
            plan.ResiduoDeLaUltimaCuota.Should().Be(residuo, donde);
            plan.Cuotas.Where(c => c.Vencimiento <= corte).Should().OnlyContain(c => c.Interes == 0m, $"{donde}: lo vencido al corte no causa en el plan");
        }
        // Las cuotas cargadas superan el saldo en 15 y la última sólo tiene 5: quedaría negativa.
        var descuadre = () => GeneradorDePlan.DesdeMigracion(new EntradaDePlanMigrado(new DateOnly(2027, 6, 30), 80m,
            [new CuotaMigrada(1, new DateOnly(2027, 7, 10), 90m), new CuotaMigrada(2, new DateOnly(2027, 8, 10), 5m)], RepaymentPeriodicity.Monthly,
            AmortizationSystem.LevelPayment, 0.2m, DayCountConvention.Thirty360, RateEquivalence.Compound, RedondeoDeCartera.AlCentavo));
        descuadre.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(GeneradorDePlan.CodigoCapitalDescuadrado);
    }

    private static EntradaDePlan EntradaAlAzar(Random azar)
    {
        var monto = azar.Next(100_000, 50_000_000) + azar.Next(0, 100) / 100m;
        var ea = azar.Next(0, 400_000) / 1_000_000m;
        var desembolso = new DateOnly(2026, 1, 1).AddDays(azar.Next(0, 1_500));
        var convencion = (DayCountConvention)azar.Next(1, 4);
        var equivalencia = azar.Next(0, 4) == 0 ? RateEquivalence.Linear : RateEquivalence.Compound;
        var redondeo = new RedondeoDeCartera(
            RedondeoDeCartera.UnidadesAdmitidas[azar.Next(RedondeoDeCartera.UnidadesAdmitidas.Count)],
            (RoundingMode)azar.Next(1, 5), (RoundingMoment)azar.Next(1, 4));

        if (azar.Next(0, 12) == 0)
        {
            return new EntradaDePlan(monto, desembolso, 1, RepaymentPeriodicity.SinglePayment, AmortizationSystem.Bullet, ea, convencion,
                equivalencia, redondeo) { PrimerVencimiento = desembolso.AddDays(azar.Next(30, 1_100)) };
        }

        var periodicidad = Periodicidades[azar.Next(Periodicidades.Length)];
        var periodos = azar.Next(1, 61);
        var sistema = azar.Next(0, 3) == 0 ? AmortizationSystem.ConstantPrincipal : AmortizationSystem.LevelPayment;

        var gracia = GraceKind.None;
        var periodosDeGracia = 0;
        GraceInterestTreatment? tratamiento = null;
        int? distribucion = null;
        if (periodos > 2 && azar.Next(0, 4) == 0)
        {
            gracia = azar.Next(0, 2) == 0 ? GraceKind.Principal : GraceKind.Total;
            periodosDeGracia = azar.Next(1, Math.Min(6, periodos - 1) + 1);
            if (gracia == GraceKind.Total)
            {
                tratamiento = azar.Next(0, 2) == 0 ? GraceInterestTreatment.FirstInstallment : GraceInterestTreatment.Distributed;
                if (tratamiento == GraceInterestTreatment.Distributed) distribucion = azar.Next(1, periodos - periodosDeGracia + 1);
            }
        }

        var pagos = gracia == GraceKind.Total ? periodos - periodosDeGracia : periodos;
        var primeraQueAmortiza = gracia == GraceKind.Principal ? periodosDeGracia + 1 : 1;
        var extraordinarias = new List<CuotaExtraordinaria>();
        if (pagos >= primeraQueAmortiza + 1 && azar.Next(0, 3) == 0)
        {
            var cuantas = azar.Next(1, 3);
            for (var k = 0; k < cuantas; k++)
            {
                var cuota = azar.Next(primeraQueAmortiza, pagos + 1);
                extraordinarias.Add(new CuotaExtraordinaria(cuota, decimal.Round(monto * azar.Next(1, 15) / 100m, 2)));
            }
        }

        var seguros = new List<SeguroDelPlan>();
        if (azar.Next(0, 3) == 0)
        {
            seguros.Add(new SeguroDelPlan("VIDA", azar.Next(0, 2) == 0 ? InsuranceBase.OutstandingBalance : InsuranceBase.InitialAmount,
                azar.Next(1, 2_000) / 1_000_000m, azar.Next(0, 2) == 0 ? periodicidad : RepaymentPeriodicity.Monthly, azar.Next(0, 2) == 0));
        }

        return new EntradaDePlan(monto, desembolso, periodos, periodicidad, sistema, ea, convencion, equivalencia, redondeo)
        {
            DiaDePago = azar.Next(0, 3) == 0 ? null : azar.Next(1, 32),
            Gracia = gracia,
            PeriodosDeGracia = periodosDeGracia,
            TratamientoDeGracia = tratamiento,
            CuotasDeDistribucion = distribucion,
            Extraordinarias = extraordinarias,
            Seguros = seguros,
        };
    }
}
