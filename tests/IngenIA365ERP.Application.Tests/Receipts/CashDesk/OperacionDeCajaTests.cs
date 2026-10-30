using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, T0103 (R-07; FR-038, FR-039, FR-189, FR-190): la operación nace <c>Preparing</c> con su <c>PublicId</c> estable; cada ítem se simula con su
/// módulo; el abono extraordinario trae sus dos alternativas y sin la elección del deudor la confirmación responde <c>Receipts.Receipt.ChoiceRequired</c>; el
/// anticipo que no abona a capital exige aceptar el aviso; un rechazo del módulo es por ítem; la operación se abandona. Escrita en la fase 6; se corre al final.
/// </summary>
public class OperacionDeCajaTests
{
    private readonly CajaConContratos _k = new();

    [Fact]
    public async Task Nace_en_preparacion_con_su_PublicId_estable()
    {
        await _k.AbrirAsync();

        var r = await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default);

        r.Value.Status.Should().Be(CashOperationState.Preparing);
        r.Value.ValueDate.Should().Be(EscenarioDeRecaudo.Hoy);
        (await _k.C.E.Db.CashOperations.SingleAsync()).PublicId.Should().Be(r.Value.PublicId);
    }

    [Fact]
    public async Task Sin_sesion_abierta_no_hay_operacion()
    {
        var r = await _k.Crear().Handle(new CreateCashOperationCommand(Guid.NewGuid(), _k.Asociado.PublicId), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSesionRequerida);
    }

    [Fact]
    public async Task Cada_item_se_simula_con_su_modulo_y_el_abono_extraordinario_trae_dos_alternativas()
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;

        var r = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId,
            [_k.Cuota(_k.CreditoA, 100_000), _k.Abono(_k.CreditoB, 300_000, null), _k.CuotaDeAportes(50_000)]), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Should().HaveCount(3);
        r.Value.Items[1].Alternatives.Should().HaveCount(2);
        r.Value.Items[0].SimulationId.Should().NotBeNullOrEmpty();
        await _k.Obligaciones.Received(2).SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>());
        await _k.Cuentas.Received(1).SimularAsync(Arg.Any<SolicitudDeSimulacionDeCuenta>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_la_eleccion_del_deudor_la_confirmacion_responde_ChoiceRequired()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([_k.Abono(_k.CreditoB, 300_000, null)], [_k.Efectivo(300_000)]);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoEleccionRequerida);
    }

    [Fact]
    public async Task El_anticipo_que_no_abona_a_capital_exige_aceptar_el_aviso()
    {
        await _k.AbrirAsync();
        _k.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeSimulacion>();
            return new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "",
                CajaConContratos.Simulacion(s, null, []) with { Distribution = [new LineaDeDistribucionDto(2, ConceptoDeObligacion.CurrentInterest, s.Amount)] });
        });
        var op = await _k.PreparadaAsync([new ItemPedidoDto(ApplicationTarget.Loan, 80_000, _k.CreditoA, LoanModality: ModalidadDePago.Advance)], [_k.Efectivo(80_000)]);
        op.Items.Single().AdvanceWithoutPrincipal.Should().BeTrue();

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoAvisoNoAceptado);
    }

    [Fact]
    public async Task Un_rechazo_del_modulo_al_simular_es_por_item_y_no_cambia_nada()
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;
        _k.Obligaciones.SimularAsync(Arg.Is<SolicitudDeSimulacion>(s => s.ObligationPublicId == _k.CreditoB), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Rejected, "Lending.Payment.ObligationBlocked", "Crédito bloqueado.", null));

        var r = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, 100_000), _k.Cuota(_k.CreditoB, 100_000)]), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        (await _k.C.E.Db.CashOperations.SingleAsync()).ItemsJson.Should().Be("[]");
    }

    [Fact]
    public async Task La_operacion_se_abandona()
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;

        var r = await _k.Abandonar().Handle(new AbandonCashOperationCommand(op.PublicId), default);

        r.Value.Status.Should().Be(CashOperationState.Abandoned);
        (await _k.Abandonar().Handle(new AbandonCashOperationCommand(op.PublicId), default)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoOperacionNoEnPreparacion);
    }
}
