using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0372 (api.md §4.3, T63, FR-038, FR-042): la decisión individual. Sin el permiso del nivel efectivo, el 404 de la solicitud; nivel
/// colegiado o menor que el requerido → <c>Lending.Attribution.LevelRequired</c>; <c>Deny</c>, <c>Return</c> y toda excepción sin motivo →
/// <c>Validation.Invalid</c>; lo no exceptuable → <c>Lending.Attribution.NotExceptionable</c> con <c>reasons[]</c>; la huella vista distinta →
/// <c>Lending.Decision.ContentChanged</c> y se reenruta; no pendiente → <c>Lending.Decision.NotPending</c>; quien radicó →
/// <c>Lending.Segregation.Conflict</c>; el maestro (sin <c>Lending.Decisions.*</c>, C86) no decide. Aprobar deja <c>Approved</c> con su vigencia,
/// condiciones menores revalidadas contra la concentración, el cerrojo del deudor, la decisión con su huella y la intervención <c>Decidir</c>;
/// devolver deja <c>Returned</c>; la aprobación vencida se decide de nuevo (C53); el cupo usa el mismo comando.
/// </summary>
public sealed class RecordCreditDecisionCommandHandlerTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> HuellaAsync(Guid solicitud)
    {
        var r = await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(solicitud), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value.ContentSha256;
    }

    private async Task<Result<CreditDecisionResultDto>> DecidirAsync(Guid solicitud, CreditDecisionOutcome resultado, string? motivo = null,
        DecisionConditionsInput? condiciones = null, string? huella = null)
    {
        var r = await _o.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, solicitud, resultado, motivo, condiciones,
            huella ?? await HuellaAsync(solicitud)) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private void ComoAnalista()
    {
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");
    }

    private void ComoGerente()
    {
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Management");
    }

    private async Task<CreditApplication> LeerAsync(Guid solicitud) => await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == solicitud);

    // ------------------------------------------------------------------------------------------------------- atribución --

    [Fact]
    public async Task Sin_el_permiso_del_nivel_efectivo_es_el_404_de_la_solicitud()
    {
        var s = await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read");

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).Error.Code.Should().Be("Lending.Application.NotFound");
    }

    [Fact]
    public async Task El_maestro_y_los_administradores_sin_Lending_Decisions_no_deciden()
    {
        var s = await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder();
        _o.G.Permisos.EsMaestroGlobal.Returns(true);

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).Error.Code.Should().Be("Lending.Application.NotFound",
            "C86: el atajo del maestro global no da atribución para decidir");
    }

    [Fact]
    public async Task Un_nivel_colegiado_no_se_decide_aqui_y_dice_el_organo()
    {
        _o.Credito(_o.Ana, 40_000_000m, "CR-40");
        var s = await _o.RadicadaAsync();
        ComoGerente();

        var r = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve);

        r.Error.Code.Should().Be("Lending.Attribution.LevelRequired");
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("level").GetString().Should().Be("COMITE");
        datos.GetProperty("governingBody").GetString().Should().Be("Comité de crédito");
    }

    [Fact]
    public async Task Un_nivel_menor_que_el_requerido_es_LevelRequired_sin_organo()
    {
        _o.Credito(_o.Ana, 12_000_000m, "CR-12");
        var s = await _o.RadicadaAsync();
        ComoAnalista();

        var r = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve);

        r.Error.Code.Should().Be("Lending.Attribution.LevelRequired");
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("level").GetString().Should().Be("GERENCIA");
        datos.GetProperty("governingBody").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Una_atribucion_mayor_cubre_un_nivel_menor()
    {
        var s = await _o.RadicadaAsync();
        ComoGerente();

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).IsSuccess.Should().BeTrue("la gerencia cubre lo del analista");
    }

    // ----------------------------------------------------------------------------------------------- forma y segregación --

    [Fact]
    public async Task Negar_y_devolver_sin_motivo_son_Validation_Invalid()
    {
        var s = await _o.RadicadaAsync();
        ComoAnalista();

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Deny)).Error.Code.Should().Be("Validation.Invalid");
        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Return)).Error.Code.Should().Be("Validation.Invalid");

        var validador = new RecordCreditDecisionCommandValidator();
        validador.Validate(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId, CreditDecisionOutcome.Deny, null, null,
            new string('a', 64))).IsValid.Should().BeFalse();
        validador.Validate(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId, CreditDecisionOutcome.ApproveWithConditions,
            null, null, new string('a', 64))).IsValid.Should().BeFalse("aprobar con condiciones lleva las condiciones");
        validador.Validate(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId, CreditDecisionOutcome.Approve, null, null,
            null)).IsValid.Should().BeFalse("la huella vista es obligatoria");
    }

    [Fact]
    public async Task Una_excepcion_sin_motivo_es_Validation_Invalid_y_con_motivo_queda_marcada()
    {
        var s = await _o.RadicadaAsync(_o.Solicitud() with { PolicyExceptionRequested = true, PolicyExceptionReason = "Cliente antiguo" });
        ComoGerente();

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).Error.Code.Should().Be("Validation.Invalid");
        var r = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve, "Excepción aprobada por la trayectoria");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await _o.Db.CreditDecisions.SingleAsync()).IsPolicyException.Should().BeTrue();
    }

    [Fact]
    public async Task Quien_radico_no_decide()
    {
        var s = await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.Asesora);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task Sin_persona_no_decide()
    {
        var s = await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.SinPersona);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).Error.Code.Should().Be("Lending.Security.UserWithoutPerson");
    }

    // ------------------------------------------------------------------------------------------------------ revalidación --

    [Fact]
    public async Task Lo_no_exceptuable_no_se_aprueba_en_ningun_nivel()
    {
        var s = await _o.RadicadaAsync(_o.Solicitud() with { ApplicantCapacity = new CapacityInput(1_000_000m, 950_000m, 0m) });
        ComoAnalista();

        var r = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve);

        r.Error.Code.Should().Be("Lending.Attribution.NotExceptionable");
        OriginacionDePrueba.Datos(r.Error).GetProperty("reasons").EnumerateArray().Select(x => x.GetString()).Should().Contain("PaymentCapacity");
        (await LeerAsync(s.PublicId)).Status.Should().Be(CreditApplicationStatus.PendingDecision);
    }

    [Fact]
    public async Task Si_el_consolidado_cambio_responde_ContentChanged_y_reenruta()
    {
        var s = await _o.RadicadaAsync();
        ComoGerente();
        var vista = await HuellaAsync(s.PublicId);
        _o.Credito(_o.Ana, 4_000_000m, "CR-4");

        var r = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve, huella: vista);

        r.Error.Code.Should().Be("Lending.Decision.ContentChanged");
        var actual = OriginacionDePrueba.Datos(r.Error).GetProperty("currentSha256").GetString();
        actual.Should().NotBe(vista);
        var leida = await LeerAsync(s.PublicId);
        leida.RoutingSha256.Should().Be(actual, "se reenrutó con lo de hoy");
        leida.ConsolidatedExposure.Should().Be(7_000_000m);
        (await _o.Db.AttributionLevels.SingleAsync(n => n.Id == leida.RequiredLevelId)).Code.Should().Be("GERENCIA");
        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve, huella: actual)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Las_condiciones_menores_se_revalidan_contra_la_concentracion()
    {
        var s = await _o.RadicadaAsync();
        _o.Credito(_o.Ana, 4_000_000m, "CR-4");
        _o.PatrimonioTecnico = new IndicadorRegistradoDto(Guid.NewGuid(), 65_000_000m, "COP", new DateOnly(2026, 11, 30), "Contabilidad");
        ComoGerente();

        var completa = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve);
        completa.Error.Code.Should().Be("Lending.Concentration.LimitExceeded", "4 + 3 millones pasan el límite de 6,5");

        var mayor = await DecidirAsync(s.PublicId, CreditDecisionOutcome.ApproveWithConditions, "Más monto",
            new DecisionConditionsInput(4_000_000m));
        mayor.Error.Code.Should().Be("Validation.Invalid", "las condiciones son menores que lo pedido");

        var menor = await DecidirAsync(s.PublicId, CreditDecisionOutcome.ApproveWithConditions, "Monto menor por concentración",
            new DecisionConditionsInput(2_000_000m, 18));
        menor.IsSuccess.Should().BeTrue(menor.IsFailure ? $"{menor.Error.Code}: {menor.Error.Message}" : string.Empty);
        menor.Value.ApprovedAmount.Should().Be(2_000_000m);
        var leida = await LeerAsync(s.PublicId);
        leida.Status.Should().Be(CreditApplicationStatus.Approved);
        leida.ApprovedAmount.Should().Be(2_000_000m);
        leida.ApprovedTerms.Should().Contain("\"amount\":2000000").And.Contain("\"installments\":18");
        (await _o.Db.CreditDecisions.SingleAsync()).Conditions.Should().Be(leida.ApprovedTerms);
    }

    [Fact]
    public async Task Una_consulta_a_centrales_simulada_en_produccion_no_sirve_para_aprobar()
    {
        _o.Opciones.AmbienteDePruebas = false;
        var linea = await _o.L.LineaAsync("CENTRAL", OriginacionDePrueba.VersionDeLinea() with { RequiresBureauQuery = true });
        _o.Db.BureauQueries.Add(new BureauQuery
        {
            PersonId = _o.Ana.Id, Purpose = BureauQueryPurpose.Application, BureauCode = "DATACREDITO", ChannelCode = "SIMULADO",
            AuthorizationConsentPublicId = Guid.NewGuid(), Outcome = Domain.Enums.ExternalChannels.ExternalOutcome.Ok, Score = 780m, IsSimulated = true,
            QueriedAt = _o.G.UtcNow, QueriedByUserId = _o.Asesora.Id, ValidUntil = OriginacionDePrueba.Hoy.AddDays(30),
        });
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        var s = await _o.RadicadaAsync(_o.Solicitud() with { CreditLinePublicId = linea.PublicId });
        ComoGerente();

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve, "Con la consulta")).Error.Code.Should().Be("Lending.Adapter.SimulatedInProduction");
    }

    // ----------------------------------------------------------------------------------------------------------- aprobar --

    [Fact]
    public async Task Aprobar_deja_la_vigencia_la_decision_con_su_huella_el_cerrojo_y_la_intervencion()
    {
        var s = await _o.RadicadaAsync();
        ComoAnalista();
        _o.Cerrojo.ClearReceivedCalls();

        var r = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.SubjectStatus.Should().Be("Approved");
        r.Value.ApprovalExpiresAt.Should().Be(OriginacionDePrueba.Hoy.AddDays(30), "Originacion.DiasVigenciaDeAprobacion por defecto");
        var leida = await LeerAsync(s.PublicId);
        leida.Status.Should().Be(CreditApplicationStatus.Approved);
        leida.ApprovedAmount.Should().Be(3_000_000m);
        var decision = await _o.Db.CreditDecisions.SingleAsync();
        leida.ApprovalDecisionId.Should().Be(decision.Id);
        decision.DecisionSha256.Should().HaveLength(64);
        decision.DecidedByUserId.Should().Be(_o.Analista.Id);
        decision.DecidedByPersonId.Should().Be(_o.Analista.PersonId);
        decision.ValidUntil.Should().Be(OriginacionDePrueba.Hoy.AddDays(30));
        (await _o.Db.CaseParticipations.Where(c => c.CaseRootPublicId == s.PublicId).Select(c => c.Action).ToListAsync())
            .Should().BeEquivalentTo([AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Decidir]);
        (await _o.Db.CreditApplicationChecks.AnyAsync(c => c.ApplicationId == leida.Id && c.Stage == CheckStage.BeforeDecision))
            .Should().BeTrue("la decisión revalida en BeforeDecision");
        await _o.Cerrojo.Received().BloquearDeudoresAsync(Arg.Is<IReadOnlyCollection<int>>(x => x.Contains(_o.Ana.Id)), Arg.Any<ModoDeEspera>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ya_decidida_no_esta_pendiente()
    {
        var s = await _o.RadicadaAsync();
        ComoAnalista();
        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).IsSuccess.Should().BeTrue();

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).Error.Code.Should().Be("Lending.Decision.NotPending");
    }

    [Fact]
    public async Task Devolver_deja_la_solicitud_devuelta_y_negar_negada()
    {
        var s = await _o.RadicadaAsync();
        var t = await _o.RadicadaAsync(_o.Solicitud(2_000_000m));
        ComoAnalista();

        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Return, "Falta el certificado laboral")).Value.SubjectStatus.Should().Be("Returned");
        (await DecidirAsync(t.PublicId, CreditDecisionOutcome.Deny, "No cumple la política")).Value.SubjectStatus.Should().Be("Denied");
        (await LeerAsync(s.PublicId)).Status.Should().Be(CreditApplicationStatus.Returned);
        (await _o.Db.CreditDecisions.Select(d => d.Reason).ToListAsync()).Should().BeEquivalentTo(["Falta el certificado laboral", "No cumple la política"]);
    }

    [Fact]
    public async Task La_aprobacion_vencida_se_decide_de_nuevo_y_reemplaza_la_anterior()
    {
        var s = await _o.RadicadaAsync();
        ComoAnalista();
        (await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve)).IsSuccess.Should().BeTrue();
        var primera = await _o.Db.CreditDecisions.SingleAsync();

        _o.G.HoyLocal = OriginacionDePrueba.Hoy.AddDays(31);
        _o.G.UtcNow = _o.G.UtcNow.AddDays(31);
        (await LeerAsync(s.PublicId)).AprobacionVencida(_o.G.HoyLocal).Should().BeTrue();

        var r = await DecidirAsync(s.PublicId, CreditDecisionOutcome.Approve);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var segunda = await _o.Db.CreditDecisions.OrderByDescending(d => d.Id).FirstAsync();
        segunda.SupersedesDecisionId.Should().Be(primera.Id);
        (await LeerAsync(s.PublicId)).ApprovalValidUntil.Should().Be(_o.G.HoyLocal.AddDays(30));
    }

    [Fact]
    public async Task El_cupo_usa_el_mismo_comando()
    {
        _o.MontoOperativo(_o.Asesora, 1_000_000m);
        _o.G.ActuarComo(_o.Asesora);
        var cupo = await _o.Asignar().Handle(new AssignCreditLimitCommand(_o.Ana.PublicId, CreditLimitKind.Consolidated, null, CreditLimitOrigin.Assigned,
            3_000_000m, OriginacionDePrueba.Hoy, null, null, "Cupo de la asociada") { OperationKey = Guid.NewGuid() }, default);
        cupo.Value.Status.Should().Be(EstadosDeCupo.PendienteDeDecision);
        _o.Db.ChangeTracker.Clear();
        ComoAnalista();

        var r = await _o.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditLimit, cupo.Value.PublicId, CreditDecisionOutcome.Approve,
            null, null, cupo.Value.Routing!.ContentSha256) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.SubjectStatus.Should().Be(EstadosDeCupo.Activo);
        r.Value.ResultingSubjectPublicId.Should().NotBe(cupo.Value.PublicId, "aprobar otorga una versión nueva y firme");
    }

    [Fact]
    public async Task Un_sujeto_sin_decision_individual_todavia_no_se_decide()
    {
        var r = await _o.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.Novelty, Guid.NewGuid(), CreditDecisionOutcome.Approve, null, null,
            new string('a', 64)) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.Session.SubjectNotSupported");
    }
}
