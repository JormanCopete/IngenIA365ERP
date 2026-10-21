using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Sessions;

namespace IngenIA365ERP.Domain.Tests.Lending.Governance;

/// <summary>
/// Feature 013, T0281 y T0282 (data-model §3.5–§3.11; T64, F7, C75): órganos con su regla como hecho y miembros con vigencia sin cruces
/// (el suplente atado a su principal); sesiones <c>Draft → Open → Closed</c> con la regla y el quórum sellados al abrir, puntos con
/// regla y excluidos, voto de reemplazo con <c>ReplacedAt</c>, y nada editable tras cerrar (<c>Lending.Session.Closed</c>).
/// </summary>
public class OrganosYSesionesTests
{
    private static readonly DateOnly Dia = new(2026, 10, 8);
    private static readonly DateTime Ahora = new(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);

    private static BorradorDeReglasDeOrgano Reglas(int? vinculadosN = 4, int? vinculadosD = 5) =>
        new(Dia, 1, 2, FractionComparison.MoreThan, 1, 2, FractionComparison.MoreThan, MajorityBase.PresentEligible,
            vinculadosN, vinculadosD, "Estatutos, art. 40", "Semilla F7");

    // ------------------------------------------------------------------------------------------------ órganos (T0281) --

    [Fact]
    public void La_regla_del_consejo_sella_las_fracciones_y_compara_vinculados_con_el_limite()
    {
        var regla = GoverningBodyRuleVersion.Registrar(1, GoverningBodyKind.BoardOfDirectors, 1, Reglas(), Ahora);

        var sellada = regla.ReglaSellada();
        sellada.Quorum.Should().Be(new Fraccion(1, 2));
        sellada.Vinculados.Should().Be(new Fraccion(4, 5));
        regla.EsMenosEstrictaQue(0.8m).Should().BeFalse("4/5 es igual al piso");
        GoverningBodyRuleVersion.Registrar(1, GoverningBodyKind.BoardOfDirectors, 2, Reglas(3, 4), Ahora).EsMenosEstrictaQue(0.8m)
            .Should().BeTrue("3/4 queda bajo 4/5");
        GoverningBodyRuleVersion.Registrar(1, GoverningBodyKind.BoardOfDirectors, 3, Reglas(5, 6), Ahora).EsMenosEstrictaQue(0.8m)
            .Should().BeFalse("la cooperativa sólo endurece");
    }

    [Theory]
    [InlineData(0, 2, "QuorumFraction")]
    [InlineData(3, 2, "QuorumFraction")]
    public void Una_fraccion_fuera_de_0_y_1_es_RulesInvalid(int n, int d, string motivo)
    {
        var p = GoverningBodyRuleVersion.Validar(Reglas() with { QuorumNumerator = n, QuorumDenominator = d }, GoverningBodyKind.BoardOfDirectors);
        p!.Codigo.Should().Be("Lending.GoverningBody.RulesInvalid");
        p.Datos!["reason"].Should().Be(motivo);
    }

    [Fact]
    public void Solo_el_consejo_lleva_la_fraccion_de_vinculados_y_completa()
    {
        GoverningBodyRuleVersion.Validar(Reglas(), GoverningBodyKind.CreditCommittee)!.Datos!["reason"].Should().Be("RelatedPartyFractionNotBoard");
        GoverningBodyRuleVersion.Validar(Reglas(4, null), GoverningBodyKind.BoardOfDirectors)!.Datos!["reason"]
            .Should().Be("RelatedPartyFractionIncomplete");
        GoverningBodyRuleVersion.Validar(Reglas(null, null), GoverningBodyKind.CreditCommittee).Should().BeNull();
    }

    [Fact]
    public void Un_segundo_consejo_activo_se_rechaza_y_desactivar_no_borra()
    {
        var actual = new GoverningBody { Code = "CONSEJO", Name = "Consejo", Kind = GoverningBodyKind.BoardOfDirectors, IsActive = true };
        var nuevo = new GoverningBody { Code = "CONSEJO2", Name = "Otro consejo", Kind = GoverningBodyKind.BoardOfDirectors, IsActive = true };

        nuevo.ValidarConsejoUnico([actual])!.Datos!["existingName"].Should().Be("Consejo");
        actual.Desactivar();
        nuevo.ValidarConsejoUnico([actual]).Should().BeNull();
        actual.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Una_persona_no_tiene_dos_vigencias_cruzadas_en_el_mismo_organo()
    {
        var vigente = new GoverningBodyMember { Id = 1, GoverningBodyId = 3, PersonId = 70, Role = BodyMemberRole.Member, HasVote = true, IsPrincipal = true, ValidFrom = new(2026, 1, 1) };

        var otra = new BorradorDeMiembro(3, 70, BodyMemberRole.Chair, true, true, null, new(2026, 6, 1));
        GoverningBodyMember.Validar(otra, [vigente], null)!.Codigo.Should().Be("Lending.GoverningBody.MemberOverlaps");

        vigente.Terminar(new(2026, 5, 31)).Should().BeNull();
        GoverningBodyMember.Validar(otra, [vigente], null).Should().BeNull("la vigencia anterior terminó la víspera");
        GoverningBodyMember.Validar(otra with { GoverningBodyId = 4 }, [vigente], null).Should().BeNull("en otro órgano no se cruza");
    }

    [Fact]
    public void El_suplente_esta_atado_a_un_principal_vigente_del_mismo_organo()
    {
        var principal = new GoverningBodyMember { Id = 1, GoverningBodyId = 3, PersonId = 70, Role = BodyMemberRole.Member, HasVote = true, IsPrincipal = true, ValidFrom = new(2026, 1, 1) };
        var suplente = new BorradorDeMiembro(3, 71, BodyMemberRole.Alternate, true, false, 1, new(2026, 2, 1));

        GoverningBodyMember.Validar(suplente, [principal], principal).Should().BeNull();
        var nombrado = GoverningBodyMember.Nombrar(suplente, [principal], principal);
        nombrado.ComoMiembro(principal.PersonId).Should().Be(new MiembroDelOrgano(71, false, true, 70));

        GoverningBodyMember.Validar(suplente with { IsPrincipal = true }, [principal], principal)!.Codigo.Should().Be("Lending.GoverningBody.AlternateInvalid");
        var deOtroOrgano = new GoverningBodyMember { Id = 1, GoverningBodyId = 9, PersonId = 70, Role = BodyMemberRole.Member, HasVote = true, IsPrincipal = true, ValidFrom = new(2026, 1, 1) };
        GoverningBodyMember.Validar(suplente, [principal], deOtroOrgano)!.Codigo.Should().Be("Lending.GoverningBody.AlternateInvalid");
        GoverningBodyMember.Validar(suplente with { ValidFrom = new(2025, 12, 1) }, [principal], principal)!.Codigo
            .Should().Be("Lending.GoverningBody.AlternateInvalid");
        GoverningBodyMember.Validar(suplente with { Role = BodyMemberRole.Member }, [principal], principal)!.Codigo
            .Should().Be("Lending.GoverningBody.AlternateInvalid");
    }

    // ----------------------------------------------------------------------------------------------- sesiones (T0282) --

    private static (Session Sesion, GoverningBodyRuleVersion Regla) SesionAbierta(bool conQuorum = true)
    {
        var regla = GoverningBodyRuleVersion.Registrar(3, GoverningBodyKind.CreditCommittee, 1, Reglas(null, null), Ahora);
        regla.Id = 12;
        var sesion = Session.Crear(3, SessionKind.Ordinary, SessionMode.InPerson, Dia, "Sala 1");
        sesion.Id = 5;
        sesion.Abrir(regla, new QuorumDeSesion(5, conQuorum ? 3 : 2, 3, conQuorum), Ahora);
        return (sesion, regla);
    }

    [Fact]
    public void Registrar_la_asistencia_abre_la_sesion_y_sella_regla_y_quorum()
    {
        var (sesion, regla) = SesionAbierta();

        sesion.Status.Should().Be(SessionStatus.Open);
        sesion.RuleVersionId.Should().Be(regla.Id);
        sesion.CompositionCount.Should().Be(5);
        sesion.QuorumRequired.Should().Be(3);
        sesion.PresentEligibleCount.Should().Be(3);
        sesion.QuorumMet.Should().BeTrue();
        sesion.StartedAt.Should().Be(Ahora);

        sesion.Abrir(regla, new QuorumDeSesion(5, 4, 3, true), Ahora.AddMinutes(5));
        sesion.PresentEligibleCount.Should().Be(4, "abierta, la asistencia vuelve a sellar el quórum");
        sesion.StartedAt.Should().Be(Ahora);
    }

    [Fact]
    public void La_regla_sellada_es_la_del_mismo_organo_vigente_en_la_fecha()
    {
        var ajena = GoverningBodyRuleVersion.Registrar(9, GoverningBodyKind.CreditCommittee, 1, Reglas(null, null), Ahora);
        ajena.Id = 30;
        var sesion = Session.Crear(3, SessionKind.Ordinary, SessionMode.Virtual, Dia);

        var abrir = () => sesion.Abrir(ajena, new QuorumDeSesion(5, 3, 3, true), Ahora);
        abrir.Should().Throw<ReglaDeCarteraException>();
        sesion.Status.Should().Be(SessionStatus.Draft);
    }

    [Fact]
    public void Sin_quorum_no_se_cierra_y_cerrada_nada_cambia()
    {
        var (sinQuorum, _) = SesionAbierta(conQuorum: false);
        var p = sinQuorum.PuedeCerrar(new QuorumDeSesion(5, 2, 3, false));
        p!.Codigo.Should().Be("Lending.Session.QuorumNotMet");
        p.Datos!["required"].Should().Be(3);
        p.Datos["present"].Should().Be(2);

        var (sesion, regla) = SesionAbierta();
        sesion.Cerrar(new QuorumDeSesion(5, 3, 3, true), "CC-0001", Ahora.AddHours(2), 7, new string('a', 64), Guid.NewGuid());
        sesion.Status.Should().Be(SessionStatus.Closed);
        sesion.Number.Should().Be("CC-0001");

        sesion.ExigirEditable()!.Codigo.Should().Be("Lending.Session.Closed");
        sesion.ExigirAbierta()!.Codigo.Should().Be("Lending.Session.Closed");
        ((Action)(() => sesion.EscribirNotas("otra cosa"))).Should().Throw<ReglaDeCarteraException>().Which.Codigo.Should().Be("Lending.Session.Closed");
        ((Action)(() => sesion.Abrir(regla, new QuorumDeSesion(5, 3, 3, true), Ahora))).Should().Throw<ReglaDeCarteraException>();
        ((Action)(() => SessionAttendance.Registrar(sesion, 80, null, AttendanceStatus.Present))).Should().Throw<ReglaDeCarteraException>();
        ((Action)(() => SessionItem.Agregar(sesion, 2, SessionItemKind.Decision, VotingRule.Majority, LendingDecisionSubjects.CreditLimit,
            Guid.NewGuid(), "Cupo", []))).Should().Throw<ReglaDeCarteraException>();
    }

    [Fact]
    public void En_borrador_no_se_vota()
    {
        var sesion = Session.Crear(3, SessionKind.Extraordinary, SessionMode.Mixed, Dia);
        sesion.Id = 5;
        var punto = SessionItem.Agregar(sesion, 1, SessionItemKind.Decision, VotingRule.Majority, LendingDecisionSubjects.CreditApplication,
            Guid.NewGuid(), "Solicitud 15", []);
        punto.SessionId.Should().Be(5);

        SessionVote.PuedeRegistrar(sesion, punto, 70, [])!.Codigo.Should().Be("Lending.Session.NotOpen");
    }

    [Fact]
    public void El_punto_guarda_su_regla_y_sus_excluidos_y_sella_el_resultado_al_cerrar()
    {
        var (sesion, _) = SesionAbierta();
        var radicador = Guid.NewGuid();
        var punto = SessionItem.Agregar(sesion, 1, SessionItemKind.Decision, VotingRule.RelatedPartyQualified,
            LendingDecisionSubjects.CreditApplication, Guid.NewGuid(), "Solicitud 15", [new(radicador, "Filer"), new(radicador, "Applicant")]);
        punto.Id = 9;

        punto.Rule.Should().Be(VotingRule.RelatedPartyQualified);
        punto.Excluidos().Should().Equal(new ExclusionDePunto(radicador, "Filer"));

        var evaluacion = new EvaluacionDeVotacion(new QuorumDeSesion(5, 3, 3, true), 3, 3, 2, 2, 1, 0, ResultadoDeVotacion.Aprobado, [], ["quórum 3 de 5"]);
        punto.SellarResultado(sesion, evaluacion, CreditDecisionOutcome.ApproveWithConditions, "{\"amount\":1000}");
        punto.AsignarDecision(sesion, 44);

        punto.Outcome.Should().Be(CreditDecisionOutcome.ApproveWithConditions);
        punto.VotesFor.Should().Be(2);
        punto.VotesAgainst.Should().Be(1);
        punto.Conditions.Should().Be("{\"amount\":1000}");
        punto.EvaluationExplanation.Should().Contain("quórum");
        punto.DecisionId.Should().Be(44);

        var noAprobado = evaluacion with { Resultado = ResultadoDeVotacion.NoAprobado };
        punto.SellarResultado(sesion, noAprobado);
        punto.Outcome.Should().Be(CreditDecisionOutcome.Deny);
    }

    [Fact]
    public void Un_voto_vigente_por_persona_y_el_reemplazo_marca_ReplacedAt_en_el_anterior()
    {
        var (sesion, _) = SesionAbierta();
        var punto = SessionItem.Agregar(sesion, 1, SessionItemKind.Decision, VotingRule.Majority, LendingDecisionSubjects.CreditApplication,
            Guid.NewGuid(), "Solicitud 15", []);
        punto.Id = 9;

        var voto = SessionVote.Registrar(sesion, punto, 70, 1, VoteValue.Against, VoteMethod.RecordedBySecretary, 7, Ahora, []);
        voto.Id = 100;
        SessionVote.PuedeRegistrar(sesion, punto, 70, [voto])!.Codigo.Should().Be("Lending.Session.VoteDuplicate");

        var reemplazo = SessionVote.Reemplazar(sesion, voto, VoteValue.For, "La secretaría marcó mal el voto", VoteMethod.RecordedBySecretary, 7, Ahora.AddMinutes(1));

        voto.ReplacedAt.Should().Be(Ahora.AddMinutes(1));
        voto.EsVigente.Should().BeFalse();
        reemplazo.ReplacesVoteId.Should().Be(100);
        reemplazo.PersonId.Should().Be(70);
        reemplazo.ComoVoto().Should().Be(new VotoRegistrado(0, 70, VoteValue.For, 100));
        SessionVote.PuedeRegistrar(sesion, punto, 70, [voto]).Should().BeNull("el reemplazado ya no cuenta como voto vigente");

        reemplazo.Id = 101;
        var sinMotivo = () => SessionVote.Reemplazar(sesion, reemplazo, VoteValue.Abstain, " ", VoteMethod.RecordedBySecretary, 7, Ahora);
        sinMotivo.Should().Throw<ArgumentException>();
        var deNuevo = () => SessionVote.Reemplazar(sesion, voto, VoteValue.Abstain, "otra vez", VoteMethod.RecordedBySecretary, 7, Ahora);
        deNuevo.Should().Throw<InvalidOperationException>("un voto ya reemplazado no se vuelve a reemplazar");
    }

    [Fact]
    public void Un_voto_presencial_lleva_su_desafio()
    {
        var (sesion, _) = SesionAbierta();
        var punto = SessionItem.Agregar(sesion, 1, SessionItemKind.Decision, VotingRule.Majority, LendingDecisionSubjects.CreditApplication,
            Guid.NewGuid(), "Solicitud 15", []);
        punto.Id = 9;

        var sinDesafio = () => SessionVote.Registrar(sesion, punto, 70, 1, VoteValue.For, VoteMethod.InPersonPasskey, 7, Ahora, []);
        sinDesafio.Should().Throw<ArgumentException>();
        SessionVote.Registrar(sesion, punto, 70, 1, VoteValue.For, VoteMethod.InPersonTotp, 7, Ahora, [], Guid.NewGuid())
            .PresenceChallengePublicId.Should().NotBeNull();
    }
}
