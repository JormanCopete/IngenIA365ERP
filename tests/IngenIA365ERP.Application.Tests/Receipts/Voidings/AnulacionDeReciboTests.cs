using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Voidings;

/// <summary>
/// Feature 015, US5, T0170 (FR-057, FR-058, FR-064, FR-194; D4, D5): se anula el recibo entero con motivo; lo pide el cajero y lo aprueba otra persona
/// (<c>Receipts.Voiding.SelfApproval</c>); reversa cada porción vigente con su clave <c>:reverso</c> —obligaciones antes que cuentas—, todo o nada
/// (<c>.ContractRejected</c>); omite las aplicaciones que el módulo ya reversó; con la sesión abierta baja lo esperado; cerrada, saldo a favor del pagador; hecho
/// <c>ReciboAnulado</c> espejo; <c>.AlreadyVoided</c>; el rechazo devuelve el recibo a <c>Valid</c>. Prueba escrita, sin correr.
/// </summary>
public class AnulacionDeReciboTests
{
    private readonly CajaConContratos _k = new();
    private readonly List<string> _orden = [];
    private readonly IAutoridadDeOtroAprobador _permisos = Substitute.For<IAutoridadDeOtroAprobador>();

    public AnulacionDeReciboTests()
    {
        _k.C.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = ReceiptDocumentKind.Voiding });
        _k.C.E.Db.SaveChanges();
        _k.C.Motor.SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result.Success<ApprovalRequest?>(new ApprovalRequest { SourcePublicId = ci.ArgAt<SolicitudDeAprobacion>(0).SourcePublicId }));
        _k.C.Motor.DecidirAsync(Arg.Any<DecisionDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(ci => Result.Success(new DecisionResultDto(ci.ArgAt<DecisionDeAprobacion>(0).RequestPublicId,
                ci.ArgAt<DecisionDeAprobacion>(0).Decision == ApprovalDecisionKind.Approve ? nameof(ApprovalRequestStatus.Approved) : nameof(ApprovalRequestStatus.Rejected),
                1, new EstadoDeFuenteDto(Guid.Empty, "x", "x", null))));
        _k.Obligaciones.ReversarAsync(Arg.Any<SolicitudDeReverso>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            _orden.Add("obligacion");
            return new RespuestaDelContratoDto<ComprobanteDeReversoDto>(ResultadoDelContrato.Applied, "OK", "", null);
        });
        _k.Cuentas.ReversarAsync(Arg.Any<SolicitudDeReversoDeCuenta>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            _orden.Add("cuenta");
            return new RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>(ResultadoDelContrato.Applied, "OK", "", null);
        });
    }

    private AuditoriaDeRecaudo Auditoria => new(_k.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    private RequestReceiptVoidingCommandHandler Pedir() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Motor, _permisos, Auditoria, _k.C.E.Reloj);

    private ApproveReceiptVoidingCommandHandler Aprobar() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Numerador, _k.C.Motor, _k.C.E.Personas, _permisos,
        _k.Obligaciones, _k.Cuentas, _k.C.Emision, Auditoria, _k.C.E.Reloj);

    private RejectReceiptVoidingCommandHandler Rechazar() => new(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Motor, Auditoria, _k.C.E.Reloj);

    private async Task<ReceiptDto> ReciboAsync()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 100_000), _k.CuotaDeAportes(50_000)], [_k.Efectivo(150_000)]);
        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        return r.Value;
    }

    [Fact]
    public async Task Lo_pide_el_cajero_lo_aprueba_otra_persona_y_reversa_obligaciones_antes_que_cuentas()
    {
        var recibo = await ReciboAsync();
        var pedida = await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Digitó mal el crédito") { OperationKey = Guid.NewGuid() }, default);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? pedida.Error.Code : null);
        pedida.Value.InSameSession.Should().BeTrue();
        _k.C.E.Db.IssuedReceipts.Single().Status.Should().Be(ReceiptState.VoidPendingApproval);

        (await Aprobar().Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, null) { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoAnulacionAutoaprobada);

        _k.C.E.Como(_k.C.E.Supervisor);
        var aprobada = await Aprobar().Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, "Revisado") { OperationKey = Guid.NewGuid() }, default);

        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Code : null);
        aprobada.Value.Status.Should().Be(ReceiptVoidingState.Applied);
        aprobada.Value.Number.Should().NotBeNullOrEmpty();
        _orden.Should().Equal("obligacion", "cuenta");
        await _k.Obligaciones.Received(1).ReversarAsync(Arg.Is<SolicitudDeReverso>(s => s.Collector.Code == ClavesDeRecaudo.Codigo), Arg.Any<CancellationToken>());
        _k.C.E.Db.ReceiptApplicationPortions.Should().OnlyContain(p => p.Status == PortionState.Reversed && p.ReversalKey == ClavesDeRecaudo.Reverso(p.IdempotencyKey));
        _k.C.E.Db.IssuedReceipts.Single().Status.Should().Be(ReceiptState.Voided);
        _k.C.E.Db.PayerCreditBalances.Should().BeEmpty("con la sesión abierta el efectivo baja lo esperado");
        _k.C.E.Db.IntegrationMessages.Count(m => m.Type == ReciboAnuladoV1.Type).Should().Be(1);
    }

    [Fact]
    public async Task Anular_un_recibo_con_descuento_por_pago_anticipado_reversa_tambien_el_descuento()
    {
        // 2026-11-26 (pedido del dueño): la cuota 3 anticipada completa con el 22,22 % del interés de descuento; el asociado pagó 106.714 y Cartera recibió 110.404.
        var todas = Enum.GetValues<ModalidadDePago>();
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "", new ListadoDeObligacionesDto(EstadoDeCartera.Active,
                ci.Arg<SolicitudDeListado>().PersonPublicId, EscenarioDeRecaudo.Hoy,
                [new ObligacionListadaDto(_k.CreditoA, "CR-A", "PRESTOR18", "Préstamo", RolEnLaObligacion.Debtor, EstadoDeObligacion.Active, 0, 0m, 110_404m, 0m,
                    2_000_000m, 0m, 0m, todas, [], false, false)])));
        _k.Obligaciones.DetalleAsync(Arg.Any<SolicitudDeDetalle>(), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<DetalleDeObligacionDto>(ResultadoDelContrato.Answered, "OK", "", new DetalleDeObligacionDto(_k.CreditoA, "CR-A", "Préstamo",
                "Vigente", 0, 0m, 0m, 2_000_000m, 0m, 0m, 110_404m, EscenarioDeRecaudo.Hoy.AddDays(20), [],
                [new CuotaDeObligacionDto(3, EscenarioDeRecaudo.Hoy.AddDays(50), "Upcoming", 91_800m, 16_604m, 2_000m, 0m, 110_404m, 110_404m, 0m, 110_404m)],
                todas, true, []) { AdvanceDiscountFraction = 0.2222222222m }));
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([new ItemPedidoDto(ApplicationTarget.Loan, 110_404m, _k.CreditoA, LoanModality: ModalidadDePago.SpecificInstallment,
            InstallmentNumber: 3, ApplyAdvanceDiscount: true)], [_k.Efectivo(106_714m)]);
        var recibo = (await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default)).Value;
        var pedida = await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Era otra cuota") { OperationKey = Guid.NewGuid() }, default);
        _k.C.E.Como(_k.C.E.Supervisor);

        var aprobada = await Aprobar().Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, "Revisado") { OperationKey = Guid.NewGuid() }, default);

        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Code : null);
        var hecho = await _k.C.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ReciboAnuladoV1.Type);
        var contenido = System.Text.Json.JsonSerializer.Deserialize<ReciboAnuladoV1>(hecho.PayloadJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var descuento = contenido.Lines.Single(l => l.Role == RolesDeRecaudo.DescuentoPorPagoAnticipado);
        (descuento.Side, descuento.Amount).Should().Be((RolesDeRecaudo.Credito, 3_690m));
        contenido.Lines.Where(l => l.Role == RolesDeRecaudo.PuenteDelModulo).Sum(l => l.Amount).Should().Be(110_404m, "lo que el módulo reversa: la cuota completa");
        contenido.Lines.Where(l => l.Side == RolesDeRecaudo.Debito).Sum(l => l.Amount)
            .Should().Be(contenido.Lines.Where(l => l.Side == RolesDeRecaudo.Credito).Sum(l => l.Amount), "cuadra");
    }

    [Fact]
    public async Task Con_el_permiso_de_anular_sin_segunda_persona_quien_la_pide_la_aplica_sin_solicitud_de_aprobacion()
    {
        _permisos.TienePermisoAsync(Arg.Any<int>(), AprobarAnulacion.PermisoSinSegundaPersona, Arg.Any<CancellationToken>()).Returns(true);
        var recibo = await ReciboAsync();

        var pedida = await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Lo cobré mal") { OperationKey = Guid.NewGuid() }, default);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? pedida.Error.Code : null);
        pedida.Value.ApprovalRequestPublicId.Should().BeNull();
        await _k.C.Motor.DidNotReceive().SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>());

        var aplicada = await Aprobar().Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, null) { OperationKey = Guid.NewGuid() }, default);

        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? aplicada.Error.Code : null);
        aplicada.Value.Status.Should().Be(ReceiptVoidingState.Applied);
        _k.C.E.Db.IssuedReceipts.Single().Status.Should().Be(ReceiptState.Voided);
    }

    [Fact]
    public async Task Con_el_permiso_aprueba_la_que_ella_misma_pidio_cerrando_la_solicitud_sin_decidirla()
    {
        var recibo = await ReciboAsync();
        var pedida = await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Error") { OperationKey = Guid.NewGuid() }, default);
        pedida.Value.ApprovalRequestPublicId.Should().NotBeNull();
        _permisos.TienePermisoAsync(Arg.Any<int>(), AprobarAnulacion.PermisoSinSegundaPersona, Arg.Any<CancellationToken>()).Returns(true);

        var aplicada = await Aprobar().Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, null) { OperationKey = Guid.NewGuid() }, default);

        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? aplicada.Error.Code : null);
        aplicada.Value.Status.Should().Be(ReceiptVoidingState.Applied);
        await _k.C.Motor.Received(1).InvalidarAsync(Arg.Any<string>(), pedida.Value.PublicId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _k.C.Motor.DidNotReceive().DecidirAsync(Arg.Any<DecisionDeAprobacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_rechazo_del_contrato_no_deja_nada()
    {
        var recibo = await ReciboAsync();
        var pedida = await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Error") { OperationKey = Guid.NewGuid() }, default);
        _k.Cuentas.ReversarAsync(Arg.Any<SolicitudDeReversoDeCuenta>(), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>(ResultadoDelContrato.Rejected, "Lending.Account.Locked", "Cuenta bloqueada", null));
        _k.C.E.Como(_k.C.E.Supervisor);

        var r = await Aprobar().Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, null) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoAnulacionRechazadaPorContrato);
    }

    [Fact]
    public async Task Omite_las_aplicaciones_que_el_modulo_ya_reverso_y_con_la_sesion_cerrada_deja_saldo_a_favor()
    {
        var recibo = await ReciboAsync();
        var aportes = _k.C.E.Db.ReceiptApplications.Single(a => a.Target == ApplicationTarget.Account);
        aportes.Status = ApplicationState.ReversedByModule;
        foreach (var p in _k.C.E.Db.ReceiptApplicationPortions.Where(p => p.ReceiptApplicationId == aportes.Id)) p.Status = PortionState.Reversed;
        var sesion = await _k.C.E.Db.ReceiptsCashSessions.SingleAsync();
        sesion.Status = CashSessionState.Closed;
        _k.C.E.Db.SaveChanges();
        var pedida = await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Cerrada") { OperationKey = Guid.NewGuid() }, default);
        _k.C.E.Como(_k.C.E.Supervisor);

        var r = await Aprobar().Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, null) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.OmittedApplications.Should().ContainSingle();
        await _k.Cuentas.DidNotReceive().ReversarAsync(Arg.Any<SolicitudDeReversoDeCuenta>(), Arg.Any<CancellationToken>());
        var saldo = _k.C.E.Db.PayerCreditBalances.Single();
        saldo.Origin.Should().Be(PayerCreditBalanceOrigin.VoidAfterSessionClose);
        saldo.Amount.Should().Be(100_000);
        r.Value.PayerCreditBalancePublicId.Should().Be(saldo.PublicId);
    }

    [Fact]
    public async Task Un_recibo_ya_anulado_o_pendiente_no_se_vuelve_a_pedir_y_el_rechazo_lo_devuelve_a_vigente()
    {
        var recibo = await ReciboAsync();
        var pedida = await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Primera") { OperationKey = Guid.NewGuid() }, default);
        (await Pedir().Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Segunda") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoAnulacionYaHecha);

        _k.C.E.Como(_k.C.E.Supervisor);
        var rechazada = await Rechazar().Handle(new RejectReceiptVoidingCommand(pedida.Value.PublicId, "No procede") { OperationKey = Guid.NewGuid() }, default);

        rechazada.Value.Status.Should().Be(ReceiptVoidingState.Rejected);
        _k.C.E.Db.IssuedReceipts.Single().Status.Should().Be(ReceiptState.Valid);
    }
}
