using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0374 (T64, FR-039, FR-040): los asuntos de acta de la originación. <see cref="AsuntoDeActaDeSolicitud"/> y
/// <see cref="AsuntoDeActaDeCupo"/> describen el punto (número, persona, monto, nivel, vinculado y fundamento; el órgano que decide; quienes no
/// votan) y, al cerrar el acta, aplican el resultado —aprobación (también con condiciones), negación, devolución— con las mismas revalidaciones que
/// la decisión individual; si una falla, el cierre entero se revierte.
/// </summary>
public sealed class AsuntosDeActaDeOriginacionTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;
    private GoverningBodyMember _m1 = null!, _m2 = null!;
    private User _secretaria = null!;

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync();
        _m1 = _o.G.Miembro(_o.Comite, _o.G.Persona("Uno", "Comité", "7001"));
        _m2 = _o.G.Miembro(_o.Comite, _o.G.Persona("Dos", "Comité", "7002"));
        _o.G.Miembro(_o.Comite, _o.G.Persona("Tres", "Comité", "7003"));
        _secretaria = _o.UsuarioConPersona("Sara", "7004");
        _o.Credito(_o.Dario, 40_000_000m, "CR-D");
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private AsuntoDeActaDeSolicitud AsuntoDeSolicitud() =>
        new(_o.Db, _o.Evaluacion(), _o.Validaciones(), _o.Enrutamiento(), _o.DecisionDeSolicitud(), _o.G.Reloj);

    private AsuntoDeActaDeCupo AsuntoDeCupo() => new(_o.Db, _o.EnrutamientoDeCupo(), _o.DecisionDeCupo());

    private AsuntosDeActa Asuntos() => new([AsuntoDeSolicitud(), AsuntoDeCupo()]);

    private void ComoSecretaria()
    {
        _o.G.ActuarComo(_secretaria);
        _o.G.Conceder("Lending.Sessions.Read", "Lending.Sessions.Manage", "Lending.Sessions.RecordVotes", "Lending.Sessions.Close");
    }

    private async Task<(SessionDto Sesion, SessionItemDto Punto)> SesionConPuntoAsync(string sujeto, Guid id)
    {
        ComoSecretaria();
        var creada = await new CreateSessionCommandHandler(_o.Db, _o.G.Reloj).Handle(new CreateSessionCommand(_o.Comite.PublicId, SessionKind.Ordinary,
            SessionMode.InPerson, OriginacionDePrueba.Hoy) { OperationKey = Guid.NewGuid() }, default);
        creada.IsSuccess.Should().BeTrue(creada.IsFailure ? creada.Error.Message : string.Empty);
        var abierta = await new RecordSessionAttendanceCommandHandler(_o.Db, _o.G.Reloj).Handle(new RecordSessionAttendanceCommand(creada.Value.PublicId,
            [new AttendeeInput(_m1.PublicId, true), new AttendeeInput(_m2.PublicId, true)]) { OperationKey = Guid.NewGuid() }, default);
        abierta.IsSuccess.Should().BeTrue(abierta.IsFailure ? abierta.Error.Message : string.Empty);
        var punto = await new AddSessionItemCommandHandler(_o.Db, Asuntos(), new VinculadosVigentes(_o.Db), new ReglasDeVinculados(_o.G.Valores, _o.G.Perfil),
            _o.G.Reloj).Handle(new AddSessionItemCommand(creada.Value.PublicId, sujeto, id, SessionItemKind.Decision) { OperationKey = Guid.NewGuid() }, default);
        punto.IsSuccess.Should().BeTrue(punto.IsFailure ? $"{punto.Error.Code}: {punto.Error.Message}" : string.Empty);
        _o.Db.ChangeTracker.Clear();
        return (abierta.Value, punto.Value);
    }

    private async Task VotarAsync(SessionDto s, SessionItemDto p, VoteValue voto)
    {
        var presencia = new PresenciaEnSesion(_o.Db, Substitute.For<IMfaDirectory>(), Substitute.For<IWebAuthnService>(),
            Substitute.For<ICentralIdentityProvider>(), Substitute.For<IDesafiosDePresencia>(), _o.G.Reloj);
        foreach (var miembro in new[] { _m1, _m2 })
        {
            var persona = await _o.Db.People.AsNoTracking().SingleAsync(x => x.Id == miembro.PersonId);
            var r = await new RecordSessionVoteCommandHandler(_o.Db, Asuntos(), _o.G.Intervenciones(), presencia, _o.G.Permisos, _o.G.Actor, _o.G.Reloj)
                .Handle(new RecordSessionVoteCommand(s.PublicId, p.ItemPublicId, persona.PublicId, voto, VoteMethod.RecordedBySecretary, null, null)
                    { OperationKey = Guid.NewGuid() }, default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        }
        _o.Db.ChangeTracker.Clear();
    }

    private async Task<Result<SessionDto>> CerrarAsync(SessionDto s)
    {
        var huella = (await new GetSessionQueryHandler(_o.Db, Asuntos(), _o.G.Auditoria, _o.G.Reloj).Handle(new GetSessionQuery(s.PublicId), default))
            .Value.CurrentContentSha256!;
        var r = await new CloseSessionCommandHandler(_o.Db, Asuntos(), _o.G.Intervenciones(), new NumeradorDeCartera(_o.Db, _o.Cerrojo), new SinActaEnPdf(),
            _o.G.Actor, _o.G.Reloj).Handle(new CloseSessionCommand(s.PublicId, huella) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<CreditApplicationDto> SolicitudDelComiteAsync()
    {
        _o.G.ActuarComo(_o.Asesora);
        return await _o.RadicadaAsync(_o.Solicitud(3_000_000m, 24, _o.Dario));
    }

    // ------------------------------------------------------------------------------------------------------- describir --

    [Fact]
    public async Task La_solicitud_describe_el_punto_su_organo_y_quienes_no_votan()
    {
        var s = await SolicitudDelComiteAsync();

        var d = (await AsuntoDeSolicitud().DescribirAsync(s.PublicId, OriginacionDePrueba.Hoy, default)).Value;

        d.Titulo.Should().Contain(s.Number!).And.Contain("Darío").And.Contain("3.000.000");
        d.Resumen.Should().Contain("Comité de crédito").And.Contain("Sin personas vinculadas").And.Contain("Fundamento:").And.Contain("43.000.000");
        d.Pendiente.Should().BeTrue();
        d.GoverningBodyId.Should().Be(_o.Comite.Id);
        d.MontoConsolidado.Should().Be(43_000_000m);
        d.ContentSha256.Should().Be(d.RoutingSha256).And.HaveLength(64);
        d.Personas.Should().ContainEquivalentOf(new PersonaDelAsunto(_o.Asesora.PersonId!.Value, MotivosDeExclusion.Radicador));
        d.Personas.Should().ContainEquivalentOf(new PersonaDelAsunto(_o.Dario.Id, MotivosDeExclusion.Solicitante));
    }

    [Fact]
    public async Task El_cupo_describe_el_punto_y_quien_lo_pidio_no_vota()
    {
        _o.MontoOperativo(_o.Asesora, 1_000_000m);
        _o.G.ActuarComo(_o.Asesora);
        var cupo = (await _o.Asignar().Handle(new AssignCreditLimitCommand(_o.Beto.PublicId, CreditLimitKind.Consolidated, null, CreditLimitOrigin.Assigned,
            60_000_000m, OriginacionDePrueba.Hoy, null, null, "Cupo empresarial") { OperationKey = Guid.NewGuid() }, default)).Value;
        _o.Db.ChangeTracker.Clear();

        var d = (await AsuntoDeCupo().DescribirAsync(cupo.PublicId, OriginacionDePrueba.Hoy, default)).Value;

        d.Pendiente.Should().BeTrue();
        d.Titulo.Should().Contain("consolidado").And.Contain("Beto").And.Contain("60.000.000");
        d.Resumen.Should().Contain("Comité de crédito").And.Contain("Cupo empresarial");
        d.GoverningBodyId.Should().Be(_o.Comite.Id);
        d.ContentSha256.Should().Be(cupo.Routing!.ContentSha256);
        d.Personas.Select(p => p.Motivo).Should().BeEquivalentTo([MotivosDeExclusion.Radicador, MotivosDeExclusion.Solicitante]);
    }

    // ---------------------------------------------------------------------------------------------------------- cerrar --

    [Fact]
    public async Task Aprobar_en_el_acta_aprueba_la_solicitud_con_la_decision_colegiada()
    {
        var s = await SolicitudDelComiteAsync();
        var (sesion, punto) = await SesionConPuntoAsync(LendingDecisionSubjects.CreditApplication, s.PublicId);
        await VotarAsync(sesion, punto, VoteValue.For);

        var r = await CerrarAsync(sesion);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var leida = await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId);
        var decision = await _o.Db.CreditDecisions.AsNoTracking().SingleAsync();
        leida.Status.Should().Be(CreditApplicationStatus.Approved);
        leida.ApprovalDecisionId.Should().Be(decision.Id);
        leida.ApprovedAmount.Should().Be(3_000_000m);
        leida.ApprovalValidUntil.Should().Be(OriginacionDePrueba.Hoy.AddDays(30));
        decision.SessionItemId.Should().NotBeNull();
    }

    [Fact]
    public async Task Aprobar_con_condiciones_en_el_acta_aprueba_lo_menor()
    {
        var s = await SolicitudDelComiteAsync();
        var (sesion, punto) = await SesionConPuntoAsync(LendingDecisionSubjects.CreditApplication, s.PublicId);
        var propuesta = await new UpdateSessionItemCommandHandler(_o.Db, _o.G.Reloj).Handle(new UpdateSessionItemCommand(sesion.PublicId, punto.ItemPublicId,
            CreditDecisionOutcome.ApproveWithConditions, "{\"amount\":2000000}") { OperationKey = Guid.NewGuid() }, default);
        propuesta.IsSuccess.Should().BeTrue(propuesta.IsFailure ? propuesta.Error.Message : string.Empty);
        _o.Db.ChangeTracker.Clear();
        await VotarAsync(sesion, punto, VoteValue.For);

        (await CerrarAsync(sesion)).IsSuccess.Should().BeTrue();

        var leida = await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId);
        leida.Status.Should().Be(CreditApplicationStatus.Approved);
        leida.ApprovedAmount.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task Sin_la_mayoria_el_acta_niega_la_solicitud()
    {
        var s = await SolicitudDelComiteAsync();
        var (sesion, punto) = await SesionConPuntoAsync(LendingDecisionSubjects.CreditApplication, s.PublicId);
        await VotarAsync(sesion, punto, VoteValue.Against);

        (await CerrarAsync(sesion)).IsSuccess.Should().BeTrue();

        (await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId)).Status.Should().Be(CreditApplicationStatus.Denied);
    }

    [Fact]
    public async Task Si_la_revalidacion_falla_el_cierre_entero_se_revierte()
    {
        var s = await SolicitudDelComiteAsync();
        var (sesion, punto) = await SesionConPuntoAsync(LendingDecisionSubjects.CreditApplication, s.PublicId);
        await VotarAsync(sesion, punto, VoteValue.For);
        _o.PatrimonioTecnico = new IndicadorRegistradoDto(Guid.NewGuid(), 400_000_000m, "COP", new DateOnly(2026, 11, 30), "Contabilidad");

        var r = await CerrarAsync(sesion);

        r.Error.Code.Should().Be("Lending.Concentration.LimitExceeded", "40 + 3 millones pasan el límite de 40");
        (await _o.Db.LendingSessions.AsNoTracking().SingleAsync()).Status.Should().Be(SessionStatus.Open);
        (await _o.Db.CreditDecisions.AnyAsync()).Should().BeFalse();
        (await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId)).Status.Should().Be(CreditApplicationStatus.PendingDecision);
    }

    [Fact]
    public async Task La_devolucion_y_la_negacion_se_aplican_a_la_solicitud()
    {
        var s = await SolicitudDelComiteAsync();
        var t = await _o.RadicadaAsync(_o.Solicitud(2_000_000m, 24, _o.Dario));
        var asunto = AsuntoDeSolicitud();
        DecisionDeActa Decision(Guid sujeto, CreditDecisionOutcome resultado) =>
            new(new Session(), new SessionItem { SubjectType = LendingDecisionSubjects.CreditApplication, SubjectPublicId = sujeto }, new CreditDecision(),
                resultado, null, OriginacionDePrueba.Hoy);

        (await asunto.AlDecidirAsync(Decision(s.PublicId, CreditDecisionOutcome.Return), default)).IsSuccess.Should().BeTrue();
        (await asunto.AlDecidirAsync(Decision(t.PublicId, CreditDecisionOutcome.Deny), default)).IsSuccess.Should().BeTrue();
        await _o.Db.SaveChangesAsync();

        (await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId)).Status.Should().Be(CreditApplicationStatus.Returned);
        (await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == t.PublicId)).Status.Should().Be(CreditApplicationStatus.Denied);
        (await asunto.AlDecidirAsync(Decision(s.PublicId, CreditDecisionOutcome.Approve), default)).Error.Code
            .Should().Be("Lending.Session.SubjectNotPending");
    }

    [Fact]
    public async Task Aprobar_un_cupo_en_el_acta_lo_otorga_con_la_decision()
    {
        _o.MontoOperativo(_o.Asesora, 1_000_000m);
        _o.G.ActuarComo(_o.Asesora);
        var cupo = (await _o.Asignar().Handle(new AssignCreditLimitCommand(_o.Beto.PublicId, CreditLimitKind.Consolidated, null, CreditLimitOrigin.Assigned,
            60_000_000m, OriginacionDePrueba.Hoy, null, null, "Cupo empresarial") { OperationKey = Guid.NewGuid() }, default)).Value;
        _o.Db.ChangeTracker.Clear();
        var (sesion, punto) = await SesionConPuntoAsync(LendingDecisionSubjects.CreditLimit, cupo.PublicId);
        await VotarAsync(sesion, punto, VoteValue.For);

        var r = await CerrarAsync(sesion);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var decision = await _o.Db.CreditDecisions.AsNoTracking().SingleAsync();
        var otorgado = await _o.Db.CreditLimits.AsNoTracking().SingleAsync(c => c.DecisionId == decision.Id);
        otorgado.Amount.Should().Be(60_000_000m);
        otorgado.VersionNumber.Should().Be(2);
        (await ConsultasDeCupos.DtoAsync(_o.Db, otorgado, OriginacionDePrueba.Hoy, default)).Status.Should().Be(EstadosDeCupo.Activo);
    }
}
