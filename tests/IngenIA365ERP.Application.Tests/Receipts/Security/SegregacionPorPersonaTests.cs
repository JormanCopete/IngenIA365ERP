using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Enums.Approvals;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Security;

/// <summary>
/// Feature 015, T0054 (US12; R-31; FR-012, FR-013, FR-184): la segregación es por <b>persona</b> (dos usuarios de la misma persona son la misma, nunca por el
/// entero del token); la suplencia del supervisor la tiene otra persona, con vigencia; la caja pide el aprobador <b>presente</b> (428 con la solicitud, la
/// misma operación sigue con la solicitud aprobada) y quien aprueba es otra persona con una atribución que alcance; la Tesorería pide la aprobación
/// <b>diferida</b> por el motor y su fuente rechaza al que preparó.
/// </summary>
public class SegregacionPorPersonaTests
{
    private readonly EscenarioDeRecaudo _e = new();
    private readonly IMotorDeAprobaciones _motor = Substitute.For<IMotorDeAprobaciones>();
    private static readonly Guid Operacion = Guid.NewGuid();

    private PedidoDeAprobacionPresente Pedido(Guid? aprobacion = null, string huella = "h1") =>
        new(ApprovalSubjects.CashCountDifference, Operacion, "Diferencia de arqueo", 50_000, EscenarioDeRecaudo.Hoy, huella,
            "Receipts.Count.ApproveDifference", [], aprobacion);

    private ApprovalRequest Solicitud(ApprovalRequestStatus estado, string huella = "h1", int? aprobador = null)
    {
        var s = new ApprovalRequest
        {
            Subject = ApprovalSubjects.CashCountDifference, SourceType = ApprovalSourceTypes.ReceiptsOperation, SourcePublicId = Operacion,
            SourceLabel = "x", Amount = 50_000, OperationDate = EscenarioDeRecaudo.Hoy, CreatedByUserId = _e.Cajera.Id, RequestedByUserId = _e.Cajera.Id,
            Status = estado, CurrentLevel = 1, ContentSha256 = huella, RequestedAt = DateTime.UtcNow,
        };
        s.SellarNiveles([new NivelDeAprobacion(1, 0m, "Receipts.Count.ApproveDifference")]);
        if (aprobador is { } a)
            s.Decisions.Add(new ApprovalDecision { Level = 1, Decision = ApprovalDecisionKind.Approve, DecidedByUserId = a, DecidedByName = "sup", DecidedAt = DateTime.UtcNow });
        _e.Db.ApprovalRequests.Add(s);
        _e.Db.SaveChanges();
        return s;
    }

    private AprobacionPresenteEnCaja Presente() => new(_e.Db, _motor, _e.Actor, new ServiceCollection().BuildServiceProvider());

    [Fact]
    public async Task Dos_usuarios_de_la_misma_persona_son_la_misma_persona()
    {
        (await _e.Segregacion.MismaPersonaAsync(_e.Cajera.Id, _e.CajeraBis.Id)).Should().BeTrue();
        (await _e.Segregacion.MismaPersonaAsync(_e.Cajera.Id, _e.Supervisor.Id)).Should().BeFalse();
        (await _e.Segregacion.MismaPersonaAsync(_e.SinPersona.Id, _e.SinPersona.Id)).Should().BeTrue("un usuario es igual a sí mismo");
        (await _e.Segregacion.MismaPersonaAsync(_e.SinPersona.Id, _e.Cajera.Id)).Should().BeFalse();

        var r = await _e.Segregacion.OtraPersonaAsync(_e.Cajera.Id, _e.CajeraBis.Id, new Error("Receipts.Count.SelfApproval", "x"));
        r.Error.Code.Should().Be("Receipts.Count.SelfApproval");
    }

    [Fact]
    public async Task La_suplencia_la_tiene_otra_persona_con_vigencia()
    {
        var punto = _e.Punto("NORTE");
        var handler = new RegisterSupervisorDelegationCommandHandler(_e.Db, _e.Alcance, _e.Segregacion);

        var misma = await handler.Handle(new RegisterSupervisorDelegationCommand(punto.PublicId, _e.Cajera.PublicId, _e.CajeraBis.PublicId,
            EscenarioDeRecaudo.Hoy, null, "vacaciones"), CancellationToken.None);
        var otra = await handler.Handle(new RegisterSupervisorDelegationCommand(punto.PublicId, _e.Supervisor.PublicId, _e.OtroCajero.PublicId,
            EscenarioDeRecaudo.Hoy, EscenarioDeRecaudo.Hoy.AddDays(5), "vacaciones"), CancellationToken.None);

        misma.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSuplenteMismaPersona);
        otra.IsSuccess.Should().BeTrue();
        var vigentes = await new ListSupervisorDelegationsQueryHandler(_e.Db, _e.Alcance, _e.Reloj)
            .Handle(new ListSupervisorDelegationsQuery(punto.PublicId, OnlyCurrent: true), CancellationToken.None);
        vigentes.Value.Should().ContainSingle(d => d.DelegateName == "oscar");
    }

    [Fact]
    public async Task Sin_la_aprobacion_la_caja_responde_428_con_la_solicitud_guardada()
    {
        var nueva = new ApprovalRequest { Subject = ApprovalSubjects.CashCountDifference, SourceType = ApprovalSourceTypes.ReceiptsOperation, SourcePublicId = Operacion };
        nueva.SellarNiveles([new NivelDeAprobacion(1, 0m, "Receipts.Count.ApproveDifference")]);
        _motor.SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _e.Db.ApprovalRequests.Add(nueva); return Result.Success<ApprovalRequest?>(nueva); });

        var r = await Presente().ExigirAsync(Pedido(), CancellationToken.None);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoPresenciaRequerida);
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { approvalRequestPublicId = nueva.PublicId }, o => o.ExcludingMissingMembers());
        _e.Db.ApprovalRequests.Should().ContainSingle(a => a.PublicId == nueva.PublicId, "la solicitud sobrevive al 428");
        await _motor.Received(1).SolicitarAsync(Arg.Is<SolicitudDeAprobacion>(s => s.CreatedByUserId == _e.Cajera.Id && s.ContentSha256 == "h1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_la_solicitud_aprobada_la_misma_operacion_sigue_con_su_aprobador()
    {
        var aprobada = Solicitud(ApprovalRequestStatus.Approved, aprobador: _e.Supervisor.Id);

        var r = await Presente().ExigirAsync(Pedido(aprobada.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.AprobadorUserId.Should().Be(_e.Supervisor.Id);
        r.Value.ApprovalRequestPublicId.Should().Be(aprobada.PublicId);
    }

    [Fact]
    public async Task Pendiente_vuelve_al_428_rechazada_falla_y_otra_huella_no_corresponde()
    {
        var pendiente = Solicitud(ApprovalRequestStatus.Pending);
        (await Presente().ExigirAsync(Pedido(pendiente.PublicId), CancellationToken.None)).Error.Code.Should().Be(ErroresDeRecaudo.CodigoPresenciaRequerida);
        (await Presente().ExigirAsync(Pedido(pendiente.PublicId, huella: "otra"), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoAprobacionNoCorresponde, "si cambió lo que se aprueba, la aprobación ya no vale");

        var rechazada = Solicitud(ApprovalRequestStatus.Rejected, huella: "h2");
        (await Presente().ExigirAsync(Pedido(rechazada.PublicId, huella: "h2"), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoAprobacionRechazada);
    }

    [Fact]
    public async Task Sin_encabezado_se_reusa_la_solicitud_pendiente()
    {
        var pendiente = Solicitud(ApprovalRequestStatus.Pending);

        var r = await Presente().ExigirAsync(Pedido(), CancellationToken.None);

        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new { approvalRequestPublicId = pendiente.PublicId }, o => o.ExcludingMissingMembers());
        await _motor.DidNotReceiveWithAnyArgs().SolicitarAsync(default!, default);
    }

    [Fact]
    public async Task Si_la_politica_no_la_exige_la_operacion_sigue_sin_aprobacion()
    {
        _motor.SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>()).Returns(Result.Success<ApprovalRequest?>(null));

        var r = await Presente().ExigirAsync(Pedido(), CancellationToken.None);

        r.Value.Should().Be(AprobacionPresenteObtenida.NoHizoFalta);
    }

    [Fact]
    public async Task La_fuente_de_Recaudo_rechaza_al_mismo_y_al_que_no_le_alcanza_la_atribucion()
    {
        var solicitud = Solicitud(ApprovalRequestStatus.Pending);
        var fuente = new FuenteDeAprobacionDeRecaudo(_e.Db, _e.Personas);

        (await fuente.AlAprobarElNivelAsync(solicitud, _e.CajeraBis.Id, ApprovalMethod.InPersonTotp, true, CancellationToken.None)).Error.Code
            .Should().Be(FuenteDeAprobacionDeRecaudo.CodigoAutoaprobacion, "es la misma persona que la cajera");
        (await fuente.AlAprobarElNivelAsync(solicitud, _e.Supervisor.Id, ApprovalMethod.InPersonTotp, true, CancellationToken.None)).IsSuccess
            .Should().BeTrue("sin límite registrado, la atribución no limita");
    }

    [Fact]
    public async Task La_aprobacion_diferida_de_Tesoreria_la_pide_el_motor_y_su_fuente_rechaza_al_que_preparo()
    {
        var aprobaciones = new AprobacionDiferidaEnTesoreria(_e.Db, _motor, _e.Actor);
        _motor.SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>()).Returns(Result.Success<ApprovalRequest?>(new ApprovalRequest()));

        var r = await aprobaciones.PedirAsync(new PedidoDeAprobacionDiferida(ApprovalSubjects.PaymentBatchApproval, Guid.NewGuid(), "Lote 1", 80_000_000,
            EscenarioDeRecaudo.Hoy, "lote", "Treasury.Batches.Approve", []), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        await _motor.Received(1).SolicitarAsync(Arg.Is<SolicitudDeAprobacion>(s => s.SourceType == ApprovalSourceTypes.TreasuryDocument && s.Amount == 80_000_000),
            Arg.Any<CancellationToken>());

        var lote = new ApprovalRequest { Subject = ApprovalSubjects.PaymentBatchApproval, RequestedByUserId = _e.Cajera.Id, CreatedByUserId = _e.Cajera.Id, Amount = 1 };
        var fuente = new FuenteDeAprobacionDeTesoreria(_e.Db, _e.Personas, []);
        (await fuente.AlAprobarElNivelAsync(lote, _e.CajeraBis.Id, ApprovalMethod.OwnSession, true, CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeTesoreria.CodigoLoteAutoaprobado);
        (await fuente.AlAprobarElNivelAsync(lote, _e.Supervisor.Id, ApprovalMethod.OwnSession, true, CancellationToken.None)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_segregacion_de_Tesoreria_compara_personas()
    {
        var t = new SegregacionDeTesoreria(_e.Personas);
        (await t.AprobarLoteAsync(_e.Cajera.Id, _e.CajeraBis.Id)).Error.Code.Should().Be(ErroresDeTesoreria.CodigoLoteAutoaprobado);
        (await t.DescargarArchivoAsync(_e.Cajera.Id, [_e.Supervisor.Id], _e.Supervisor.Id)).Error.Code.Should().Be(ErroresDeTesoreria.CodigoDescargaPorQuienPreparo);
        (await t.DescargarArchivoAsync(_e.Cajera.Id, [_e.Supervisor.Id], _e.OtroCajero.Id)).IsSuccess.Should().BeTrue();
        (await t.RegistrarResultadoAsync(false, _e.Cajera.Id, [_e.Supervisor.Id], _e.Cajera.Id)).IsSuccess.Should().BeTrue("sin la bandera, cualquiera");
        (await t.RegistrarResultadoAsync(true, _e.Cajera.Id, [_e.Supervisor.Id], _e.CajeraBis.Id)).Error.Code
            .Should().Be(ErroresDeTesoreria.CodigoResultadoPorTerceraPersona);
        (await t.ConciliarAsync(_e.OtroCajero.Id, [_e.Cajera.Id, _e.Supervisor.Id])).IsSuccess.Should().BeTrue();
        (await t.ConciliarAsync(_e.CajeraBis.Id, [_e.Cajera.Id])).Error.Code.Should().Be(ErroresDeTesoreria.CodigoConciliacionConflicto);
    }
}
