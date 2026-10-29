using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Treasury.Checkbooks;

/// <summary>
/// Feature 015, T1, T0408 (FR-220; US19-3): al cierre del mes, los cheques emitidos y no entregados se reclasifican al efectivo de la cuenta con
/// <c>ChequesReclasificadosAlCierre</c> (y su reverso el primer día del mes siguiente); los entregados sin cobrar más allá de <c>Cheques.DiasACuentaPorPagar</c> pasan a cuenta
/// por pagar al beneficiario con <c>ChequeNoCobradoACuentaPorPagar</c>, nunca a ingreso; lo corre una persona, una vez por cuenta y mes y con el mes terminado. Prueba escrita,
/// sin correr.
/// </summary>
public class ChequesAlCierreDelMesTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly Checkbook _chequera;

    public ChequesAlCierreDelMesTests()
    {
        _chequera = new Checkbook { BankAccountId = _t.Pagos.Id, FromNumber = 1, ToNumber = 100, NextCheckNumber = 10, CustodianUserId = _t.E.Cajera.Id, RegisteredByUserId = _t.E.Cajera.Id };
        _t.E.Db.Checkbooks.Add(_chequera);
        _t.E.Db.SaveChanges();
    }

    private static DateOnly Corte => new DateOnly(EscenarioDeRecaudo.Hoy.Year, EscenarioDeRecaudo.Hoy.Month, 1).AddDays(-1);

    private RunMonthEndChecksCommandHandler Correr() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Numerador, _t.Parametros, _t.Emision,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

    private IssuedCheck Cheque(long numero, IssuedCheckState estado, DateOnly emitido, DateOnly? entregado = null, decimal valor = 1_000_000m)
    {
        var orden = _t.Orden(valor, PaymentOrderState.Disbursed, PaymentForm.Check);
        var c = new IssuedCheck
        {
            CheckbookId = _chequera.Id, CheckNumber = numero, PaymentOrderId = orden.Id, BeneficiaryPersonId = _t.Beneficiaria.Id, Amount = valor, IssuedOn = emitido,
            Status = estado, DeliveredOn = entregado,
        };
        _t.E.Db.IssuedChecks.Add(c);
        _t.E.Db.SaveChanges();
        return c;
    }

    [Fact]
    public async Task Reclasifica_los_emitidos_no_entregados_con_su_reverso_y_pasa_a_cuenta_por_pagar_los_antiguos()
    {
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.ChequesDiasACuentaPorPagar, "180", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
        Cheque(1, IssuedCheckState.Issued, Corte.AddDays(-3));
        var antiguo = Cheque(2, IssuedCheckState.Delivered, Corte.AddDays(-250), Corte.AddDays(-200));
        Cheque(3, IssuedCheckState.Delivered, Corte.AddDays(-30), Corte.AddDays(-20));

        var r = await Correr().Handle(new RunMonthEndChecksCommand(_t.Pagos.PublicId, Corte.Year, Corte.Month) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        (r.Value.ReclassifiedCount, r.Value.MovedToPayableCount).Should().Be((1, 1));
        var reclasificaciones = await _t.E.Db.IntegrationMessages.Where(m => m.Type == ChequesReclasificadosAlCierreV1.Type).ToListAsync();
        reclasificaciones.Should().HaveCount(2, "la reclasificación y su reverso del primer día del mes siguiente");
        reclasificaciones.Should().Contain(m => m.PayloadJson.Contains("\"isReversal\":true") || m.PayloadJson.Contains("\"IsReversal\":true"));
        var aPagar = await _t.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ChequeNoCobradoACuentaPorPagarV1.Type);
        aPagar.PayloadJson.Should().Contain(RolesDeTesoreriaT1.CuentaPorPagarChequesNoCobrados).And.NotContain("Ingreso");
        (await _t.E.Db.IssuedChecks.SingleAsync(c => c.Id == antiguo.Id)).MovedToPayableRunId.Should().NotBeNull();
    }

    [Fact]
    public async Task Corre_una_sola_vez_por_cuenta_y_mes_y_con_el_mes_terminado()
    {
        var primero = await Correr().Handle(new RunMonthEndChecksCommand(_t.Pagos.PublicId, Corte.Year, Corte.Month) { OperationKey = Guid.NewGuid() }, default);
        var repetido = await Correr().Handle(new RunMonthEndChecksCommand(_t.Pagos.PublicId, Corte.Year, Corte.Month) { OperationKey = Guid.NewGuid() }, default);
        var abierto = await Correr().Handle(new RunMonthEndChecksCommand(_t.Pagos.PublicId, EscenarioDeRecaudo.Hoy.Year, EscenarioDeRecaudo.Hoy.Month)
            { OperationKey = Guid.NewGuid() }, default);

        primero.IsSuccess.Should().BeTrue();
        repetido.Error.Code.Should().Be(ErroresDeTesoreria.CodigoCierreDeChequesRepetido);
        abierto.Error.Code.Should().Be(ErroresDeTesoreria.CodigoCierreDeChequesMesAbierto);
    }

    [Fact]
    public async Task Un_cheque_ya_pasado_a_cuenta_por_pagar_no_se_pasa_otra_vez()
    {
        var c = Cheque(4, IssuedCheckState.Delivered, Corte.AddDays(-400), Corte.AddDays(-390));
        c.MovedToPayableRunId = 99;
        CierreDelMesDeCheques.ParaCuentaPorPagar(c, Corte, 180).Should().BeFalse();
        c.MovedToPayableRunId = null;
        CierreDelMesDeCheques.ParaCuentaPorPagar(c, Corte, 180).Should().BeTrue();
        await Task.CompletedTask;
    }
}
