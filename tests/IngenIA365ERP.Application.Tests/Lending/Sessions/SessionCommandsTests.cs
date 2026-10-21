using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.DecideApproval;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Sessions;

/// <summary>
/// Feature 013, T0248 (US17; T64, C75, F7, F8; api.md §5; data-model §3.8–§3.11): las sesiones y sus actas con un
/// <see cref="IAsuntoDeActa"/> falso por sujeto. La asistencia pasa <c>Draft → Open</c> y sella la regla vigente en <c>HeldOn</c>, la
/// composición, el quórum requerido y los presentes elegibles; un punto calcula su regla (<c>RelatedPartyQualified</c> con un vinculado en
/// entidad con actividad financiera) y sus excluidos (radicador, solicitante, persona del vínculo); un sujeto no pendiente o de otro órgano
/// se rechaza. Los votos: la secretaría registra el de cualquiera, <c>Sessions.Vote</c> sólo el propio, sin ninguno
/// <c>Lending.Permission.Required</c>; el excluido o no miembro, <c>VoterExcluded</c>; sesión no abierta o cerrada; el voto de reemplazo
/// marca <c>ReplacedAt</c> en el anterior y cuenta el vigente (C75); presencia con TOTP. El cierre: sin quórum no se cierra; con quórum
/// evalúa, escribe <c>LND_CreditDecisions</c>, numera, sella y avisa a cada asunto; si un asunto falla nada queda; una huella vieja es
/// <c>Lending.Decision.ContentChanged</c>.
/// </summary>
public sealed class SessionCommandsTests : IDisposable
{
    private static readonly DateOnly HeldOn = new(2026, 11, 30);

    private readonly GobiernoDePrueba _g = new();
    private readonly AsuntoFalso _solicitudes = new(LendingDecisionSubjects.CreditApplication);
    private readonly AsuntoFalso _cupos = new(LendingDecisionSubjects.CreditLimit);
    private readonly DesafiosEnMemoria _desafios = new();
    private readonly IMfaDirectory _mfa = Substitute.For<IMfaDirectory>();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly GoverningBody _comite;
    private readonly GoverningBody _consejo;
    private readonly Person _uno, _dos, _tres, _sara, _deudor;
    private readonly GoverningBodyMember _m1, _m2, _m3;
    private readonly User _secretaria, _usuarioDos, _usuarioUno, _sinPermisos;

    public SessionCommandsTests()
    {
        _comite = _g.Organo("COMITE", "Comité de crédito", GoverningBodyKind.CreditCommittee);
        _consejo = _g.Organo("CONSEJO", "Consejo de administración", GoverningBodyKind.BoardOfDirectors, vinculadosN: 4, vinculadosD: 5);
        _uno = _g.Persona("Uno", "Comité", "7001");
        _dos = _g.Persona("Dos", "Comité", "7002");
        _tres = _g.Persona("Tres", "Comité", "7003");
        _sara = _g.Persona("Sara", "Secretaria", "7004");
        _deudor = _g.Persona("Diego", "Deudor", "7005");
        _m1 = _g.Miembro(_comite, _uno, BodyMemberRole.Chair);
        _m2 = _g.Miembro(_comite, _dos);
        _m3 = _g.Miembro(_comite, _tres);
        _secretaria = _g.Usuario(_sara, "secretaria@coop.test");
        _usuarioDos = _g.Usuario(_dos, "comite.2@coop.test");
        _usuarioUno = _g.Usuario(_uno, "comite.1@coop.test");
        _sinPermisos = _g.Usuario(_g.Persona("Lola", "Lectura", "7006"), "lectura@coop.test");
        ComoSecretaria();

        _mfa.ListarCifradosTotpActivosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([new CredencialTotpCifrada(Guid.NewGuid(), "cifrado")]);
        _mfa.ListarWebAuthnActivasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<CredencialWebAuthnPermitida>());
        _identidad.VerifyMfaCodeAsync(Arg.Any<Guid>(), "123456", Arg.Any<CancellationToken>()).Returns(true);
    }

    public void Dispose() => _g.Dispose();

    private void ComoSecretaria()
    {
        _g.ActuarComo(_secretaria);
        _g.Conceder("Lending.Sessions.Read", "Lending.Sessions.Manage", "Lending.Sessions.RecordVotes", "Lending.Sessions.Close");
    }

    // ---------------------------------------------------------------------------------------------------------- armado --

    private AsuntosDeActa Asuntos() => new([_solicitudes, _cupos]);

    private PresenciaEnSesion Presencia() => new(_g.Db, _mfa, Substitute.For<IWebAuthnService>(), _identidad, _desafios, _g.Reloj);

    private async Task<SessionDto> Crear(GoverningBody? organo = null)
    {
        var r = await new CreateSessionCommandHandler(_g.Db, _g.Reloj).Handle(new CreateSessionCommand((organo ?? _comite).PublicId, SessionKind.Ordinary,
            SessionMode.InPerson, HeldOn) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private Task<Result<SessionDto>> Asistencia(SessionDto s, params (GoverningBodyMember Miembro, bool Presente)[] asistentes) =>
        new RecordSessionAttendanceCommandHandler(_g.Db, _g.Reloj).Handle(new RecordSessionAttendanceCommand(s.PublicId,
            asistentes.Select(a => new AttendeeInput(a.Miembro.PublicId, a.Presente)).ToList()) { OperationKey = Guid.NewGuid() }, default);

    private async Task<SessionDto> Abierta(params (GoverningBodyMember, bool)[] asistentes)
    {
        var s = await Crear();
        var r = await Asistencia(s, asistentes.Length > 0 ? asistentes : [(_m1, true), (_m2, true), (_m3, false)]);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private Guid Sujeto(AsuntoFalso asunto, bool pendiente = true, int? organo = null, params PersonaDelAsunto[] personas)
    {
        var id = Guid.NewGuid();
        asunto.Sujetos[id] = new AsuntoDeActa($"Solicitud {id:N}"[..20], "Resumen del análisis", pendiente, pendiente ? "Submitted" : "Approved",
            new ExpedienteDeCartera(asunto.SubjectType, id), new string('a', 64))
        {
            Personas = personas,
            GoverningBodyId = organo,
        };
        return id;
    }

    private Task<Result<SessionItemDto>> Punto(SessionDto s, string sujeto, Guid id, SessionItemKind clase = SessionItemKind.Decision) =>
        new AddSessionItemCommandHandler(_g.Db, Asuntos(), new VinculadosVigentes(_g.Db), new ReglasDeVinculados(_g.Valores, _g.Perfil), _g.Reloj)
            .Handle(new AddSessionItemCommand(s.PublicId, sujeto, id, clase) { OperationKey = Guid.NewGuid() }, default);

    private async Task<SessionItemDto> PuntoDeSolicitud(SessionDto s, params PersonaDelAsunto[] personas)
    {
        var r = await Punto(s, LendingDecisionSubjects.CreditApplication, Sujeto(_solicitudes, personas: personas));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private Task<Result<SessionVoteDto>> Votar(SessionDto s, SessionItemDto p, Person quien, VoteValue voto = VoteValue.For,
        VoteMethod metodo = VoteMethod.RecordedBySecretary, PresenciaDto? presencia = null, string? reemplazo = null) =>
        new RecordSessionVoteCommandHandler(_g.Db, Asuntos(), _g.Intervenciones(), Presencia(), _g.Permisos, _g.Actor, _g.Reloj)
            .Handle(new RecordSessionVoteCommand(s.PublicId, p.ItemPublicId, quien.PublicId, voto, metodo, presencia, reemplazo)
                { OperationKey = Guid.NewGuid() }, default);

    private async Task<string> Huella(SessionDto s) =>
        (await new GetSessionQueryHandler(_g.Db, Asuntos(), _g.Auditoria, _g.Reloj).Handle(new GetSessionQuery(s.PublicId), default)).Value.CurrentContentSha256!;

    private CloseSessionCommandHandler Cierre(IActaDeSesionEnPdf? pdf = null) =>
        new(_g.Db, Asuntos(), _g.Intervenciones(), new NumeradorDeCartera(_g.Db, Substitute.For<ICerrojoDeCartera>()), pdf ?? new SinActaEnPdf(),
            _g.Actor, _g.Reloj);

    private async Task<Result<SessionDto>> Cerrar(SessionDto s, string? huella = null) =>
        await Cierre().Handle(new CloseSessionCommand(s.PublicId, huella ?? await Huella(s)) { OperationKey = Guid.NewGuid() }, default);

    // ------------------------------------------------------------------------------------------------- asistencia --

    [Fact]
    public async Task La_asistencia_abre_la_sesion_y_sella_la_regla_la_composicion_y_el_quorum()
    {
        var s = await Crear();
        s.Status.Should().Be(SessionStatus.Draft);

        var r = await Asistencia(s, (_m1, true), (_m2, true), (_m3, false));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(SessionStatus.Open);
        r.Value.Quorum.Should().Be(new QuorumDto(Required: 2, Present: 2, Met: true, Composition: 3));
        var sesion = await _g.Db.LendingSessions.SingleAsync();
        sesion.RuleVersionId.Should().Be((await _g.Db.GoverningBodyRuleVersions.SingleAsync(v => v.GoverningBodyId == _comite.Id)).Id);
        r.Value.Attendees.Should().HaveCount(3);
    }

    [Fact]
    public async Task Un_miembro_de_otro_organo_no_registra_asistencia()
    {
        var ajeno = _g.Miembro(_consejo, _g.Persona("Otro", "Consejero", "7100"));
        var s = await Crear();

        var r = await Asistencia(s, (ajeno, true));

        r.Error.Code.Should().Be("Lending.GoverningBody.MemberNotFound");
    }

    // ------------------------------------------------------------------------------------------------------ puntos --

    [Fact]
    public async Task El_punto_calcula_la_regla_ordinaria_y_los_excluidos_radicador_y_solicitante()
    {
        var s = await Abierta();

        var p = await PuntoDeSolicitud(s, new PersonaDelAsunto(_sara.Id, MotivosDeExclusion.Radicador), new PersonaDelAsunto(_deudor.Id, MotivosDeExclusion.Solicitante));

        p.Rule.Should().Be(VotingRule.Majority);
        p.Excluded.Select(e => (e.Person.PersonPublicId, e.Reason)).Should().BeEquivalentTo(
            [(_sara.PublicId, "Filer"), (_deudor.PublicId, "Applicant")]);
        p.Order.Should().Be(1);
    }

    [Fact]
    public async Task Con_un_solicitante_vinculado_el_punto_del_consejo_va_por_la_calificada_y_excluye_a_la_persona_del_vinculo()
    {
        var consejera = _g.Persona("Carla", "Consejera", "7200");
        var mc = _g.Miembro(_consejo, consejera);
        _g.Db.RelatedParties.Add(new RelatedParty
        {
            PersonId = _deudor.Id, Basis = RelatedPartyBasis.Relative, RelatedThroughPersonId = consejera.Id, KinshipKind = KinshipKind.Consanguinity,
            KinshipDegree = 1, ValidFrom = new DateOnly(2026, 1, 1),
        });
        await _g.Db.SaveChangesAsync();
        var s = await Crear(_consejo);
        (await Asistencia(s, (mc, true))).IsSuccess.Should().BeTrue();

        var r = await Punto(s, LendingDecisionSubjects.CreditApplication,
            Sujeto(_solicitudes, organo: _consejo.Id, personas: new PersonaDelAsunto(_deudor.Id, MotivosDeExclusion.Solicitante)));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Rule.Should().Be(VotingRule.RelatedPartyQualified);
        r.Value.Excluded.Should().Contain(e => e.Person.PersonPublicId == consejera.PublicId && e.Reason == "Interested");
    }

    [Fact]
    public async Task Sin_actividad_financiera_el_vinculado_va_por_la_mayoria_de_los_estatutos()
    {
        _g.ConActividadFinanciera(false);
        _g.Db.RelatedParties.Add(new RelatedParty { PersonId = _deudor.Id, Basis = RelatedPartyBasis.BoardMember, ValidFrom = new DateOnly(2026, 1, 1) });
        await _g.Db.SaveChangesAsync();
        var s = await Abierta();

        var p = await PuntoDeSolicitud(s, new PersonaDelAsunto(_deudor.Id, MotivosDeExclusion.Solicitante));

        p.Rule.Should().Be(VotingRule.Majority);
    }

    [Fact]
    public async Task Un_sujeto_no_pendiente_es_SubjectNotPending_y_uno_de_otro_organo_SubjectNotForBody()
    {
        var s = await Abierta();

        var noPendiente = await Punto(s, LendingDecisionSubjects.CreditApplication, Sujeto(_solicitudes, pendiente: false));
        var deOtro = await Punto(s, LendingDecisionSubjects.CreditApplication, Sujeto(_solicitudes, organo: _consejo.Id));
        var sinAsunto = await Punto(s, LendingDecisionSubjects.Novelty, Guid.NewGuid());

        noPendiente.Error.Code.Should().Be("Lending.Session.SubjectNotPending");
        GobiernoDePrueba.Datos(noPendiente.Error).GetProperty("subjectStatus").GetString().Should().Be("Approved");
        deOtro.Error.Code.Should().Be("Lending.Session.SubjectNotForBody");
        sinAsunto.Error.Code.Should().Be("Lending.Session.SubjectNotSupported");
    }

    [Fact]
    public async Task Un_punto_con_votos_no_se_retira()
    {
        var s = await Abierta();
        var p = await PuntoDeSolicitud(s);
        (await Votar(s, p, _uno)).IsSuccess.Should().BeTrue();

        var r = await new RemoveSessionItemCommandHandler(_g.Db, _g.Actor, _g.Reloj)
            .Handle(new RemoveSessionItemCommand(s.PublicId, p.ItemPublicId, "Se lleva a otra sesión") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.Session.ItemHasVotes");
    }

    // ------------------------------------------------------------------------------------------------------- votos --

    [Fact]
    public async Task La_secretaria_registra_el_voto_de_cualquier_miembro()
    {
        var s = await Abierta();
        var p = await PuntoDeSolicitud(s);

        var r = await Votar(s, p, _uno);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Method.Should().Be(VoteMethod.RecordedBySecretary);
        r.Value.Person.PersonPublicId.Should().Be(_uno.PublicId);
        r.Value.RecordedBy!.UserPublicId.Should().Be(_secretaria.PublicId);
        (await _g.Db.CaseParticipations.SingleAsync()).Action.Should().Be(AccionesDeSegregacion.Votar, "el voto escribe la intervención del miembro");
    }

    [Fact]
    public async Task Con_Sessions_Vote_solo_el_propio_y_sin_ninguno_Permission_Required()
    {
        var s = await Abierta();
        var p = await PuntoDeSolicitud(s);
        _g.ActuarComo(_usuarioDos);
        _g.Conceder("Lending.Sessions.Read", "Lending.Sessions.Vote");

        var propio = await Votar(s, p, _dos, metodo: VoteMethod.OwnSession);
        var ajeno = await Votar(s, p, _uno, metodo: VoteMethod.OwnSession);
        var deSecretaria = await Votar(s, p, _uno);
        _g.ActuarComo(_sinPermisos);
        _g.Conceder("Lending.Sessions.Read");
        var sinNinguno = await Votar(s, p, _uno, metodo: VoteMethod.OwnSession);

        propio.IsSuccess.Should().BeTrue(propio.IsFailure ? propio.Error.Message : string.Empty);
        ajeno.Error.Code.Should().Be("Lending.Permission.Required");
        GobiernoDePrueba.Datos(ajeno.Error).GetProperty("permissionCodes").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("Lending.Sessions.RecordVotes");
        deSecretaria.Error.Code.Should().Be("Lending.Permission.Required");
        GobiernoDePrueba.Datos(sinNinguno.Error).GetProperty("permissionCodes").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("Lending.Sessions.RecordVotes", "Lending.Sessions.Vote");
    }

    [Fact]
    public async Task El_no_miembro_el_ausente_y_el_excluido_no_votan()
    {
        var s = await Abierta();
        var p = await PuntoDeSolicitud(s, new PersonaDelAsunto(_dos.Id, MotivosDeExclusion.Solicitante));

        var noMiembro = await Votar(s, p, _deudor);
        var ausente = await Votar(s, p, _tres);
        var excluido = await Votar(s, p, _dos);

        Motivo(noMiembro.Error).Should().Be("NotMember");
        Motivo(ausente.Error).Should().Be("Absent");
        Motivo(excluido.Error).Should().Be("Applicant");
        new[] { noMiembro, ausente, excluido }.Should().OnlyContain(x => x.Error.Code == "Lending.Session.VoterExcluded");

        static string Motivo(Error e) => GobiernoDePrueba.Datos(e).GetProperty("reason").GetString()!;
    }

    [Fact]
    public async Task En_borrador_no_se_vota_y_cerrada_tampoco()
    {
        var borrador = await Crear();
        var item = SessionItem.Agregar(await _g.Db.LendingSessions.SingleAsync(x => x.PublicId == borrador.PublicId), 1, SessionItemKind.Decision,
            VotingRule.Majority, LendingDecisionSubjects.CreditApplication, Sujeto(_solicitudes), "Punto", []);
        _g.Db.SessionItems.Add(item);
        await _g.Db.SaveChangesAsync();
        var enBorrador = await new RecordSessionVoteCommandHandler(_g.Db, Asuntos(), _g.Intervenciones(), Presencia(), _g.Permisos, _g.Actor, _g.Reloj)
            .Handle(new RecordSessionVoteCommand(borrador.PublicId, item.PublicId, _uno.PublicId, VoteValue.For, VoteMethod.RecordedBySecretary)
                { OperationKey = Guid.NewGuid() }, default);
        enBorrador.Error.Code.Should().Be("Lending.Session.NotOpen");

        var s = await Abierta();
        var p = await PuntoDeSolicitud(s);
        await Votar(s, p, _uno);
        await Votar(s, p, _dos);
        (await Cerrar(s)).IsSuccess.Should().BeTrue();

        (await Votar(s, p, _uno, VoteValue.Against, reemplazo: "Se equivocó")).Error.Code.Should().Be("Lending.Session.Closed");
    }

    [Fact]
    public async Task El_voto_de_reemplazo_marca_ReplacedAt_en_el_anterior_y_cuenta_el_vigente()
    {
        var s = await Abierta();
        var p = await PuntoDeSolicitud(s);
        var primero = await Votar(s, p, _uno, VoteValue.Against);
        var duplicado = await Votar(s, p, _uno, VoteValue.For);
        _g.UtcNow = GobiernoDePrueba.Ahora.AddMinutes(5);

        var reemplazo = await Votar(s, p, _uno, VoteValue.For, reemplazo: "La secretaría digitó mal el voto");
        await Votar(s, p, _dos);
        var cerrada = await Cerrar(s);

        duplicado.Error.Code.Should().Be("Lending.Session.VoteDuplicate");
        reemplazo.IsSuccess.Should().BeTrue(reemplazo.IsFailure ? reemplazo.Error.Message : string.Empty);
        reemplazo.Value.ReplacesVotePublicId.Should().Be(primero.Value.VotePublicId);
        var anterior = await _g.Db.SessionVotes.SingleAsync(v => v.PublicId == primero.Value.VotePublicId);
        anterior.ReplacedAt.Should().Be(GobiernoDePrueba.Ahora.AddMinutes(5));
        cerrada.Value.Items.Single().Outcome.Should().Be(CreditDecisionOutcome.Approve, "cuenta el voto vigente, a favor");
        cerrada.Value.Items.Single().Tally!.For.Should().Be(2);
    }

    [Fact]
    public async Task Presente_con_TOTP_vota_con_su_desafio_y_el_mismo_codigo_no_se_reusa()
    {
        var s = await Abierta();
        var p = await PuntoDeSolicitud(s);
        var presencia = new CreateSessionPresenceChallengeCommandHandler(_g.Db, Presencia(), _g.Actor);

        var desafio = await presencia.Handle(new CreateSessionPresenceChallengeCommand(s.PublicId, _uno.PublicId) { OperationKey = Guid.NewGuid() }, default);
        var sinUsuario = await presencia.Handle(new CreateSessionPresenceChallengeCommand(s.PublicId, _tres.PublicId) { OperationKey = Guid.NewGuid() }, default);
        var voto = await Votar(s, p, _uno, metodo: VoteMethod.InPersonTotp, presencia: new PresenciaDto(desafio.Value.ChallengePublicId, null, "123456"));
        var otro = await presencia.Handle(new CreateSessionPresenceChallengeCommand(s.PublicId, _dos.PublicId) { OperationKey = Guid.NewGuid() }, default);
        _desafios.TotpsUsados.Add("123456");
        var reusado = await Votar(s, p, _dos, metodo: VoteMethod.InPersonTotp, presencia: new PresenciaDto(otro.Value.ChallengePublicId, null, "123456"));
        var vencido = await Votar(s, p, _dos, metodo: VoteMethod.InPersonTotp, presencia: new PresenciaDto(Guid.NewGuid(), null, "123456"));

        desafio.IsSuccess.Should().BeTrue(desafio.IsFailure ? desafio.Error.Message : string.Empty);
        desafio.Value.Methods.Should().Equal("Totp");
        sinUsuario.Error.Code.Should().Be("Lending.Session.MemberNotUser");
        voto.IsSuccess.Should().BeTrue(voto.IsFailure ? voto.Error.Message : string.Empty);
        voto.Value.Method.Should().Be(VoteMethod.InPersonTotp);
        (await _g.Db.SessionVotes.SingleAsync()).PresenceChallengePublicId.Should().Be(desafio.Value.ChallengePublicId);
        reusado.Error.Code.Should().Be("Approvals.Presence.TotpReused");
        vencido.Error.Code.Should().Be("Approvals.Presence.Expired");
    }

    [Fact]
    public async Task Sin_quorum_no_se_vota()
    {
        var s = await Abierta((_m1, true), (_m2, false), (_m3, false));
        var p = await PuntoDeSolicitud(s);

        (await Votar(s, p, _uno)).Error.Code.Should().Be("Lending.Session.QuorumNotMet");
    }

    // ------------------------------------------------------------------------------------------------------ cierre --

    [Fact]
    public async Task Sin_quorum_no_se_cierra()
    {
        var s = await Abierta((_m1, true), (_m2, false), (_m3, false));

        var r = await Cerrar(s, new string('0', 64));

        r.Error.Code.Should().Be("Lending.Session.QuorumNotMet");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("required").GetInt32().Should().Be(2);
        datos.GetProperty("present").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Con_quorum_evalua_escribe_las_decisiones_numera_sella_y_avisa_a_cada_asunto()
    {
        var s = await Abierta();
        var decide = await PuntoDeSolicitud(s);
        var negado = await PuntoDeSolicitud(s);
        var recomienda = await Punto(s, LendingDecisionSubjects.CreditLimit, Sujeto(_cupos), SessionItemKind.Recommendation);
        await Votar(s, decide, _uno);
        await Votar(s, decide, _dos);
        await Votar(s, negado, _uno, VoteValue.Against);
        await Votar(s, negado, _dos, VoteValue.Abstain);

        var r = await Cerrar(s);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(SessionStatus.Closed);
        r.Value.Number.Should().Be("COMITE-000001");
        r.Value.ContentSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        r.Value.Items.Single(i => i.ItemPublicId == decide.ItemPublicId).Outcome.Should().Be(CreditDecisionOutcome.Approve);
        r.Value.Items.Single(i => i.ItemPublicId == negado.ItemPublicId).Outcome.Should().Be(CreditDecisionOutcome.Deny);
        r.Value.Items.Single(i => i.ItemPublicId == recomienda.Value.ItemPublicId).DecisionPublicId.Should().BeNull("un punto que recomienda no decide");

        var decisiones = await _g.Db.CreditDecisions.ToListAsync();
        decisiones.Should().HaveCount(2).And.OnlyContain(d => d.SessionItemId != null && d.DecidedByUserId == null);
        decisiones.Single(d => d.Outcome == CreditDecisionOutcome.Deny).Reason.Should().NotBeNullOrWhiteSpace();
        _solicitudes.Decididos.Should().HaveCount(2);
        _cupos.Decididos.Should().BeEmpty();
        (await _g.Db.CaseParticipations.Where(c => c.Action == AccionesDeSegregacion.Decidir).CountAsync()).Should().Be(2);
        (await _g.Db.LendingNumberSequences.SingleAsync()).ScopeKey.Should().Be(NumeradorDeCartera.AmbitoDeOrgano(_comite));
    }

    [Fact]
    public async Task Si_un_asunto_falla_al_decidir_nada_queda()
    {
        var s = await Abierta();
        var p1 = await PuntoDeSolicitud(s);
        var p2 = await Punto(s, LendingDecisionSubjects.CreditLimit, Sujeto(_cupos));
        foreach (var p in new[] { p1, p2.Value })
        {
            await Votar(s, p, _uno);
            await Votar(s, p, _dos);
        }
        _cupos.FallarAlDecidir = new Error("Lending.Concentration.LimitExceeded", "El cupo llevaría el endeudamiento por encima del límite.");
        var huella = await Huella(s);
        _g.Db.ChangeTracker.Clear();

        var r = await Cerrar(s, huella);

        r.Error.Code.Should().Be("Lending.Concentration.LimitExceeded");
        _g.Db.ChangeTracker.Clear();
        (await _g.Db.CreditDecisions.CountAsync()).Should().Be(0);
        var sesion = await _g.Db.LendingSessions.SingleAsync();
        sesion.Status.Should().Be(SessionStatus.Open);
        sesion.Number.Should().BeNull();
        (await _g.Db.SessionItems.ToListAsync()).Should().OnlyContain(i => i.VotesFor == null && i.DecisionId == null);
        (await _g.Db.LendingNumberSequences.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_huella_vieja_es_ContentChanged()
    {
        var s = await Abierta();
        var p = await PuntoDeSolicitud(s);
        await Votar(s, p, _uno);
        var vista = await Huella(s);
        await Votar(s, p, _dos);

        var porVoto = await Cerrar(s, vista);
        var antesDelSujeto = await Huella(s);
        _solicitudes.Sujetos[p.SubjectPublicId] = _solicitudes.Sujetos[p.SubjectPublicId] with { ContentSha256 = new string('b', 64) };
        var porSujeto = await Cerrar(s, antesDelSujeto);

        porVoto.Error.Code.Should().Be("Lending.Decision.ContentChanged");
        porSujeto.Error.Code.Should().Be("Lending.Decision.ContentChanged", "un asunto que cambió desde que se revisó el acta no se decide");
        GobiernoDePrueba.Datos(porSujeto.Error).GetProperty("currentContentSha256").GetString().Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public async Task Quien_radico_el_asunto_no_cierra_el_acta_que_lo_decide()
    {
        var s = await Abierta();
        var id = Sujeto(_solicitudes);
        var p = (await Punto(s, LendingDecisionSubjects.CreditApplication, id)).Value;
        await Votar(s, p, _uno);
        await Votar(s, p, _dos);
        _g.Db.CaseParticipations.Add(CaseParticipation.Registrar(LendingDecisionSubjects.CreditApplication, id, LendingDecisionSubjects.CreditApplication, id,
            AccionesDeSegregacion.Radicar, _secretaria.Id, _sara.Id, GobiernoDePrueba.Ahora.AddDays(-2)));
        await _g.Db.SaveChangesAsync();

        var r = await Cerrar(s);

        r.Error.Code.Should().Be("Lending.Segregation.Conflict");
        (await _g.Db.CreditDecisions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Leer_el_acta_deja_Lending_Session_Viewed()
    {
        var s = await Abierta();

        await Huella(s);

        (await _g.EventosAsync()).Should().Contain(IngenIA365ERP.Application.Common.Audit.AuditEventTypes.LendingSessionViewed);
    }

    // ------------------------------------------------------------------------------------------------------ dobles --

    /// <summary>Un asunto de acta falso: sus sujetos en memoria, las decisiones que recibió y una falla a pedido.</summary>
    internal sealed class AsuntoFalso(string sujeto) : IAsuntoDeActa
    {
        public string SubjectType { get; } = sujeto;

        public Dictionary<Guid, AsuntoDeActa> Sujetos { get; } = [];

        public List<DecisionDeActa> Decididos { get; } = [];

        public Error? FallarAlDecidir { get; set; }

        public Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct) =>
            Task.FromResult(Sujetos.TryGetValue(subjectPublicId, out var a)
                ? Result.Success(a)
                : Result.Failure<AsuntoDeActa>(new Error("Lending.Application.NotFound", "La solicitud no existe.")));

        public Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct)
        {
            if (FallarAlDecidir is { } error) return Task.FromResult(Result.Failure(error));
            Decididos.Add(decision);
            return Task.FromResult(Result.Success());
        }
    }

    /// <summary>Los desafíos de presencia en memoria (un solo intento por desafío; los TOTP usados no se repiten).</summary>
    private sealed class DesafiosEnMemoria : IDesafiosDePresencia
    {
        private readonly Dictionary<Guid, DesafioDePresencia> _desafios = [];

        public HashSet<string> TotpsUsados { get; } = [];

        public Task GuardarAsync(DesafioDePresencia desafio, TimeSpan vigencia, CancellationToken ct)
        {
            _desafios[desafio.PublicId] = desafio;
            return Task.CompletedTask;
        }

        public Task<DesafioDePresencia?> ConsumirAsync(Guid publicId, CancellationToken ct) =>
            Task.FromResult(_desafios.Remove(publicId, out var d) ? d : null);

        public Task<bool> MarcarTotpUsadoAsync(Guid centralUserId, string codigo, TimeSpan vigencia, CancellationToken ct) =>
            Task.FromResult(TotpsUsados.Add(codigo));
    }
}
