using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.BankItems;

/// <summary>
/// Feature 015, R1, T0377 (FR-069, FR-071; D11): la consignación reportada en ventanilla.
/// <list type="bullet">
/// <item>por defecto queda por verificar y no aplica nada; con <c>AplicarDeInmediato</c> se aplica con medio «consignación» y queda pendiente de cruzar;</item>
/// <item>al verificarla con la línea del extracto: la por verificar se aplica entonces, la ya aplicada sólo identifica la línea con el mismo recibo (no se aplica dos
/// veces);</item>
/// <item>la que no aparece en los días configurados alerta (sin cambiarla) y se muestra vencida.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class ConsignacionReportadaTests
{
    private readonly BancosDePrueba _b = new();
    private readonly ICuentasBancariasPropias _propias = Substitute.For<ICuentasBancariasPropias>();

    public ConsignacionReportadaTests()
    {
        var oficina = _b.K.C.E.OficinaA.PublicId;
        _propias.PorPublicIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CuentaBancariaPropiaDto(_b.Cuenta.PublicId, "RECAUDO", "Banco de pruebas", "1007", "Savings", "****5555", oficina, true, true));
        _b.K.C.E.Como(_b.K.C.E.Cajera);
    }

    private RegisterReportedDepositCommandHandler Registrar() => new(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Actor, _b.K.C.E.Reloj, _propias, _b.K.C.Numerador, _b.K.C.Cerrojo,
        _b.K.C.E.Parametros, _b.Aplicacion, _b.Auditoria);

    private VerifyReportedDepositCommandHandler Verificar() => new(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Actor, _b.Movimientos, _b.K.C.E.Parametros, _b.Aplicacion,
        _b.Partidas, _b.Auditoria, _b.K.C.E.Reloj);

    private RegisterReportedDepositCommand Pedido(decimal valor = 90_000m) =>
        new(_b.K.Asociado.PublicId, _b.Cuenta.PublicId, EscenarioDeRecaudo.Hoy.AddDays(-1), valor, Guid.NewGuid(), ApplicationTarget.Loan, _b.K.CreditoA, "SOP-1")
        {
            OperationKey = Guid.NewGuid(),
        };

    [Fact]
    public async Task Por_defecto_queda_por_verificar_y_al_cruzarla_se_aplica()
    {
        var r = await Registrar().Handle(Pedido(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(ReportedDepositStatus.ToVerify);
        r.Value.AppliedImmediately.Should().BeFalse();
        (await _b.K.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(0);

        var abono = _b.Abono(90_000m, diasAtras: 0, referencia: "SOP-1");
        _b.K.C.E.Como(_b.K.C.E.Supervisor);
        var v = await Verificar().Handle(new VerifyReportedDepositCommand(r.Value.PublicId, abono.PublicId), default);

        v.IsSuccess.Should().BeTrue(v.IsFailure ? v.Error.Message : null);
        v.Value.Status.Should().Be(ReportedDepositStatus.Verified);
        v.Value.ReceiptNumber.Should().NotBeNullOrEmpty();
        await _b.K.Obligaciones.Received(1).AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aplicada_de_inmediato_al_cruzarla_no_se_aplica_otra_vez()
    {
        _b.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.ConsignacionReportadaModo, "AplicarDeInmediato");

        var r = await Registrar().Handle(Pedido(), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.AppliedImmediately.Should().BeTrue();
        r.Value.ReceiptNumber.Should().NotBeNullOrEmpty();
        await _b.K.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.All(p => p.MeansClass == ClaseDeMedioDeRecaudo.BankDeposit)),
            Arg.Any<CancellationToken>());

        var abono = _b.Abono(90_000m, diasAtras: 0);
        _b.K.C.E.Como(_b.K.C.E.Supervisor);
        var v = await Verificar().Handle(new VerifyReportedDepositCommand(r.Value.PublicId, abono.PublicId), default);

        v.Value.Status.Should().Be(ReportedDepositStatus.Verified);
        v.Value.ReceiptNumber.Should().Be(r.Value.ReceiptNumber, "la línea queda identificada con el mismo recibo");
        await _b.K.Obligaciones.Received(1).AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
        (await _b.K.C.E.Db.BankItemIdentifications.SingleAsync()).Amount.Should().Be(90_000m);
    }

    [Fact]
    public async Task Un_abono_que_no_alcanza_no_la_verifica()
    {
        var r = await Registrar().Handle(Pedido(90_000m), default);
        var abono = _b.Abono(50_000m, diasAtras: 0);

        var v = await Verificar().Handle(new VerifyReportedDepositCommand(r.Value.PublicId, abono.PublicId), default);

        v.Error.Code.Should().Be(ErroresDeIdentificacion.CodigoNoCuadra);
    }

    [Fact]
    public async Task La_que_no_aparece_en_los_dias_configurados_alerta_y_se_muestra_vencida()
    {
        var r = await Registrar().Handle(Pedido() with { DepositDate = EscenarioDeRecaudo.Hoy.AddDays(-10) }, default);
        var tarea = new Application.Receipts.Tareas.AntiguedadDeRecaudo(_b.K.C.E.Db, _b.K.C.E.Parametros, _b.Movimientos,
            Substitute.For<Application.Common.Calendar.ICalendarioHabil>(), _b.K.C.E.Alertas, _b.K.C.E.Reloj);

        (await tarea.ConsignacionesReportadasAsync(default)).Should().Be(1);

        await _b.K.C.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoConsignacionReportadaSinVerificar),
            Arg.Any<CancellationToken>());
        var lista = await new ListReportedDepositsQueryHandler(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Parametros, _b.K.C.E.Reloj)
            .Handle(new ListReportedDepositsQuery(), default);
        lista.Value.Single(d => d.PublicId == r.Value.PublicId).Status.Should().Be(ReportedDepositStatus.Expired);
        (await _b.K.C.E.Db.ReportedDeposits.SingleAsync()).Status.Should().Be(ReportedDepositStatus.ToVerify, "la tarea sólo avisa");
    }
}
