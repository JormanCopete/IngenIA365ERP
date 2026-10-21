using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;

namespace IngenIA365ERP.Domain.Tests.Lending.Governance;

/// <summary>
/// Feature 013, T0283 (data-model §3.4, §3.12–§3.14; T63, T64, T66, T62): la decisión tiene exactamente un decisor
/// (<c>CK_LND_CreditDecisions_Decider</c>) y su huella; el vinculado nombra la persona del vínculo y no se cruza; la intervención es
/// un hecho con persona; el alcance de líneas se reemplaza dando de baja lo retirado.
/// </summary>
public class DecisionesYVinculadosTests
{
    private static readonly DateTime Ahora = new(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Solicitud = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static LoDecidido Aprobar() =>
        new(LendingDecisionSubjects.CreditApplication, Solicitud, Solicitud, CreditDecisionOutcome.Approve, 15_000_000m, new string('b', 64))
        { AttributionMatrixId = 2, LevelId = 20 };

    [Fact]
    public void La_decision_individual_lleva_usuario_y_persona_y_ningun_punto()
    {
        var d = CreditDecision.Individual(Aprobar(), 7, 70, Ahora);

        d.DecidedByUserId.Should().Be(7);
        d.DecidedByPersonId.Should().Be(70);
        d.SessionItemId.Should().BeNull();
        d.EsColegiada.Should().BeFalse();
        d.DecisionSha256.Should().Be(CreditDecision.Huella(Aprobar())).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Sin_persona_no_se_decide()
    {
        var decidir = () => CreditDecision.Individual(Aprobar(), 7, null, Ahora);
        var e = decidir.Should().Throw<ReglaDeCarteraException>().Which;
        e.Codigo.Should().Be("Lending.Security.UserWithoutPerson");
        e.Problema.Datos!.Should().ContainKey("howToFix");
    }

    [Fact]
    public void La_decision_colegiada_lleva_el_punto_y_ningun_usuario()
    {
        var punto = new SessionItem
        {
            Id = 9, Kind = SessionItemKind.Decision, SubjectType = LendingDecisionSubjects.CreditApplication, SubjectPublicId = Solicitud,
        };

        var d = CreditDecision.Colegiada(Aprobar(), punto, Ahora);

        d.SessionItemId.Should().Be(9);
        d.DecidedByUserId.Should().BeNull();
        d.DecidedByPersonId.Should().BeNull();
        d.EsColegiada.Should().BeTrue();

        var otroSujeto = new SessionItem { Id = 10, Kind = SessionItemKind.Decision, SubjectType = LendingDecisionSubjects.CreditLimit, SubjectPublicId = Solicitud };
        ((Action)(() => CreditDecision.Colegiada(Aprobar(), otroSujeto, Ahora))).Should().Throw<ArgumentException>();
        var recomendacion = new SessionItem { Id = 11, Kind = SessionItemKind.Recommendation, SubjectType = LendingDecisionSubjects.CreditApplication, SubjectPublicId = Solicitud };
        ((Action)(() => CreditDecision.Colegiada(Aprobar(), recomendacion, Ahora))).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(CreditDecisionOutcome.Deny, false)]
    [InlineData(CreditDecisionOutcome.Return, false)]
    [InlineData(CreditDecisionOutcome.Approve, true)]
    public void Negar_devolver_y_toda_excepcion_exigen_motivo(CreditDecisionOutcome resultado, bool excepcion)
    {
        var d = Aprobar() with { Outcome = resultado, IsPolicyException = excepcion };

        CreditDecision.Validar(d)!.Codigo.Should().Be("Validation.Invalid");
        CreditDecision.Validar(d with { Reason = "Capacidad de pago insuficiente" }).Should().BeNull();
        CreditDecision.Validar(Aprobar()).Should().BeNull("aprobar sin excepción no exige motivo");
    }

    [Fact]
    public void La_huella_cambia_si_cambia_el_monto_consolidado_o_el_enrutamiento()
    {
        var h = CreditDecision.Huella(Aprobar());
        CreditDecision.Huella(Aprobar() with { ConsolidatedAmount = 15_000_001m }).Should().NotBe(h);
        CreditDecision.Huella(Aprobar() with { RoutingSha256 = new string('c', 64) }).Should().NotBe(h);
    }

    // ---------------------------------------------------------------------------------------------------- vinculados --

    [Theory]
    [InlineData(RelatedPartyBasis.Spouse)]
    [InlineData(RelatedPartyBasis.Relative)]
    public void Conyuge_y_pariente_sin_la_persona_del_vinculo_es_LinkRequired(RelatedPartyBasis fundamento)
    {
        RelatedParty.Validar(new BorradorDeVinculado(70, fundamento, new(2026, 1, 1)), [])!.Codigo.Should().Be("Lending.RelatedParty.LinkRequired");
    }

    [Fact]
    public void El_pariente_lleva_clase_y_grado_y_los_demas_fundamentos_no_nombran_a_otra_persona()
    {
        var pariente = new BorradorDeVinculado(70, RelatedPartyBasis.Relative, new(2026, 1, 1)) { RelatedThroughPersonId = 80 };
        RelatedParty.Validar(pariente, [])!.Codigo.Should().Be("Lending.RelatedParty.LinkRequired");
        RelatedParty.Validar(pariente with { KinshipKind = KinshipKind.Consanguinity, KinshipDegree = 2 }, []).Should().BeNull();
        RelatedParty.Validar(new BorradorDeVinculado(70, RelatedPartyBasis.BoardMember, new(2026, 1, 1)) { RelatedThroughPersonId = 80 }, [])!
            .Codigo.Should().Be("Lending.RelatedParty.LinkRequired");
    }

    [Fact]
    public void El_mismo_vinculo_no_se_cruza_y_terminarlo_lo_cierra_con_motivo()
    {
        var miembro = RelatedParty.Registrar(new BorradorDeVinculado(70, RelatedPartyBasis.BoardMember, new(2026, 1, 1)), []);
        var otra = new BorradorDeVinculado(70, RelatedPartyBasis.BoardMember, new(2026, 6, 1));

        RelatedParty.Validar(otra, [miembro])!.Codigo.Should().Be("Lending.RelatedParty.Duplicate");
        RelatedParty.Validar(otra with { Basis = RelatedPartyBasis.LegalRepresentative }, [miembro]).Should().BeNull("otro fundamento es otro vínculo");

        miembro.Terminar(new(2026, 5, 31), 7, "Terminó su periodo en el consejo").Should().BeNull();
        miembro.EndedByUserId.Should().Be(7);
        miembro.VigenteEn(new(2026, 6, 1)).Should().BeFalse();
        RelatedParty.Validar(otra, [miembro]).Should().BeNull();
    }

    // ------------------------------------------------------------------------------------------------- intervenciones --

    [Fact]
    public void La_intervencion_exige_accion_conocida_usuario_y_persona()
    {
        var i = CaseParticipation.Registrar("CreditApplication", Solicitud, "CreditApplication", Solicitud, AccionesDeSegregacion.Radicar, 7, 70, Ahora);
        i.ComoPrevia().Should().Be(new IntervencionPrevia(Solicitud, "Radicar", 7, 70, Ahora));

        ((Action)(() => CaseParticipation.Registrar("CreditApplication", Solicitud, "CreditApplication", Solicitud, "Aprobar", 7, 70, Ahora)))
            .Should().Throw<ArgumentException>();
        ((Action)(() => CaseParticipation.Registrar("CreditApplication", Solicitud, "CreditApplication", Solicitud, "Decidir", 7, 0, Ahora)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    // ---------------------------------------------------------------------------------------------- alcance de líneas --

    [Fact]
    public void Reemplazar_el_alcance_de_lineas_da_de_baja_lo_retirado_y_admite_una_por_defecto()
    {
        var a = new UserCreditLineScope { Id = 1, UserId = 7, CreditLineId = 10, IsDefault = true };
        var b = new UserCreditLineScope { Id = 2, UserId = 7, CreditLineId = 11 };

        var r = UserCreditLineScope.Reemplazar(7, [a, b], [(11, true), (12, false)], Ahora, "ana");

        r.Problema.Should().BeNull();
        a.IsDeleted.Should().BeTrue();
        a.DeletedBy.Should().Be("ana");
        b.IsDefault.Should().BeTrue();
        r.Retiradas.Should().ContainSingle().Which.Should().BeSameAs(a);
        r.Agregadas.Should().ContainSingle().Which.CreditLineId.Should().Be(12);

        UserCreditLineScope.Reemplazar(7, [], [(11, true), (12, true)], Ahora, "ana").Problema!.Codigo.Should().Be("Lending.Scope.DefaultDuplicate");
    }
}
