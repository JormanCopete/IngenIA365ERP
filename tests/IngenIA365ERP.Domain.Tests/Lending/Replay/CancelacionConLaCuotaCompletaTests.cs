using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// 2026-11-16 (pedido del dueño; <c>Pagos.InteresesAlPagarAntesDelVencimiento</c> = «CuotaCompleta»): la cancelación que cobra el interés completo de la
/// cuota en curso, en el motor real. Un crédito de 3.000.000 a 12 cuotas mensuales al 24 % EA desembolsado el 15-01-2027 (cuota 1 el 15-02, cuota 2 el
/// 15-03) se cancela el 25-02: con la cuota completa la cuota 2 termina con exactamente el interés de su plan, nada se causa después, simular es
/// aplicar, una usura conocida después no cambia lo cobrado y el día del vencimiento anterior no hay cuota en curso que cobrar.
/// </summary>
public class CancelacionConLaCuotaCompletaTests
{
    private static readonly DateOnly Desembolso = new(2027, 1, 15);
    private static readonly DateOnly Pago = new(2027, 2, 25);

    private static EntradaDeOperacion Cancelacion(DateOnly fecha, decimal valor, bool cuotaCompleta, string id = "P1") =>
        new(id, LoanOperationKind.Payment, fecha, CreditosDePrueba.Instante(fecha), 2)
        {
            Contenido = new ContenidoDePago(PaymentModality.FullPayoff, valor) { CuotaCompleta = cuotaCompleta },
        };

    private static EntradaDeReconstruccion Con(params EntradaDeOperacion[] pagos)
    {
        var e = CreditosDePrueba.Simple(desembolso: Desembolso);
        return e with { Operaciones = [.. e.Operaciones, .. pagos] };
    }

    [Fact]
    public void Cobra_lo_que_faltaba_de_la_cuota_en_curso_y_la_cuota_termina_con_el_interes_de_su_plan()
    {
        var entrada = Con(Cancelacion(Pago, 4_000_000m, cuotaCompleta: true));
        var plan = MotorDeCartera.GenerarPlan(entrada.Plan!);
        var antes = MotorDeCartera.Reconstruir(Con(), Pago);
        var causado = antes.Saldo(LoanConcept.CurrentInterest, 2);

        var estado = MotorDeCartera.Reconstruir(entrada, new DateOnly(2027, 5, 31), Desembolso);
        var d = estado.Pagos["P1"];

        d.InteresPorCausar.Should().ContainSingle().Which.Should().Be(new LineaDeDistribucion(2, LoanConcept.CurrentInterest, plan.Cuotas[1].Interes - causado));
        d.Lineas.Single(l => l.Cuota == 2 && l.Concepto == LoanConcept.CurrentInterest).Monto.Should().Be(plan.Cuotas[1].Interes,
            "la cuota 2 se cobra con todo el interés de su plan (la base del período es su saldo inicial)");
        d.Avisos.Should().Equal(AplicadorDePagos.AvisoInteresDeLaCuotaCompleta);
        d.Cancela.Should().BeTrue();
        d.Excedente.Should().Be(4_000_000m - antes.ValorParaCancelar - d.InteresDeLaCuotaEnCurso);
        estado.ValorParaCancelar.Should().Be(0m, "después de la cancelación no queda nada");
        estado.Saldos.Should().BeEmpty();
        estado.Libro.Movimientos.Where(m => m.Clase == LoanTransactionKind.Accrual && m.Dia > Pago).Should().BeEmpty("sin capital ya no se causa");
        estado.Libro.Movimientos.Should().ContainSingle(m => m.Clase == LoanTransactionKind.UnaccruedInstallmentInterest)
            .Which.Should().Be(new MovimientoDeLibro(Pago, LoanTransactionKind.UnaccruedInstallmentInterest, LoanConcept.CurrentInterest, 2,
                d.InteresDeLaCuotaEnCurso) { OperacionId = "P1" });
        AplicadorDePagos.ValorParaCancelar(antes, cuotaCompleta: true).Should().Be(antes.ValorParaCancelar + d.InteresDeLaCuotaEnCurso);
        AplicadorDePagos.ValorParaCancelar(antes, cuotaCompleta: false).Should().Be(antes.ValorParaCancelar);
    }

    [Fact]
    public void Simular_es_aplicar_y_el_corrido_es_lo_de_siempre()
    {
        var antes = MotorDeCartera.Reconstruir(Con(), Pago);
        var simulada = AplicadorDePagos.Distribuir(antes, new SolicitudDeDistribucion(PaymentModality.FullPayoff, 4_000_000m, Pago) { CuotaCompleta = true },
            Prelacion.PorDefecto, RedondeoDeCartera.AlCentavo);
        var aplicada = MotorDeCartera.Reconstruir(Con(Cancelacion(Pago, 4_000_000m, cuotaCompleta: true)), Pago).Pagos["P1"];

        aplicada.Lineas.Should().Equal(simulada.Lineas);
        aplicada.InteresPorCausar.Should().Equal(simulada.InteresPorCausar);
        HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, aplicada)
            .Should().Be(HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, simulada), "simular = aplicar (FR-072)");

        var corrida = MotorDeCartera.Reconstruir(Con(Cancelacion(Pago, 4_000_000m, cuotaCompleta: false)), Pago).Pagos["P1"];
        corrida.InteresPorCausar.Should().BeEmpty();
        corrida.Avisos.Should().BeEmpty();
        corrida.Aplicado.Should().Be(antes.ValorParaCancelar);
        HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, corrida)
            .Should().NotBe(HuellaDeCartera.DeSimulacion(Guid.Empty, VersionesDeReglas.Vigente, aplicada));
    }

    [Fact]
    public void Una_usura_que_rige_despues_y_se_conoce_mas_tarde_no_cambia_lo_cobrado()
    {
        var pago = Cancelacion(Pago, 4_000_000m, cuotaCompleta: true);
        var baja = new ValorDeUsura(new DateOnly(2027, 3, 1), 0.10m, CreditosDePrueba.Instante(new DateOnly(2027, 2, 27)), 99, Guid.NewGuid());
        var conBaja = Con(pago) with { Usura = [.. Con().Usura.Where(u => u.ValidFrom < new DateOnly(2027, 3, 1)), baja] };

        var alAplicar = MotorDeCartera.Reconstruir(conBaja with { InformacionHasta = CreditosDePrueba.Instante(Pago, 16) }, Pago).Pagos["P1"];
        var despues = MotorDeCartera.Reconstruir(conBaja, new DateOnly(2027, 4, 30)).Pagos["P1"];

        despues.InteresPorCausar.Should().Equal(alAplicar.InteresPorCausar, "la proyección congela la usura vigente en la fecha valor");
        despues.Lineas.Should().Equal(alAplicar.Lineas);
    }

    [Fact]
    public void El_dia_del_vencimiento_anterior_no_hay_cuota_en_curso_que_cobrar()
    {
        var vence = new DateOnly(2027, 2, 15);
        var d = MotorDeCartera.Reconstruir(Con(Cancelacion(vence, 4_000_000m, cuotaCompleta: true)), vence).Pagos["P1"];

        d.InteresPorCausar.Should().BeEmpty("la cuota 2 empieza ese día: todavía no corre");
        d.Avisos.Should().BeEmpty();
        d.Cancela.Should().BeTrue();
    }

    [Fact]
    public void Si_los_fondos_no_cubren_lo_causado_no_se_cobra_y_si_no_alcanzan_lo_que_faltaba_queda_debiendose()
    {
        var antes = MotorDeCartera.Reconstruir(Con(), Pago);
        var porCausar = antes.InteresPorCausarDeLaCuotaEnCurso().Sum(l => l.Monto);
        porCausar.Should().BeGreaterThan(0m);

        var corta = MotorDeCartera.Reconstruir(Con(Cancelacion(Pago, antes.ValorParaCancelar - 1m, cuotaCompleta: true)), Pago).Pagos["P1"];
        corta.InteresPorCausar.Should().BeEmpty("sin cubrir el capital, la causación sigue y cobrará lo suyo");
        corta.Cancela.Should().BeFalse();

        var media = antes.ValorParaCancelar + 1_000m;
        var estado = MotorDeCartera.Reconstruir(Con(Cancelacion(Pago, media, cuotaCompleta: true)), new DateOnly(2027, 3, 10));
        var d = estado.Pagos["P1"];
        d.InteresPorCausar.Sum(l => l.Monto).Should().Be(porCausar, "con el capital pagado, la cuota en curso se cobra entera");
        d.Excedente.Should().Be(0m);
        d.Cancela.Should().BeFalse();
        estado.Saldo(LoanConcept.Principal).Should().Be(0m);
        estado.Saldo(LoanConcept.CurrentInterest, 2).Should().Be(porCausar - 1_000m, "lo que faltó de la cuota en curso queda en ella, sin causar más");
    }
}
