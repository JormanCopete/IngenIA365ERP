using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Accrual;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// Feature 013, K3 (T0874; FR-091, T29, T30): la versión completa del plan tras una novedad (<see cref="GeneradorDePlan.UnirConAnterior"/>) y
/// la operación <c>TermsChange</c> en la reconstrucción (<see cref="InterpreteDeNovedad"/>): el plan se regenera desde la fecha valor sobre todo
/// el capital pendiente, las cuotas vencidas se conservan con su número y su capital se consolida, y el conteo de reestructuraciones rige
/// desde el día siguiente. Las cifras del plan nuevo las fijan los casos dorados <c>k3-*</c>; aquí se miran las propiedades.
/// </summary>
public class NovedadEnLaReconstruccionTests
{
    private static readonly DateOnly Desembolso = new(2027, 1, 15);

    private static EntradaDeReconstruccion ConNovedad(DateOnly fecha, ContenidoDeNovedad contenido, int reestructuraciones = 0)
    {
        var entrada = CreditosDePrueba.Simple(reestructuraciones: reestructuraciones);
        return entrada with
        {
            Operaciones = [.. entrada.Operaciones, new EntradaDeOperacion("N1", LoanOperationKind.TermsChange, fecha, CreditosDePrueba.Instante(fecha), 2)
            {
                Contenido = contenido,
            }],
        };
    }

    [Fact]
    public void La_version_completa_conserva_las_vencidas_y_sigue_la_numeracion()
    {
        var original = GeneradorDePlan.Generar(CreditosDePrueba.Simple().Plan!);
        var fecha = new DateOnly(2027, 4, 15);
        var saldo = original.Cuotas.Single(c => c.Numero == 3).SaldoFinal;
        var novedad = GeneradorDePlan.RegenerarPorNovedad(new EntradaDeNovedadDePlan(NoveltyMechanism.Extension, fecha, original.Entrada, saldo, 15)
        {
            PrimeraCuota = 4,
        });

        var completo = GeneradorDePlan.UnirConAnterior(original, novedad);

        completo.Cuotas.Select(c => c.Numero).Should().Equal(Enumerable.Range(1, 18));
        completo.Cuotas.Take(3).Should().Equal(original.Cuotas.Take(3), "las vencidas quedan como estaban");
        completo.Cuotas.Skip(3).Should().Equal(novedad.Cuotas);
        completo.Cuotas[3].Vencimiento.Should().Be(new DateOnly(2027, 5, 15), "el día de pago del crédito no se corre");
        completo.Entrada.Monto.Should().Be(completo.TotalCapital, "Σ capital de la versión = su base (SC-017)");
        completo.PrimeraCuotaRegenerada.Should().Be(4);

        var mal = () => GeneradorDePlan.UnirConAnterior(original, novedad with { PrimeraCuotaRegenerada = 5 });
        mal.Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be(GeneradorDePlan.CodigoNovedadInvalida);
    }

    [Theory]
    [InlineData(NoveltyMechanism.RateChange, "rate")]
    [InlineData(NoveltyMechanism.PaymentDateChange, "paymentDay")]
    public void Las_condiciones_que_faltan_se_rechazan(NoveltyMechanism mecanismo, string campo)
    {
        var base_ = CreditosDePrueba.Simple().Plan!;
        var accion = () => GeneradorDePlan.RegenerarPorNovedad(new EntradaDeNovedadDePlan(mecanismo, new DateOnly(2027, 3, 1), base_, 1_000_000m, 6));
        accion.Should().Throw<ReglaDeCarteraException>().Which.Problema.Datos.Should().ContainKey(campo);
    }

    [Fact]
    public void Un_enlazado_empieza_en_la_cuota_uno_y_solo_la_refinanciacion_recoge()
    {
        var base_ = CreditosDePrueba.Simple().Plan!;
        var novacion = () => GeneradorDePlan.RegenerarPorNovedad(new EntradaDeNovedadDePlan(NoveltyMechanism.Novation, new DateOnly(2027, 3, 1), base_, 1_000_000m, 6)
        {
            PrimeraCuota = 3,
        });
        novacion.Should().Throw<ReglaDeCarteraException>();
        var prorrogaQueRecoge = () => GeneradorDePlan.RegenerarPorNovedad(new EntradaDeNovedadDePlan(NoveltyMechanism.Extension, new DateOnly(2027, 3, 1), base_, 1_000_000m, 6)
        {
            SaldosRecogidos = [200_000m],
        });
        prorrogaQueRecoge.Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void La_prorroga_antes_del_primer_vencimiento_regenera_todo_el_plan_sin_mover_el_capital()
    {
        var fecha = new DateOnly(2027, 2, 1);
        var entrada = ConNovedad(fecha, new ContenidoDeNovedad(NoveltyMechanism.Extension, 18) { NoveltyId = 1, ReestructuracionesDespues = 1 });

        var estado = MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 2, 10));

        estado.Plan!.Cuotas.Select(c => c.Numero).Should().Equal(Enumerable.Range(1, 18));
        estado.Plan.Cuotas[0].Vencimiento.Should().Be(new DateOnly(2027, 3, 15), "un período después de la decisión, el día de pago del crédito");
        estado.Saldo(LoanConcept.Principal).Should().Be(3_000_000m);
        estado.Plan.TotalCapital.Should().Be(3_000_000m);
        estado.Suspendida.Should().BeFalse("una sola reestructuración no suspende");
    }

    [Fact]
    public void Las_cuotas_vencidas_se_conservan_y_su_capital_pasa_al_plan_nuevo()
    {
        // Sin pagos: la 1 (15-02) y la 2 (15-03) vencen impagas; la novedad del 20-03 consolida su capital en el plan nuevo, que sigue en la 3.
        var fecha = new DateOnly(2027, 3, 20);
        var entrada = ConNovedad(fecha, new ContenidoDeNovedad(NoveltyMechanism.Extension, 20) { ReestructuracionesDespues = 1 });

        var antes = MotorDeCartera.Reconstruir(entrada, fecha.AddDays(-1));
        var despues = MotorDeCartera.Reconstruir(entrada, fecha.AddDays(3));

        antes.CapitalVencido.Should().BeGreaterThan(0m);
        despues.Plan!.Cuotas.Select(c => c.Numero).Should().Equal(Enumerable.Range(1, 22));
        despues.Plan.PrimeraCuotaRegenerada.Should().Be(3);
        despues.Saldo(LoanConcept.Principal).Should().Be(3_000_000m, "el capital no se mueve: se reparte en el plan nuevo");
        despues.Saldo(LoanConcept.Principal, 1).Should().Be(0m);
        despues.Saldo(LoanConcept.Principal, 2).Should().Be(0m);
        despues.CapitalVencido.Should().Be(0m, "el capital vencido quedó en el plan nuevo");
        despues.Saldo(LoanConcept.CurrentInterest, 1).Should().Be(antes.Saldo(LoanConcept.CurrentInterest, 1), "su interés se sigue debiendo");
        despues.Plan.Cuotas.Skip(2).Sum(c => c.Capital).Should().Be(3_000_000m);
    }

    [Fact]
    public void La_segunda_reestructuracion_suspende_desde_el_dia_siguiente()
    {
        var fecha = new DateOnly(2027, 2, 1);
        var entrada = ConNovedad(fecha, new ContenidoDeNovedad(NoveltyMechanism.Extension, 18) { ReestructuracionesDespues = 2 }, reestructuraciones: 1);

        MotorDeCartera.Reconstruir(entrada, fecha.AddDays(-1)).Suspendida.Should().BeFalse("antes de la decisión rige una");
        var despues = MotorDeCartera.Reconstruir(entrada, fecha.AddDays(1));
        despues.MotivoDeSuspension.Should().Be(MotivoDeSuspension.Reestructuraciones);
        despues.Transiciones.Should().ContainSingle(t => t.Fecha == fecha.AddDays(1), "lo causado desde el día siguiente va a suspenso");
        despues.Saldo(LoanConcept.CurrentInterestInSuspense).Should().BeGreaterThan(0m);
        despues.Saldo(LoanConcept.CurrentInterest).Should().Be(MotorDeCartera.Reconstruir(entrada, fecha).Saldo(LoanConcept.CurrentInterest),
            "lo causado hasta la decisión no se reclasifica (T30)");
    }

    [Fact]
    public void El_cambio_de_tasa_rige_desde_el_dia_siguiente()
    {
        var fecha = new DateOnly(2027, 2, 15);
        var sinNovedad = CreditosDePrueba.Simple();
        var entrada = ConNovedad(fecha, new ContenidoDeNovedad(NoveltyMechanism.RateChange, 11) { TasaEfectivaAnual = 0.18m });

        var estado = MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 3, 1));

        estado.TasaDelDia!.TasaDiariaCorriente.Should().BeLessThan(MotorDeCartera.Reconstruir(sinNovedad, new DateOnly(2027, 3, 1)).TasaDelDia!.TasaDiariaCorriente);
        estado.Plan!.Entrada.TasaEfectivaAnual.Should().Be(0.18m);
    }

    /// <summary>
    /// La capitalización de una prórroga (T0874, cierre del hueco): las filas <c>Capitalization</c> sacan el interés de su cuota y entran como capital
    /// sin cuota; el plan nuevo suma lo capitalizado y el libro cuadra con los saldos.
    /// </summary>
    [Fact]
    public void La_capitalizacion_saca_el_interes_y_lo_vuelve_capital()
    {
        // Sin pagos: la 1 vence el 15-02 impaga; el 20-02 se prorroga capitalizando todo el interés corriente de la 1.
        var fecha = new DateOnly(2027, 2, 20);
        var antes = MotorDeCartera.Reconstruir(CreditosDePrueba.Simple(), fecha);
        var interes = antes.Saldo(LoanConcept.CurrentInterest, 1);
        interes.Should().BeGreaterThan(0m);
        var entrada = ConNovedad(fecha, new ContenidoDeNovedad(NoveltyMechanism.Extension, 18)
        {
            ReestructuracionesDespues = 1,
            Capitalizaciones = [new PartidaDeNovedad(LoanConcept.CurrentInterest, 1, interes)],
        });

        var estado = MotorDeCartera.Reconstruir(entrada, fecha, fecha);

        estado.Saldo(LoanConcept.CurrentInterest, 1).Should().Be(0m, "lo capitalizado sale de su cuota");
        estado.Saldo(LoanConcept.Principal).Should().Be(3_000_000m + interes, "y entra como capital");
        estado.Plan!.Cuotas.Skip(1).Sum(c => c.Capital).Should().Be(3_000_000m + interes, "el plan nuevo reparte el capital y lo capitalizado");
        estado.Libro.Movimientos.Where(m => m.Clase == LoanTransactionKind.Capitalization).Select(m => (m.Concepto, m.Cuota, m.Monto))
            .Should().BeEquivalentTo([(LoanConcept.CurrentInterest, (int?)1, -interes), (LoanConcept.Principal, (int?)null, interes)]);
        ElLibroCuadra(entrada, fecha.AddDays(40));

        var demasiado = ConNovedad(fecha, new ContenidoDeNovedad(NoveltyMechanism.Extension, 18)
        {
            Capitalizaciones = [new PartidaDeNovedad(LoanConcept.CurrentInterest, 1, interes + 1m)],
        });
        ((Action)(() => MotorDeCartera.Reconstruir(demasiado, fecha))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(GeneradorDePlan.CodigoNovedadInvalida, "no se capitaliza más que el saldo");
    }

    /// <summary>
    /// La novación en el crédito anterior y en el enlazado (T0874, cierre del hueco): el anterior traslada todo lo que queda —capital por cuota,
    /// interés— y deja de causar; el enlazado nace con el capital trasladado más lo capitalizado y recibe el interés no capitalizado en su cuota 1.
    /// Lo que sale del anterior es lo que entra al enlazado, y los dos libros cuadran.
    /// </summary>
    [Theory]
    [InlineData(NoveltyMechanism.Novation)]
    [InlineData(NoveltyMechanism.Refinancing)]
    public void La_novacion_traslada_todo_al_enlazado_y_el_anterior_deja_de_causar(NoveltyMechanism mecanismo)
    {
        var fecha = new DateOnly(2027, 3, 20);
        var antes = MotorDeCartera.Reconstruir(CreditosDePrueba.Simple(), fecha);
        var corriente1 = antes.Saldo(LoanConcept.CurrentInterest, 1);
        var corriente3 = antes.Saldo(LoanConcept.CurrentInterest, 3);
        var mora1 = antes.Saldo(LoanConcept.DefaultInterest, 1);
        corriente3.Should().BeGreaterThan(0m);
        mora1.Should().BeGreaterThan(0m);
        var anterior = ConNovedad(fecha, new ContenidoDeNovedad(mecanismo, 24)
        {
            ReestructuracionesDespues = 1,
            Capitalizaciones = [new PartidaDeNovedad(LoanConcept.CurrentInterest, 1, corriente1)],
        });

        var cerrado = MotorDeCartera.Reconstruir(anterior, fecha.AddDays(60), fecha);

        cerrado.Saldos.Should().BeEmpty("el anterior queda en cero");
        cerrado.Libro.Movimientos.Where(m => m.Clase == LoanTransactionKind.Accrual && m.Dia > fecha).Should().BeEmpty("y ya no causa nada desde el día siguiente");
        var trasladado = cerrado.Libro.Movimientos.Where(m => m.Clase == LoanTransactionKind.TransferToLinkedLoan).ToList();
        trasladado.Where(m => m.Concepto == LoanConcept.Principal).Sum(m => m.Monto).Should().Be(-3_000_000m);
        trasladado.Where(m => m.Concepto == LoanConcept.CurrentInterest && m.Cuota == 3).Sum(m => m.Monto).Should().Be(-corriente3);
        trasladado.Where(m => m.Concepto == LoanConcept.DefaultInterest && m.Cuota == 1).Sum(m => m.Monto).Should().Be(-mora1);
        trasladado.Should().NotContain(m => m.Concepto == LoanConcept.CurrentInterest && m.Cuota == 1, "lo capitalizado ya salió");
        ElLibroCuadra(anterior, fecha.AddDays(60));

        // El enlazado: nace con el capital + lo capitalizado y recibe lo demás en su cuota 1.
        var capital = 3_000_000m + corriente1;
        var traslados = new List<PartidaDeNovedad> { new(LoanConcept.Principal, null, 3_000_000m) };
        traslados.AddRange(trasladado.Where(m => m.Concepto != LoanConcept.Principal)
            .GroupBy(m => m.Concepto).Select(g => new PartidaDeNovedad(g.Key, 1, -g.Sum(m => m.Monto))));
        var plan = new EntradaDePlan(capital, fecha, 24, RepaymentPeriodicity.Monthly, AmortizationSystem.LevelPayment, 0.24m,
            DayCountConvention.Thirty360, RateEquivalence.Compound, RedondeoDeCartera.AlCentavo) { DiaDePago = 15 };
        var base_ = CreditosDePrueba.Simple();
        var enlazado = base_ with
        {
            Condiciones = base_.Condiciones with { Reestructuraciones = 1 },
            Plan = plan,
            Operaciones =
            [
                new EntradaDeOperacion("E1", LoanOperationKind.TermsChange, fecha, CreditosDePrueba.Instante(fecha), 1)
                {
                    Contenido = new ContenidoDeNovedad(mecanismo, 24)
                    {
                        Lado = LadoDeLaNovedad.Enlazado,
                        ReestructuracionesDespues = 1,
                        Capitalizaciones = [new PartidaDeNovedad(LoanConcept.CurrentInterest, 1, corriente1)],
                        Traslados = traslados,
                    },
                },
            ],
        };

        var nuevo = MotorDeCartera.Reconstruir(enlazado, fecha, fecha);
        nuevo.Saldo(LoanConcept.Principal).Should().Be(capital);
        nuevo.Saldo(LoanConcept.CurrentInterest, 1).Should().Be(-trasladado.Where(m => m.Concepto == LoanConcept.CurrentInterest).Sum(m => m.Monto),
            "la corriente no capitalizada de las cuotas 2 y 3 llega a su cuota 1");
        nuevo.Saldo(LoanConcept.DefaultInterest, 1).Should().Be(-trasladado.Where(m => m.Concepto == LoanConcept.DefaultInterest).Sum(m => m.Monto));
        nuevo.Plan!.Cuotas[0].Vencimiento.Day.Should().Be(15, "el día de pago pactado del enlazado");
        var entra = nuevo.Libro.Movimientos.Where(m => m.Clase is LoanTransactionKind.TransferFromLinkedLoan or LoanTransactionKind.Capitalization).Sum(m => m.Monto);
        var sale = -cerrado.Libro.Movimientos.Where(m => m.Clase is LoanTransactionKind.TransferToLinkedLoan or LoanTransactionKind.Capitalization).Sum(m => m.Monto);
        entra.Should().Be(sale, "lo que sale del anterior es lo que entra al enlazado");
        ElLibroCuadra(enlazado, fecha.AddDays(60));

        var mal = enlazado with { Plan = plan with { Monto = capital + 1m } };
        ((Action)(() => MotorDeCartera.Reconstruir(mal, fecha))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(ReconstruccionDeCredito.CodigoOrigenInvalido, "el plan pactado del enlazado es el de la novedad");
    }

    [Fact]
    public void Una_novedad_sin_su_decision_no_se_reconstruye()
    {
        var entrada = CreditosDePrueba.Simple();
        var sinDecision = entrada with
        {
            Operaciones = [.. entrada.Operaciones, new EntradaDeOperacion("N1", LoanOperationKind.TermsChange, new DateOnly(2027, 2, 1), CreditosDePrueba.Instante(new DateOnly(2027, 2, 1)), 2)],
        };
        ((Action)(() => MotorDeCartera.Reconstruir(sinDecision, new DateOnly(2027, 3, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(ReconstruccionDeCredito.CodigoOperacionNoSoportada);

        var enlazadoEnUnaProrroga = ConNovedad(new DateOnly(2027, 2, 1), new ContenidoDeNovedad(NoveltyMechanism.Extension, 18) { Lado = LadoDeLaNovedad.Enlazado });
        ((Action)(() => MotorDeCartera.Reconstruir(enlazadoEnUnaProrroga, new DateOnly(2027, 3, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(GeneradorDePlan.CodigoNovedadInvalida, "sólo una novación o refinanciación crean un enlazado");
    }

    /// <summary>La condonación (T0884): filas <c>Waiver</c> por concepto y cuota, sin pasar del saldo; el libro cuadra.</summary>
    [Fact]
    public void La_condonacion_baja_cada_concepto_de_su_cuota_sin_pasar_del_saldo()
    {
        var fecha = new DateOnly(2027, 3, 20);
        var antes = MotorDeCartera.Reconstruir(CreditosDePrueba.Simple(), fecha);
        var mora = antes.Saldo(LoanConcept.DefaultInterest, 1);
        mora.Should().BeGreaterThan(0m);
        var entrada = CreditosDePrueba.Simple();
        EntradaDeReconstruccion Con(decimal valor) => entrada with
        {
            Operaciones = [.. entrada.Operaciones, new EntradaDeOperacion("W1", LoanOperationKind.Waiver, fecha, CreditosDePrueba.Instante(fecha), 2)
            {
                Contenido = new ContenidoDeCondonacion([new PartidaDeNovedad(LoanConcept.DefaultInterest, 1, valor), new PartidaDeNovedad(LoanConcept.Principal, 1, 10_000m)]),
            }],
        };

        var estado = MotorDeCartera.Reconstruir(Con(mora), fecha, fecha);
        estado.Saldo(LoanConcept.DefaultInterest, 1).Should().Be(0m);
        estado.Saldo(LoanConcept.Principal).Should().Be(3_000_000m - 10_000m, "el capital condonado baja el de su cuota");
        estado.Libro.Movimientos.Where(m => m.Clase == LoanTransactionKind.Waiver).Should().HaveCount(2);
        ElLibroCuadra(Con(mora), fecha.AddDays(30));

        ((Action)(() => MotorDeCartera.Reconstruir(Con(mora + 0.01m), fecha))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(InterpreteDeCondonacion.CodigoExcedeElSaldo);
    }

    /// <summary>La insolvencia que termina (T0886): suspende desde su inicio y, fracasada o con acuerdo, vuelve a causar desde el día siguiente.</summary>
    [Fact]
    public void La_insolvencia_suspende_hasta_que_termina()
    {
        var inicio = new DateOnly(2027, 2, 1);
        var fin = new DateOnly(2027, 3, 1);
        var abierta = CreditosDePrueba.Simple(insolvencia: inicio);
        var cerrada = abierta with { Condiciones = abierta.Condiciones with { InsolvenciaHasta = fin } };

        MotorDeCartera.Reconstruir(abierta, fin.AddDays(5)).MotivoDeSuspension.Should().Be(Domain.Lending.Accrual.MotivoDeSuspension.Insolvencia);
        MotorDeCartera.Reconstruir(cerrada, fin).MotivoDeSuspension.Should().Be(Domain.Lending.Accrual.MotivoDeSuspension.Insolvencia);
        var despues = MotorDeCartera.Reconstruir(cerrada, fin.AddDays(5));
        despues.Suspendida.Should().BeFalse("la negociación terminó");
        despues.Transiciones.Should().Contain(t => t.Fecha == fin.AddDays(1), "se reanuda el día siguiente");
    }

    /// <summary>Σ libro desde el origen = saldos por concepto y cuota (el capital sin cuota, por total), como en los casos dorados.</summary>
    private static void ElLibroCuadra(EntradaDeReconstruccion entrada, DateOnly fecha)
    {
        var completo = MotorDeCartera.Reconstruir(entrada, fecha, entrada.Operaciones.Min(o => o.FechaValor));
        foreach (var grupo in completo.Libro.Movimientos.GroupBy(m => m.Concepto))
        {
            if (grupo.Any(m => m.Cuota is null))
                grupo.Sum(m => m.Monto).Should().Be(completo.Saldo(grupo.Key), $"Σ libro de {grupo.Key}");
            else
                foreach (var cuota in grupo.GroupBy(m => m.Cuota))
                    cuota.Sum(m => m.Monto).Should().Be(completo.Saldo(grupo.Key, cuota.Key), $"Σ libro de {grupo.Key} en la cuota {cuota.Key}");
        }
        MotorDeCartera.Verificar(entrada, fecha, completo.Libro.Movimientos).Should().BeEmpty("el libro de la reconstrucción cuadra consigo mismo");
    }
}
