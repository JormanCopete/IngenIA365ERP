using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, R1, T0360 (FR-046; D14): el recaudo pendiente de aplicar.
/// <list type="bullet">
/// <item>sólo si el contrato responde «no habilitado» y la persona está en una carga aplicada sin activar (Cartera por <c>IConsultaDeCargaSinActivar</c>, Captaciones
/// en sus exigibles); fuera de ese caso el ítem se rechaza (<c>Receipts.PendingApplication.NotEligible</c>);</item>
/// <item>por persona y producto, por valor libre; al confirmar no va a ningún contrato, queda la fila y el hecho <c>RecaudoPendienteReconocido</c> (sin
/// <c>ReciboEmitido</c> si no hay más);</item>
/// <item>se aplica después por una persona que elige el producto y la modalidad, con la fecha valor del recibo y la clave de la porción; el hecho
/// <c>RecaudoPendienteAplicado</c>; repetir no aplica dos veces.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class RecaudoPendienteDeAplicarTests
{
    private readonly CajaConContratos _k = new();
    private readonly IConsultaDeCargaSinActivar _carga = Substitute.For<IConsultaDeCargaSinActivar>();
    private bool _carteraActiva;
    private bool _enLaCarga = true;

    public RecaudoPendienteDeAplicarTests()
    {
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "",
                new ListadoDeObligacionesDto(_carteraActiva ? EstadoDeCartera.Active : EstadoDeCartera.NotActivated, ci.Arg<SolicitudDeListado>().PersonPublicId,
                    EscenarioDeRecaudo.Hoy, [])));
        _carga.PersonaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new RespuestaDelContratoDto<CargaSinActivarDto>(ResultadoDelContrato.Answered, "OK", "",
                new CargaSinActivarDto(_enLaCarga, _enLaCarga ? [new ObligacionCargadaDto(Guid.NewGuid(), "CR-MIG-1", "Consumo", new DateOnly(2026, 11, 30))] : [])));
    }

    private UpdateCashOperationItemsCommandHandler Items() => new(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas, _carga);

    private ConfirmacionDeRecibo Confirmacion() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.E.Reloj, _k.C.Cerrojo, _k.C.Numerador, _k.C.E.Parametros,
        _k.C.Presente, new Application.Accounting.Receipts.ContabilidadParaRecaudo(Substitute.For<MediatR.ISender>()), _k.Obligaciones, _k.Cuentas, _k.Listas,
        _k.C.Emision, _k.C.E.Alertas, cargaSinActivar: _carga);

    private ApplyPendingApplicationCommandHandler Aplicar() => new(_k.C.E.Db, _k.C.E.Actor, _k.C.E.Reloj, _k.C.Cerrojo, _k.Obligaciones, _k.Cuentas, _k.C.Emision,
        new AuditoriaDeRecaudo(_k.C.E.Servicios, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditoriaDeRecaudo>.Instance));

    private static ItemPedidoDto Pendiente(decimal valor) =>
        new(ApplicationTarget.PendingApplication, valor, PendingTarget: ApplicationTarget.Loan, ProductReference: "CR-MIG-1");

    private async Task<Guid> OperacionConPendienteAsync(decimal valor = 80_000m)
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;
        var items = await Items().Handle(new UpdateCashOperationItemsCommand(op, [Pendiente(valor)]), default);
        items.IsSuccess.Should().BeTrue(items.IsFailure ? items.Error.Message : null);
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(valor)]), default)).IsSuccess.Should().BeTrue();
        return op;
    }

    [Fact]
    public async Task Fuera_de_una_carga_aplicada_sin_activar_el_item_se_rechaza()
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;

        _enLaCarga = false;
        var fuera = await Items().Handle(new UpdateCashOperationItemsCommand(op, [Pendiente(10_000m)]), default);
        fuera.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);

        _enLaCarga = true;
        _carteraActiva = true;
        var activa = await Items().Handle(new UpdateCashOperationItemsCommand(op, [Pendiente(10_000m)]), default);
        activa.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado, "con Cartera activada se paga el crédito por su contrato");
    }

    [Fact]
    public async Task Al_confirmar_queda_el_pendiente_sin_contrato_y_su_hecho_reconocido()
    {
        var op = await OperacionConPendienteAsync();

        var r = await Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        await _k.Obligaciones.DidNotReceive().AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
        var pendiente = await _k.C.E.Db.PendingApplications.AsNoTracking().SingleAsync();
        (pendiente.Status, pendiente.Amount, pendiente.TargetModule, pendiente.ValueDate).Should()
            .Be((PendingApplicationState.Pending, 80_000m, ApplicationTarget.Loan, EscenarioDeRecaudo.Hoy));
        (await _k.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == RecaudoPendienteReconocidoV1.Type)).Should().Be(1);
        (await _k.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == ReciboEmitidoV1.Type)).Should().Be(0, "no hay otra aplicación en el recibo");
    }

    [Fact]
    public async Task Se_aplica_despues_con_la_modalidad_elegida_la_fecha_valor_del_recibo_y_la_clave_de_la_porcion()
    {
        var op = await OperacionConPendienteAsync();
        await Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);
        var pendiente = await _k.C.E.Db.PendingApplications.AsNoTracking().SingleAsync();
        _carteraActiva = true;
        var credito = Guid.NewGuid();

        var r = await Aplicar().Handle(new ApplyPendingApplicationCommand(pendiente.PublicId, credito, ModalidadDePago.CycleInstallment), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(PendingApplicationState.Applied);
        var porcion = await _k.C.E.Db.ReceiptApplicationPortions.AsNoTracking().SingleAsync();
        await _k.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.Single().IdempotencyKey == porcion.IdempotencyKey
            && s.Payments.Single().ValueDate == EscenarioDeRecaudo.Hoy && s.Payments.Single().ObligationPublicId == credito), Arg.Any<CancellationToken>());
        (await _k.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == RecaudoPendienteAplicadoV1.Type)).Should().Be(1);

        var otra = await Aplicar().Handle(new ApplyPendingApplicationCommand(pendiente.PublicId, Guid.NewGuid(), ModalidadDePago.CycleInstallment), default);
        otra.Error.Code.Should().Be(ErroresDelPendiente.CodigoNoPendiente, "un pendiente se aplica una vez");
    }
}
