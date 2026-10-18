using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Risk.Evaluations;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Lending.WriteOffs;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// La evaluación de cartera (feature 013, K3, US14, T0899; api.md §12.6; FR-102): se prepara con su corte y su mes de efecto (los del tipo de entidad si el
/// documento los trae), sin otra abierta del mismo corte, con la calificación por las reglas al corte de cada crédito; el comité de riesgos la recomienda y
/// el consejo la aprueba por el acta; se aplica con esos dos puntos, y el cierre mensual del mes de efecto recibe la candidata <c>Evaluation</c> sin
/// reescribir el cierre anterior. (nuevo)
/// </summary>
public sealed class EvaluacionDeCarteraTests : IAsyncLifetime
{
    private static readonly DateOnly Corte = new(2026, 12, 31);

    private CierreDePrueba _c = null!;
    private GoverningBody _comite = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        _comite = _c.G.Organo("COMRIES", "Comité de riesgos", GoverningBodyKind.RiskCommittee);
        await _c.CausarDiciembreAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private PreparePortfolioEvaluationCommandHandler Preparar() =>
        new(_c.Db, _c.D.Numerador(), _c.G.Perfil, new LectorDeDocumentoNormativo(_c.Db),
            new NormaPendienteDeCartera(_c.D.Parametros(), _c.G.Perfil, new LectorDeDocumentoNormativo(_c.Db)), _c.G.Alcance, _c.G.Auditoria, _c.G.Actor,
            _c.G.Reloj);

    private DiscardPortfolioEvaluationCommandHandler Descartar() => new(_c.Db);

    private ApplyPortfolioEvaluationCommandHandler Aplicar() => new(_c.Db, new PuntoDeActaCerrado(_c.Db), _c.G.Actor, _c.G.Reloj);

    private AsuntoDeEvaluacionDeCartera Asunto() => new(_c.Db);

    private async Task<Result<PortfolioEvaluationDto>> PrepararAsync(string efecto = "2027-01", RatingCategory propuesta = RatingCategory.D,
        DateOnly? corte = null)
    {
        _c.G.ActuarComo(_c.Jefe);
        var r = await Preparar().Handle(new PreparePortfolioEvaluationCommand(corte ?? Corte, efecto, """{ "criterio": "capacidad de pago" }""",
            [new PortfolioEvaluationLoanInput(_c.Credito.PublicId, propuesta, "Deterioro de la capacidad de pago del deudor")]) { OperationKey = Guid.NewGuid() }, default);
        _c.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<(Session Sesion, SessionItem Punto)> PuntoAsync(GoverningBody organo, Guid evaluacion, SessionItemKind clase,
        CreditDecisionOutcome resultado = CreditDecisionOutcome.Approve)
    {
        var sesion = new Session
        {
            GoverningBodyId = organo.Id, Kind = SessionKind.Ordinary, Mode = SessionMode.InPerson, HeldOn = _c.G.HoyLocal, Status = SessionStatus.Closed,
            Number = $"{organo.Code}-{Guid.NewGuid():N}"[..20], ClosedAt = _c.G.UtcNow,
        };
        _c.Db.LendingSessions.Add(sesion);
        await _c.Db.SaveChangesAsync();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = clase, Rule = VotingRule.Majority, SubjectType = LendingDecisionSubjects.PortfolioEvaluation,
            SubjectPublicId = evaluacion, Title = "Evaluación de cartera", Outcome = resultado,
        };
        _c.Db.SessionItems.Add(punto);
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
        return (sesion, punto);
    }

    private async Task<Result> DecidirAsync(Session sesion, SessionItem punto, CreditDecisionOutcome resultado = CreditDecisionOutcome.Approve)
    {
        var decision = CreditDecision.Colegiada(new LoDecidido(punto.SubjectType, punto.SubjectPublicId, punto.SubjectPublicId, resultado)
        {
            Reason = resultado == CreditDecisionOutcome.Approve ? null : "No alcanzó la mayoría",
        }, punto, _c.G.UtcNow);
        var r = await Asunto().AlDecidirAsync(new DecisionDeActa(sesion, punto, decision, resultado, null, _c.G.HoyLocal), default);
        if (r.IsSuccess) await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>El comité la recomienda (punto Recommendation) y el consejo la aprueba; devuelve los dos puntos.</summary>
    private async Task<(SessionItem Recomendacion, SessionItem Aprobacion)> AprobadaAsync(Guid evaluacion)
    {
        var (_, recomendacion) = await PuntoAsync(_comite, evaluacion, SessionItemKind.Recommendation);
        var (sesion, aprobacion) = await PuntoAsync(_c.D.O.Consejo, evaluacion, SessionItemKind.Decision);
        var r = await DecidirAsync(sesion, aprobacion);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return (recomendacion, aprobacion);
    }

    private Task<Result<PortfolioEvaluationDto>> AplicarAsync(Guid evaluacion, Guid recomendacion, Guid aprobacion)
    {
        _c.G.ActuarComo(_c.Jefe);
        return Aplicar().Handle(new ApplyPortfolioEvaluationCommand(evaluacion, recomendacion, aprobacion) { OperationKey = Guid.NewGuid() }, default);
    }

    [Fact]
    public async Task Prepararla_sella_la_calificacion_por_las_reglas_y_no_admite_otra_abierta_del_mismo_corte()
    {
        var r = await PrepararAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (r.Value.Status, r.Value.CutoffDate, r.Value.EffectMonth, r.Value.LoanCount).Should().Be((PortfolioEvaluationStatus.Draft, Corte, "2027-01", 1));
        r.Value.Number.Should().StartWith(EvaluacionesDeCartera.Prefijo);
        var linea = r.Value.Loans.Should().ContainSingle().Subject;
        (linea.RuleRatingAtCutoff, linea.ProposedCategory, linea.Reason).Should().Be((RatingCategory.A, RatingCategory.D, "Deterioro de la capacidad de pago del deudor"));

        (await PrepararAsync()).Error.Code.Should().Be(EvaluacionesDeCartera.CodigoSuperpuesta);
    }

    [Fact]
    public async Task El_mes_de_efecto_va_despues_del_corte_y_un_credito_que_no_estaba_vivo_no_entra()
    {
        var validador = new PreparePortfolioEvaluationCommandValidator();
        validador.Validate(new PreparePortfolioEvaluationCommand(Corte, "2026-12", null, [new PortfolioEvaluationLoanInput(Guid.NewGuid(), RatingCategory.B, "x")]))
            .Errors.Should().Contain(e => e.PropertyName == "EffectMonth");
        validador.Validate(new PreparePortfolioEvaluationCommand(new DateOnly(2026, 12, 15), "2027-01", null,
            [new PortfolioEvaluationLoanInput(Guid.NewGuid(), RatingCategory.B, "x")])).Errors.Should().Contain(e => e.PropertyName == "CutoffDate");
        validador.Validate(new PreparePortfolioEvaluationCommand(Corte, "2027-01", null,
            [new PortfolioEvaluationLoanInput(Guid.NewGuid(), RatingCategory.B, "x", RatingSubcategory.E1)])).IsValid.Should().BeFalse("sólo E lleva subcategoría");

        (await PrepararAsync("2026-12", corte: new DateOnly(2026, 11, 30))).Error.Code.Should().Be(EvaluacionesDeCartera.CodigoCreditoNoVivo,
            "el crédito se desembolsó el 1 de diciembre");
    }

    [Fact]
    public async Task Fuera_de_la_periodicidad_del_tipo_de_entidad_se_prepara_igual_y_avisa()
    {
        await CastigosDePrueba.SembrarTipoDeEntidadAsync(_c.Db, _c.G, evaluacion: """{ "cutoffMonths": [6, 12], "effectMonthsAfterCutoff": 1 }""");

        var r = await PrepararAsync("2027-02");

        // Decisión del dueño del 2026-10-18: las periodicidades regulatorias son informativas; antes respondía Lending.PortfolioEvaluation.PeriodInvalid.
        r.IsSuccess.Should().BeTrue("la entidad decide según su calendario: fuera de la periodicidad sólo se avisa");
        r.Value.Warnings.Should().ContainSingle(w => w.Code == EvaluacionesDeCartera.AvisoFueraDePeriodicidad)
            .Which.Message.Should().Contain("2027-01", "el aviso dice el mes de efecto que esperaría la norma");
    }

    [Fact]
    public async Task Dentro_de_la_periodicidad_no_avisa()
    {
        await CastigosDePrueba.SembrarTipoDeEntidadAsync(_c.Db, _c.G, evaluacion: """{ "cutoffMonths": [6, 12], "effectMonthsAfterCutoff": 1 }""");

        var r = await PrepararAsync("2027-01");

        r.IsSuccess.Should().BeTrue();
        r.Value.Warnings.Should().NotContain(w => w.Code == EvaluacionesDeCartera.AvisoFueraDePeriodicidad);
    }

    [Fact]
    public async Task El_comite_la_recomienda_y_el_consejo_la_aprueba_por_el_acta()
    {
        var e = (await PrepararAsync()).Value;

        var enBorrador = (await Asunto().DescribirAsync(e.PublicId, _c.G.HoyLocal, default)).Value;
        (enBorrador.Pendiente, enBorrador.GoverningBodyId).Should().Be((true, _comite.Id), "sin recomendación la trata el comité de riesgos");
        enBorrador.Personas.Should().ContainSingle(p => p.PersonId == _c.Jefe.PersonId);

        var (_, recomendacion) = await PuntoAsync(_comite, e.PublicId, SessionItemKind.Recommendation);
        (await Asunto().DescribirAsync(e.PublicId, _c.G.HoyLocal, default)).Value.GoverningBodyId.Should().Be(_c.D.O.Consejo.Id);
        var (sesion, aprobacion) = await PuntoAsync(_c.D.O.Consejo, e.PublicId, SessionItemKind.Decision);
        (await DecidirAsync(sesion, aprobacion)).IsSuccess.Should().BeTrue();

        var aprobada = await _c.Db.PortfolioEvaluations.AsNoTracking().SingleAsync(x => x.PublicId == e.PublicId);
        (aprobada.Status, aprobada.RecommendationSessionItemId, aprobada.ApprovalSessionItemId).Should()
            .Be((PortfolioEvaluationStatus.Approved, recomendacion.Id, aprobacion.Id));
    }

    [Fact]
    public async Task Un_punto_que_decide_del_comite_la_deja_recomendada_y_sin_recomendacion_el_consejo_no_decide()
    {
        var e = (await PrepararAsync()).Value;
        var (sesionDelConsejo, puntoDelConsejo) = await PuntoAsync(_c.D.O.Consejo, e.PublicId, SessionItemKind.Decision);
        (await DecidirAsync(sesionDelConsejo, puntoDelConsejo)).Error.Code.Should().Be(EvaluacionesDeCartera.CodigoSinRecomendacion);

        var (sesion, punto) = await PuntoAsync(_comite, e.PublicId, SessionItemKind.Decision);
        (await DecidirAsync(sesion, punto)).IsSuccess.Should().BeTrue();
        (await _c.Db.PortfolioEvaluations.AsNoTracking().SingleAsync(x => x.PublicId == e.PublicId)).Status.Should().Be(PortfolioEvaluationStatus.Recommended);

        var (otra, rechazo) = await PuntoAsync(_c.D.O.Consejo, e.PublicId, SessionItemKind.Decision, CreditDecisionOutcome.Deny);
        (await DecidirAsync(otra, rechazo, CreditDecisionOutcome.Deny)).IsSuccess.Should().BeTrue();
        (await _c.Db.PortfolioEvaluations.AsNoTracking().SingleAsync(x => x.PublicId == e.PublicId)).Status.Should().Be(PortfolioEvaluationStatus.Rejected);
    }

    [Fact]
    public async Task Se_aplica_con_sus_dos_puntos_y_con_otros_es_ItemMismatch()
    {
        var e = (await PrepararAsync()).Value;
        (await AplicarAsync(e.PublicId, Guid.NewGuid(), Guid.NewGuid())).Error.Code.Should().Be(EvaluacionesDeCartera.CodigoEstadoInvalido);
        var (recomendacion, aprobacion) = await AprobadaAsync(e.PublicId);

        (await AplicarAsync(e.PublicId, aprobacion.PublicId, aprobacion.PublicId)).Error.Code.Should().Be(ErroresDeGobierno.CodigoPuntoNoCorresponde);
        (await AplicarAsync(e.PublicId, recomendacion.PublicId, recomendacion.PublicId)).Error.Code.Should().Be(ErroresDeGobierno.CodigoPuntoNoCorresponde);
        var r = await AplicarAsync(e.PublicId, recomendacion.PublicId, aprobacion.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (r.Value.Status, r.Value.RecommendationItem!.Kind, r.Value.ApprovalItem!.Kind).Should()
            .Be((PortfolioEvaluationStatus.Applied, SessionItemKind.Recommendation, SessionItemKind.Decision));
        var lista = await new ListPortfolioEvaluationsQueryHandler(_c.Db, _c.G.Alcance).Handle(new ListPortfolioEvaluationsQuery(PortfolioEvaluationStatus.Applied), default);
        lista.Value.Should().ContainSingle(x => x.PublicId == e.PublicId);
        var detalle = await new GetPortfolioEvaluationQueryHandler(_c.Db, _c.G.Alcance).Handle(new GetPortfolioEvaluationQuery(e.PublicId), default);
        detalle.Value.Loans.Should().ContainSingle();
        _c.G.AlcanceActual = Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio;
        (await new GetPortfolioEvaluationQueryHandler(_c.Db, _c.G.Alcance).Handle(new GetPortfolioEvaluationQuery(e.PublicId), default))
            .Error.Code.Should().Be(EvaluacionesDeCartera.CodigoInexistente);
    }

    [Fact]
    public async Task El_cierre_del_mes_de_efecto_la_recibe_como_candidata_y_el_anterior_no_se_reescribe()
    {
        var diciembre = await _c.CerrarAsync(2026, 12);
        var e = (await PrepararAsync()).Value;
        var (recomendacion, aprobacion) = await AprobadaAsync(e.PublicId);
        (await AplicarAsync(e.PublicId, recomendacion.PublicId, aprobacion.PublicId)).IsSuccess.Should().BeTrue();

        await _c.C.CausarHastaAsync(new DateOnly(2027, 1, 31));
        _c.IrA(new DateOnly(2027, 2, 2));
        var enero = await _c.CerrarAsync(2027, 1);

        var deEnero = await Application.Lending.Integration.CategoriaContableDeCartera.LineasDe(_c.Db, enero).SingleAsync(l => l.LoanId == _c.Credito.Id);
        (deEnero.FinalRating, deEnero.RatingCause).Should().Be((RatingCategory.D, RatingCause.Evaluation));
        var deDiciembre = await Application.Lending.Integration.CategoriaContableDeCartera.LineasDe(_c.Db, diciembre).SingleAsync(l => l.LoanId == _c.Credito.Id);
        deDiciembre.RatingCause.Should().NotBe(RatingCause.Evaluation, "la evaluación rige desde el cierre de su mes de efecto y nunca reescribe cierres anteriores");
    }

    [Fact]
    public async Task Descartarla_mientras_no_se_aplique_exige_motivo_y_libera_el_corte()
    {
        var r = await PrepararAsync();
        new DiscardPortfolioEvaluationCommandValidator().Validate(new DiscardPortfolioEvaluationCommand(r.Value.PublicId, "")).IsValid.Should().BeFalse();
        await AprobadaAsync(r.Value.PublicId);

        var descartada = await Descartar().Handle(new DiscardPortfolioEvaluationCommand(r.Value.PublicId, "El consejo pidió rehacerla")
            { OperationKey = Guid.NewGuid() }, default);
        _c.Db.ChangeTracker.Clear();

        descartada.IsSuccess.Should().BeTrue(descartada.IsFailure ? $"{descartada.Error.Code}: {descartada.Error.Message}" : string.Empty);
        descartada.Value.Status.Should().Be(PortfolioEvaluationStatus.Discarded);
        (await Descartar().Handle(new DiscardPortfolioEvaluationCommand(r.Value.PublicId, "Otra vez") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(EvaluacionesDeCartera.CodigoEstadoInvalido);
        (await PrepararAsync()).IsSuccess.Should().BeTrue("descartada, se prepara otra del mismo corte");
    }

    [Fact]
    public async Task Prepararla_avisa_la_periodicidad_de_la_norma_si_sigue_pendiente()
    {
        var r = await PrepararAsync();

        r.Value.Warnings!.Select(a => a.Code).Should().BeEquivalentTo([NormaPendienteDeCartera.PeriodicidadDeLaEvaluacion]);
    }
}
