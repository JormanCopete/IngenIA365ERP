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

    [Theory]
    [InlineData(NoveltyMechanism.Novation)]
    [InlineData(NoveltyMechanism.Refinancing)]
    public void La_novacion_y_la_refinanciacion_no_se_reconstruyen_en_el_credito_anterior(NoveltyMechanism mecanismo)
    {
        var entrada = ConNovedad(new DateOnly(2027, 2, 1), new ContenidoDeNovedad(mecanismo, 12));
        ((Action)(() => MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 3, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(ReconstruccionDeCredito.CodigoOperacionNoSoportada);
    }

    [Fact]
    public void La_capitalizacion_todavia_no_se_reconstruye_y_sin_decision_tampoco()
    {
        var capitaliza = ConNovedad(new DateOnly(2027, 2, 1), new ContenidoDeNovedad(NoveltyMechanism.Extension, 18) { InteresCapitalizado = 1_000m });
        ((Action)(() => MotorDeCartera.Reconstruir(capitaliza, new DateOnly(2027, 3, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(ReconstruccionDeCredito.CodigoOperacionNoSoportada);

        var entrada = CreditosDePrueba.Simple();
        var sinDecision = entrada with
        {
            Operaciones = [.. entrada.Operaciones, new EntradaDeOperacion("N1", LoanOperationKind.TermsChange, new DateOnly(2027, 2, 1), CreditosDePrueba.Instante(new DateOnly(2027, 2, 1)), 2)],
        };
        ((Action)(() => MotorDeCartera.Reconstruir(sinDecision, new DateOnly(2027, 3, 1)))).Should().Throw<ReglaDeCarteraException>()
            .Which.Codigo.Should().Be(ReconstruccionDeCredito.CodigoOperacionNoSoportada);
    }
}
