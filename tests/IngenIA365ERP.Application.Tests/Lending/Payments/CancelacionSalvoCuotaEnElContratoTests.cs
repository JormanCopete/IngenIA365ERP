using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration.Contrato;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Payoffs;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 028 (R4, T058, T059, T062; FR-019): la cancelación de todo menos la cuota causada por el contrato de obligaciones real (<see cref="PagosDePrueba"/>:
/// Ana, 3.000.000 a 24 cuotas desde el 01-12-2026; la cuota 1 vence el 01-01-2027). Sólo la admiten la recogida (<c>OwnLoanPayoff</c>) y Recaudo; se simula
/// sin valor (cuesta lo que cuesta); al aplicarla deja la cuota en curso con el interés del período, sella en la decisión la cuota, la regla y el interés
/// fijado, registra el plan sin las cuotas siguientes y el libro cuadra. Un crédito de vivienda se recoge completo.
/// </summary>
public class CancelacionSalvoCuotaEnElContratoTests : IAsyncLifetime
{
    private static readonly DateOnly AMitad = PagosDePrueba.Hoy.AddDays(10);
    private static readonly Recaudador Recogida = new("CAR", ClaseDeRecaudador.OwnLoanPayoff);
    private PagosDePrueba _p = null!;

    // 2026-11-27 (D13 ajustada): estas pruebas son la cuota tal como se envió (D13-A): el crédito de Ana fuera del tope legal del prepago (1 SMMLV). Las del
    // proporcional, dentro del tope, arman su propio escenario.
    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync(topeSmmlv: 1m);

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private PagoAAplicar DeLaRecogida(decimal valor) => _p.Pago(ModalidadDePago.PayoffExceptCurrentInstallment, valor, AMitad,
        clave: $"CAR:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}", medio: ClaseDeMedioDeRecaudo.InternalOffset);

    [Fact]
    public void Solo_la_recogida_y_Recaudo_admiten_cancelar_todo_menos_la_cuota_causada()
    {
        foreach (var clase in Enum.GetValues<ClaseDeRecaudador>())
        {
            ClasesDeMedioDeRecaudo.AdmiteModalidad(clase, ModalidadDePago.PayoffExceptCurrentInstallment).Should()
                .Be(clase is ClaseDeRecaudador.OwnLoanPayoff or ClaseDeRecaudador.Receipts, clase.ToString());
            foreach (var otra in Enum.GetValues<ModalidadDePago>().Where(m => m != ModalidadDePago.PayoffExceptCurrentInstallment))
                ClasesDeMedioDeRecaudo.AdmiteModalidad(clase, otra).Should().BeTrue($"{clase} admite {otra} como siempre");
        }
        ClasesDeMedioDeRecaudo.ValidarModalidad(ClaseDeRecaudador.Payroll, ModalidadDePago.PayoffExceptCurrentInstallment)!.Code
            .Should().Be(ErroresDePagos.CodigoModalidadNoAdmitida);
        ClasesDeMedioDeRecaudo.AdmiteModalidad(ClaseDeRecaudador.Receipts, (ModalidadDePago)99).Should().BeFalse();
    }

    [Fact]
    public void La_simulacion_sin_valor_se_admite_como_la_cancelacion_total()
    {
        var validador = new SolicitudDeSimulacionValidator();
        validador.Validate(new SolicitudDeSimulacion(Recogida, Guid.NewGuid(), ModalidadDePago.PayoffExceptCurrentInstallment, 0m, AMitad))
            .IsValid.Should().BeTrue("cuesta lo que cueste: el contrato lo calcula");
        validador.Validate(new SolicitudDeSimulacion(Recogida, Guid.NewGuid(), ModalidadDePago.FreeAmount, 0m, AMitad)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Con_otro_recaudador_el_contrato_la_rechaza_al_simular_y_al_aplicar()
    {
        await _p.CausarHastaAsync(AMitad);
        var antes = await _p.Db.LoanOperations.CountAsync();

        var simulada = await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, _p.Credito.PublicId,
            ModalidadDePago.PayoffExceptCurrentInstallment, 0m, AMitad), default);
        var aplicada = await _p.AplicarAsync(_p.Pago(ModalidadDePago.PayoffExceptCurrentInstallment, 1_000_000m, AMitad));

        simulada.Code.Should().Be(ErroresDePagos.CodigoModalidadNoAdmitida);
        aplicada.Code.Should().Be(ErroresDePagos.CodigoModalidadNoAdmitida);
        (await _p.Db.LoanOperations.CountAsync()).Should().Be(antes, "no se escribe nada");
    }

    [Fact]
    public async Task La_recogida_deja_la_cuota_causada_sella_la_decision_y_el_libro_cuadra()
    {
        await _p.CausarHastaAsync(AMitad);
        var cargado = await _p.C.Cargador().CargarAsync(_p.Credito.Id, null, AMitad, default);
        var estado = MotorDeCartera.Reconstruir(cargado!.Entrada!, AMitad);
        var porCausar = estado.InteresPorCausarDeLaCuotaEnCurso().Sum(l => l.Monto);
        porCausar.Should().BePositive("a mitad de la cuota 1 le falta el interés de los días que quedan");
        var cuota1 = estado.Plan!.Cuotas[0];

        // Se simula sin valor: cuesta el capital de las cuotas 2 a 24 (la 1 queda con lo suyo).
        var simulada = await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(Recogida, _p.Credito.PublicId,
            ModalidadDePago.PayoffExceptCurrentInstallment, 0m, AMitad), default);
        simulada.Result.Should().Be(ResultadoDelContrato.Answered, simulada.Message);
        var cuesta = simulada.Value!.AppliedAmount;
        cuesta.Should().Be(estado.Plan.Cuotas.Skip(1).Sum(c => c.Capital));
        simulada.Value.Notices.Should().Contain(AplicadorDePagos.AvisoCuotaCausadaDejada);
        AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, cuotaCompleta: false).QuedaEnLaCuota
            .Should().Be(cuota1.Capital + cuota1.Interes + cuota1.Seguro + cuota1.Administracion, "la cuota queda como se envió");

        var pago = DeLaRecogida(cuesta);
        var r = await _p.AplicarAsync(pago, recaudador: Recogida);
        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        r.Value!.PaidOff.Should().BeFalse("la cuota 1 sigue debiéndose");
        r.Value.Surplus.Should().Be(0m);
        r.Value.Warnings.Should().Contain(AplicadorDePagos.AvisoCuotaCausadaDejada);
        var repetido = await _p.AplicarAsync(pago, recaudador: Recogida);
        repetido.Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
        repetido.Value!.Warnings.Should().Contain(AplicadorDePagos.AvisoCuotaCausadaDejada, "el comprobante repetido lo dice igual");

        var op = await _p.OperacionAsync(r.Value.OperationPublicId);
        op.PaymentModality.Should().Be(PaymentModality.PayoffExceptCurrentInstallment);
        var decision = DecisionDePago.Leer(op.Payload)!;
        decision.Installment.Should().Be(1, "la decisión sella la cuota que dejó aunque la decidiera el aplicador");
        decision.LeftInstallmentRule.Should().Be(AplicadorDePagos.ReglaDeLaCuotaDejada);
        decision.LeftInstallmentInterest.Should().Be(porCausar);
        decision.Contenido().Cuota.Should().Be(1);

        var filas = await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id).ToListAsync();
        var causada = filas.Should().ContainSingle(t => t.Kind == LoanTransactionKind.UnaccruedInstallmentInterest).Subject;
        (causada.Concept, causada.InstallmentNumber, causada.Amount, causada.ValueDate).Should().Be((LoanConcept.CurrentInterest, 1, porCausar, AMitad));
        filas.Where(t => t.Kind == LoanTransactionKind.PaymentAllocation).Should().OnlyContain(t => t.InstallmentNumber > 1,
            "la cuota 1 no se toca");

        var plan = await _p.Db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == _p.Credito.Id).OrderByDescending(v => v.VersionNumber).FirstAsync();
        plan.InstallmentCount.Should().Be(1, "las cuotas siguientes salen del plan");
        (await _p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _p.Credito.Id)).Status.Should().Be(LoanStatus.Active);

        var despues = MotorDeCartera.Reconstruir((await _p.C.Cargador().CargarAsync(_p.Credito.Id, null, AMitad.AddDays(5), default))!.Entrada!, AMitad.AddDays(5));
        despues.Saldo(LoanConcept.CurrentInterest, 1).Should().Be(cuota1.Interes, "la cuota 1 lleva el interés del período completo y no causa más");
        despues.ValorParaCancelar.Should().Be(cuota1.Capital + cuota1.Interes);
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task El_reverso_de_la_recogida_devuelve_el_plan_y_descongela_la_cuota()
    {
        await _p.CausarHastaAsync(AMitad);
        var cargado = await _p.C.Cargador().CargarAsync(_p.Credito.Id, null, AMitad, default);
        var antes = MotorDeCartera.Reconstruir(cargado!.Entrada!, AMitad);
        var porCausar = antes.InteresPorCausarDeLaCuotaEnCurso().Sum(l => l.Monto);
        var pago = DeLaRecogida(AplicadorDePagos.ValorParaCancelarSalvoCuota(antes, null, cuotaCompleta: false).ParaCancelar);
        (await _p.AplicarAsync(pago, recaudador: Recogida)).Result.Should().Be(ResultadoDelContrato.Applied);

        var r = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(Recogida, pago.IdempotencyKey, "Se anuló el crédito nuevo antes del giro"), default);
        _p.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        var reverso = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.PaymentReversal);
        var espejo = await _p.Db.LoanTransactions.AsNoTracking()
            .SingleAsync(t => t.OperationId == reverso.Id && t.Kind == LoanTransactionKind.UnaccruedInstallmentInterest);
        espejo.Amount.Should().Be(-porCausar, "lo que se causó en la cuota dejada se deshace");
        var plan = await _p.Db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == _p.Credito.Id).OrderByDescending(v => v.VersionNumber).FirstAsync();
        plan.InstallmentCount.Should().Be(antes.Plan!.Cuotas.Count, "el plan vuelve a tener todas sus cuotas");
        var despues = MotorDeCartera.Reconstruir((await _p.C.Cargador().CargarAsync(_p.Credito.Id, null, AMitad, default))!.Entrada!, AMitad);
        despues.Huella.Should().Be(antes.Huella, "como si la recogida no hubiera existido");
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public void Un_credito_que_no_admite_dejar_la_cuota_se_recoge_completo()
    {
        var liquidacion = new LiquidacionDeCreditoRecogido
        {
            LoanId = 1, LoanPublicId = Guid.NewGuid(), Number = "CR-1", Fecha = AMitad, InformacionHasta = DateTime.UtcNow, ValorARecoger = 100m,
            Desglose = new DesgloseDeRecogida(Capital: 100m), Intereses = ModoDeInteresesAlCancelar.Corrido, Proporcionales = "ComoAlCancelar",
            Huella = "sha256:x", AdmiteDejarLaCuota = false, RecogidoCompletoPorque = ErroresDeRecogida.MotivoViviendaCompleta,
        };

        liquidacion.TratamientoEfectivo(InstallmentTreatment.Exclude).Should().Be(InstallmentTreatment.Include);
        liquidacion.Proyeccion(InstallmentTreatment.Exclude).TratamientoDeCuota.Should().Be(InstallmentTreatment.Include);
        (liquidacion with { AdmiteDejarLaCuota = true, RecogidoCompletoPorque = null }).TratamientoEfectivo(InstallmentTreatment.Exclude)
            .Should().Be(InstallmentTreatment.Exclude);
    }
}
