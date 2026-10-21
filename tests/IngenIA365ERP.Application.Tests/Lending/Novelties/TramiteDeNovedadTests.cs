using FluentAssertions;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Novelties;

/// <summary>
/// El trámite de una novedad hasta la decisión (feature 013, K3, T0861; api.md §11.1, FR-090, FR-091): la causa la calcula el sistema; la simulación no
/// escribe y su huella es la que queda al radicar; el radicado enruta por la matriz (sujeto <c>Novelty</c>) y quien radica no decide. (nuevo)
/// </summary>
public sealed class TramiteDeNovedadTests : IAsyncLifetime
{
    private NovedadesDePrueba _n = null!;

    public async Task InitializeAsync() => _n = await NovedadesDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _n.Dispose();
        return Task.CompletedTask;
    }

    private static NewTermsInput Prorroga(int cuotas = 36) => new(Installments: cuotas);

    [Fact]
    public async Task Pedirla_por_dificultades_la_deja_en_borrador_como_reestructuracion_con_su_numero_y_la_calificacion_de_hoy()
    {
        var r = await _n.PedirAsync(NoveltyMechanism.Extension, Prorroga());

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(LoanNoveltyStatus.Draft);
        r.Value.Cause.Should().Be(NoveltyCause.Restructuring, "por dificultades de pago es reestructuración, cualquiera sea el mecanismo (FR-090)");
        r.Value.Number.Should().StartWith(NovedadesDeCartera.Prefijo);
        r.Value.Loans.Should().ContainSingle().Which.LoanNumber.Should().Be(_n.Credito.Number);
        r.Value.RatingAtRequest.Should().NotBeNullOrEmpty();
        r.Value.Consents.Should().BeEmpty("el crédito de Ana no tiene codeudores ni garantías");
    }

    [Fact]
    public async Task A_peticion_del_deudor_y_al_dia_es_modificacion()
    {
        var r = await _n.PedirAsync(NoveltyMechanism.PaymentDateChange, new NewTermsInput(PaymentDay: 10), NoveltyRequestMotive.DebtorRequest);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Cause.Should().Be(NoveltyCause.Modification);
        var s = await _n.Simular().Handle(new SimulateLoanNoveltyQuery(r.Value.PublicId), default);
        s.IsSuccess.Should().BeTrue(s.IsFailure ? $"{s.Error.Code}: {s.Error.Message}" : string.Empty);
        s.Value.RatingAfter.Should().Be(s.Value.RatingNow, "una modificación no cambia la calificación (FR-093 sólo rige para reestructurados)");
    }

    [Fact]
    public async Task Con_mora_reciente_sobre_el_limite_no_se_admite_como_modificacion()
    {
        _n.Db.LoanArrearsHistory.Add(new LoanArrearsHistoryEntry
        {
            LoanId = _n.Credito.Id, PeriodYear = 2026, PeriodMonth = 11, MaxDaysPastDue = 75, Origin = ArrearsHistoryOrigin.Migration,
        });
        await _n.Db.SaveChangesAsync();
        _n.Db.ChangeTracker.Clear();

        var r = await _n.PedirAsync(NoveltyMechanism.Extension, Prorroga(), NoveltyRequestMotive.DebtorRequest);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeNovedades.CodigoModificacionNoAdmitida);
        var datos = NovedadesDePrueba.Datos(r.Error);
        datos.GetProperty("recentDaysPastDue").GetInt32().Should().Be(75);
        (await _n.Db.LoanNovelties.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sobre_un_credito_que_no_esta_activo_responde_NotActive()
    {
        var pagado = _n.D.O.Credito(_n.D.O.Ana, 1_000_000m, "CR-PAGADO");
        var vivo = await _n.Db.Loans.SingleAsync(l => l.Id == pagado.Id);
        vivo.Status = LoanStatus.PaidOff;
        await _n.Db.SaveChangesAsync();
        _n.Db.ChangeTracker.Clear();

        var r = await _n.PedirAsync(NoveltyMechanism.Extension, Prorroga(), NoveltyRequestMotive.Hardship, vivo);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeNovedades.CodigoCreditoNoActivo);
    }

    [Fact]
    public async Task La_simulacion_no_escribe_y_trae_el_plan_nuevo_la_calificacion_el_conteo_y_la_usura()
    {
        var pedida = (await _n.PedirAsync(NoveltyMechanism.Extension, Prorroga(36))).Value;
        var operaciones = await _n.Db.LoanOperations.CountAsync();

        var s = await _n.Simular().Handle(new SimulateLoanNoveltyQuery(pedida.PublicId), default);

        s.IsSuccess.Should().BeTrue(s.IsFailure ? $"{s.Error.Code}: {s.Error.Message}" : string.Empty);
        s.Value.Cause.Should().Be(NoveltyCause.Restructuring);
        s.Value.NewSchedule.Should().HaveCount(36);
        s.Value.NewSchedule.Sum(c => c.Principal).Should().Be(s.Value.NewPrincipal, "el plan nuevo amortiza el capital que queda");
        s.Value.RestructuringCount.Should().Be(1);
        // FR-093 (bloque B2): nunca mejor que al pedirla ni que el mínimo de la norma para un reestructurado (B en el Anexo 1 sembrado).
        (s.Value.RatingNow, s.Value.RatingAfter).Should().Be(("A", "B"));
        s.Value.Usury.Verdict.Should().NotBeNullOrEmpty();
        s.Value.SimulationSha256.Should().HaveLength(64);
        (await _n.Db.LoanOperations.CountAsync()).Should().Be(operaciones, "simular no escribe");
        (await _n.Db.LoanNovelties.AsNoTracking().SingleAsync()).SimulationSha256.Should().BeEmpty("la huella queda al radicar");
    }

    [Fact]
    public async Task Radicarla_la_enruta_por_la_matriz_y_guarda_la_huella_de_la_simulacion()
    {
        var radicada = await _n.RadicadaAsync(NoveltyMechanism.Extension, Prorroga());

        radicada.Status.Should().Be(LoanNoveltyStatus.Submitted);
        radicada.Routing.Should().NotBeNull();
        radicada.Routing!.LevelCode.Should().Be("ANALISTA", "3 millones caben en la atribución del analista");
        var guardada = await _n.Db.LoanNovelties.AsNoTracking().SingleAsync();
        guardada.SimulationSha256.Should().HaveLength(64);
        guardada.RestructuringCountAfter.Should().BeNull("el conteo se sella al aplicar");
    }

    [Fact]
    public async Task Quien_la_radica_no_la_decide()
    {
        var radicada = await _n.RadicadaAsync(NoveltyMechanism.Extension, Prorroga());
        _n.G.ActuarComo(_n.D.O.Asesora);
        _n.G.Conceder("Lending.Novelties.Read", "Lending.Decisions.Analyst");

        var r = await _n.Decidir().Handle(new Application.Lending.Attributions.RecordCreditDecisionCommand(LendingDecisionSubjects.Novelty, radicada.PublicId,
            CreditDecisionOutcome.Approve, "Cumple", null, radicada.Routing!.ContentSha256) { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task El_analista_la_aprueba_con_la_huella_vista_y_la_devuelve_a_borrador_si_pide_ajustes()
    {
        var radicada = await _n.RadicadaAsync(NoveltyMechanism.Extension, Prorroga());

        var otraHuella = await _n.DecidirAsync(LendingDecisionSubjects.Novelty, radicada.PublicId, new string('0', 64));
        otraHuella.IsFailure.Should().BeTrue("la huella vista no es la de hoy");

        var devuelta = await _n.DecidirAsync(LendingDecisionSubjects.Novelty, radicada.PublicId, radicada.Routing!.ContentSha256,
            CreditDecisionOutcome.Return, "Ajuste el plazo");
        devuelta.IsSuccess.Should().BeTrue(devuelta.IsFailure ? $"{devuelta.Error.Code}: {devuelta.Error.Message}" : string.Empty);
        (await _n.Db.LoanNovelties.AsNoTracking().SingleAsync()).Status.Should().Be(LoanNoveltyStatus.Draft);

        var otraVez = await _n.RadicadaAsync(NoveltyMechanism.Extension, Prorroga(30));
        var aprobada = await _n.DecidirAsync(LendingDecisionSubjects.Novelty, otraVez.PublicId, otraVez.Routing!.ContentSha256);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        (await _n.Db.LoanNovelties.AsNoTracking().SingleAsync(n => n.PublicId == otraVez.PublicId)).Status.Should().Be(LoanNoveltyStatus.Approved);
    }

    [Fact]
    public async Task La_lista_y_el_detalle_respetan_el_alcance_y_anular_la_deja_cancelada()
    {
        var pedida = (await _n.PedirAsync(NoveltyMechanism.Extension, Prorroga())).Value;

        var lista = await _n.Listar().Handle(new ListLoanNoveltiesQuery(), default);
        lista.Value.Items.Should().ContainSingle(i => i.PublicId == pedida.PublicId);
        var detalle = await _n.Leer().Handle(new GetLoanNoveltyQuery(pedida.PublicId), default);
        detalle.Value.Number.Should().Be(pedida.Number);

        var anulada = await _n.Anular().Handle(new CancelLoanNoveltyCommand(pedida.PublicId, "El deudor desistió") { OperationKey = Guid.NewGuid() }, default);
        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? anulada.Error.Message : string.Empty);
        (await _n.Db.LoanNovelties.AsNoTracking().SingleAsync()).Status.Should().Be(LoanNoveltyStatus.Cancelled);
    }

    [Fact]
    public async Task La_matriz_enruta_la_novedad_por_su_mecanismo_y_su_causa()
    {
        // T0879 (bloque B2): una versión de la matriz desde mañana con una regla del sujeto Novelty por mecanismo y causa (columnas nacidas en K3).
        var niveles = await _n.Db.AttributionLevels.AsNoTracking().ToListAsync();
        var organos = await _n.Db.GoverningBodies.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.PublicId);
        var entrada = niveles.OrderBy(l => l.Rank).Select(l => new AttributionLevelInput(l.Code, l.Name, l.Rank, l.Kind, l.PermissionCode,
            l.GoverningBodyId is { } g ? organos[g] : null, l.MaxConsolidatedAmount, l.IsExceptionLevel, l.IsBoardLevel)).ToList();
        _n.G.ActuarComo(_n.D.O.Asesora);
        var matriz = await new AddAttributionMatrixVersionCommandHandler(_n.Db, _n.G.Actor, _n.G.Reloj).Handle(new AddAttributionMatrixVersionCommand(
            _n.G.HoyLocal.AddDays(1), "Reestructuraciones por prórroga a gerencia", null, entrada,
            [
                new AttributionRuleInput(1, LendingDecisionSubjects.Novelty, "GERENCIA",
                    new AttributionRuleConditionsDto(NoveltyMechanism: NoveltyMechanism.Extension, NoveltyCause: NoveltyCause.Restructuring)),
            ]) { OperationKey = Guid.NewGuid() }, default);
        matriz.IsSuccess.Should().BeTrue(matriz.IsFailure ? $"{matriz.Error.Code}: {matriz.Error.Message}" : string.Empty);
        matriz.Value.Rules.Should().ContainSingle().Which.Conditions.NoveltyMechanism.Should().Be(NoveltyMechanism.Extension);
        _n.Db.ChangeTracker.Clear();
        _n.D.Manana();

        var reestructuracion = await _n.RadicadaAsync(NoveltyMechanism.Extension, Prorroga());
        reestructuracion.Routing!.LevelCode.Should().Be("GERENCIA");
        reestructuracion.Routing.Reasons.Should().Contain(r => r.Detail.Contains("mecanismo Extension") && r.Detail.Contains("causa Restructuring"));
    }

    [Fact]
    public async Task La_regla_de_novedad_por_mecanismo_no_alcanza_otro_mecanismo()
    {
        var niveles = await _n.Db.AttributionLevels.AsNoTracking().ToListAsync();
        var organos = await _n.Db.GoverningBodies.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.PublicId);
        var entrada = niveles.OrderBy(l => l.Rank).Select(l => new AttributionLevelInput(l.Code, l.Name, l.Rank, l.Kind, l.PermissionCode,
            l.GoverningBodyId is { } g ? organos[g] : null, l.MaxConsolidatedAmount, l.IsExceptionLevel, l.IsBoardLevel)).ToList();
        _n.G.ActuarComo(_n.D.O.Asesora);
        (await new AddAttributionMatrixVersionCommandHandler(_n.Db, _n.G.Actor, _n.G.Reloj).Handle(new AddAttributionMatrixVersionCommand(
            _n.G.HoyLocal.AddDays(1), "Novaciones a gerencia", null, entrada,
            [new AttributionRuleInput(1, LendingDecisionSubjects.Novelty, "GERENCIA", new AttributionRuleConditionsDto(NoveltyMechanism: NoveltyMechanism.Novation))])
            { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();
        _n.Db.ChangeTracker.Clear();
        _n.D.Manana();

        (await _n.RadicadaAsync(NoveltyMechanism.Extension, Prorroga())).Routing!.LevelCode.Should().Be("ANALISTA");
    }
}
