using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.WriteOffs;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.WriteOffs;

/// <summary>
/// El lote de castigo (feature 013, K3, US14, T0897; api.md §11.3; FR-095, US14-1, US14-2, C98): armarlo verifica los requisitos de cada crédito
/// —los que cumplen entran con sus montos y el deterioro del último cierre, los que no quedan rechazados con lo que falta—, sólo en borrador se le agregan
/// o quitan créditos, enviarlo lo congela, el consejo lo aprueba o rechaza por el acta, y aplicarlo vuelve a verificar, pasa la validación previa contable,
/// deja cada crédito castigado con su operación <c>WriteOff</c> y su <c>CreditoCastigado</c>, y sella y alerta el reporte al supervisor si el tipo de entidad
/// debe hacerlo. (nuevo)
/// </summary>
public sealed class LoteDeCastigoTests : IAsyncLifetime
{
    private CastigosDePrueba _c = null!;

    public async Task InitializeAsync() => _c = await CastigosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task<WriteOffSupportInput> DeBetoAsync()
    {
        var credito = await _c.P.C.DesembolsarAsync(_c.D.O.Solicitud(1_000_000m, 12, _c.D.O.Beto));
        return await _c.SoportesAsync(credito);
    }

    [Fact]
    public async Task Armarlo_incluye_a_los_que_cumplen_y_rechaza_a_los_demas_con_lo_que_falta()
    {
        var ana = await _c.CastigableAsync();
        var beto = await DeBetoAsync();

        var r = await _c.ArmarAsync(ana, beto);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var lote = r.Value;
        (lote.Status, lote.LoanCount, lote.CutoffDate).Should().Be((WriteOffBatchStatus.Draft, 1, _c.G.HoyLocal));
        lote.Number.Should().StartWith(CastigosDeCartera.Prefijo);
        var incluido = lote.Items.Should().ContainSingle().Subject;
        (incluido.LoanPublicId, incluido.Status, incluido.Principal, incluido.Impairment).Should()
            .Be((_c.Credito.PublicId, WriteOffItemStatus.Included, 3_000_000m, 3_500_000m));
        incluido.Requirements.Should().OnlyContain(x => x.Met).And.Contain(x => x.Code == RequisitosDeCastigo.NoEsAsociado && x.Source == "Norma");
        (lote.TotalPrincipal, lote.Total).Should().Be((3_000_000m, incluido.Balance));

        var rechazado = lote.Rejected.Should().ContainSingle().Subject;
        rechazado.LoanPublicId.Should().Be(beto.LoanPublicId);
        rechazado.Missing.Should().Contain([RequisitosDeCastigo.NoEsAsociado, RequisitosDeCastigo.DeterioroTotal]);
        rechazado.Reason.Should().NotBeNullOrWhiteSpace();
        (await _c.Db.CaseParticipations.AsNoTracking().SingleAsync(p => p.CaseRootPublicId == lote.PublicId)).Action
            .Should().Be(AccionesDeSegregacion.ArmarLoteDeCastigo);
    }

    [Fact]
    public async Task Sin_soportes_ni_consulta_de_aportes_faltan_los_requisitos_de_la_norma()
    {
        await _c.AsociadoAsync(_c.Credito, asociado: false);
        await _c.CierreAsync(3_000_000m, 500_000m);

        var r = await _c.ArmarAsync(new WriteOffSupportInput(_c.Credito.PublicId,
            IrrecoverabilityAttachmentPublicId: await _c.AdjuntoAsync(Guid.NewGuid())));

        r.Value.Items.Should().BeEmpty();
        r.Value.Rejected.Single().Missing.Should().BeEquivalentTo(
            [RequisitosDeCastigo.AportesCruzados, RequisitosDeCastigo.InformeMotivado, RequisitosDeCastigo.ConceptoDeIrrecuperabilidad,
                RequisitosDeCastigo.GestionesAgotadas], "un soporte de otro crédito no cuenta y sin informe tampoco se acreditan las gestiones");
    }

    [Fact]
    public async Task Agregar_uno_que_no_cumple_es_RequirementMissing_y_fuera_de_borrador_NotDraft()
    {
        var ana = await _c.CastigableAsync();
        var beto = await DeBetoAsync();
        var lote = (await _c.ArmarAsync(ana)).Value;

        var r = await _c.Agregar().Handle(new AddWriteOffBatchItemCommand(lote.PublicId, beto.LoanPublicId, beto.IrrecoverabilityAttachmentPublicId,
            beto.ReportAttachmentPublicId, beto.ContributionsOffsetAttachmentPublicId) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(RequisitosDeCastigo.CodigoRequisitoFaltante);
        r.Error.Message.Should().Contain("que el deudor ya no sea asociado");
        var faltan = CastigosDePrueba.Datos(r.Error).GetProperty("missing").EnumerateArray().Select(x => x.GetString()).ToList();
        faltan.Should().Contain(RequisitosDeCastigo.NoEsAsociado);

        var otraVez = await _c.Agregar().Handle(new AddWriteOffBatchItemCommand(lote.PublicId, ana.LoanPublicId) { OperationKey = Guid.NewGuid() }, default);
        otraVez.Error.Code.Should().Be(ErroresDeCastigo.CodigoYaIncluido);

        await _c.Enviar().Handle(new SubmitWriteOffBatchCommand(lote.PublicId) { OperationKey = Guid.NewGuid() }, default);
        _c.Db.ChangeTracker.Clear();
        var enviado = await _c.Agregar().Handle(new AddWriteOffBatchItemCommand(lote.PublicId, beto.LoanPublicId) { OperationKey = Guid.NewGuid() }, default);
        enviado.Error.Code.Should().Be(ErroresDeCastigo.CodigoNoBorrador);
        (await _c.Leer().Handle(new GetWriteOffBatchQuery(Guid.NewGuid()), default)).Error.Code.Should().Be(ErroresDeCastigo.CodigoInexistente);
    }

    [Fact]
    public async Task Un_credito_en_otro_lote_abierto_no_entra_a_uno_nuevo()
    {
        var ana = await _c.CastigableAsync();
        await _c.ArmarAsync(ana);

        var r = await _c.ArmarAsync(ana);

        r.Error.Code.Should().Be(ErroresDeCastigo.CodigoEnOtroLote);
    }

    [Fact]
    public async Task Quitar_recalcula_y_enviar_lo_congela()
    {
        var ana = await _c.CastigableAsync();
        var beto = await DeBetoAsync();
        var lote = (await _c.ArmarAsync(ana, beto)).Value;

        var sinBeto = await _c.Quitar().Handle(new RemoveWriteOffBatchItemCommand(lote.PublicId, lote.Rejected[0].ItemPublicId) { OperationKey = Guid.NewGuid() }, default);
        sinBeto.Value.Rejected.Should().BeEmpty();
        sinBeto.Value.LoanCount.Should().Be(1);
        _c.Db.ChangeTracker.Clear();

        var enviado = await _c.Enviar().Handle(new SubmitWriteOffBatchCommand(lote.PublicId) { OperationKey = Guid.NewGuid() }, default);

        enviado.Value.Status.Should().Be(WriteOffBatchStatus.Submitted);
        _c.Db.ChangeTracker.Clear();
        var quitar = await _c.Quitar().Handle(new RemoveWriteOffBatchItemCommand(lote.PublicId, lote.Items[0].ItemPublicId) { OperationKey = Guid.NewGuid() }, default);
        quitar.Error.Code.Should().Be(ErroresDeCastigo.CodigoNoBorrador);
    }

    [Fact]
    public async Task Un_lote_sin_creditos_que_cumplan_no_se_envia()
    {
        var lote = (await _c.ArmarAsync(await DeBetoAsync())).Value;

        var r = await _c.Enviar().Handle(new SubmitWriteOffBatchCommand(lote.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeCastigo.CodigoEstadoInvalido);
    }

    [Fact]
    public async Task El_consejo_lo_decide_por_el_acta_y_quien_lo_armo_no_vota()
    {
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync());

        var d = (await _c.Asunto().DescribirAsync(lote.PublicId, _c.G.HoyLocal, default)).Value;
        (d.Pendiente, d.GoverningBodyId, d.ContentSha256.Length).Should().Be((true, _c.D.O.Consejo.Id, 64));
        d.Personas.Should().ContainSingle(p => p.PersonId == _c.Jefe.PersonId);
        d.Titulo.Should().Contain(lote.Number);

        var punto = await _c.AprobadoAsync(lote);
        var aprobado = await _c.Db.WriteOffBatches.AsNoTracking().SingleAsync(b => b.PublicId == lote.PublicId);
        (aprobado.Status, aprobado.SessionItemId).Should().Be((WriteOffBatchStatus.Approved, punto.Id));
        (await _c.Asunto().DescribirAsync(lote.PublicId, _c.G.HoyLocal, default)).Value.Pendiente.Should().BeFalse();

        var otro = await _c.EnviadoAsync(await _c.CastigableAsync(await _c.P.C.DesembolsarAsync(_c.D.O.Solicitud(1_000_000m, 12, _c.D.O.Dario))));
        await _c.AprobadoAsync(otro, CreditDecisionOutcome.Deny);
        (await _c.Db.WriteOffBatches.AsNoTracking().SingleAsync(b => b.PublicId == otro.PublicId)).Status.Should().Be(WriteOffBatchStatus.Rejected);
    }

    [Fact]
    public async Task Sin_aprobacion_es_ApprovalRequired_y_con_otro_punto_ItemMismatch()
    {
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync());

        (await _c.AplicarAsync(lote.PublicId, Guid.NewGuid())).Error.Code.Should().Be(ErroresDeCastigo.CodigoAprobacionRequerida);

        await _c.AprobadoAsync(lote);
        var (_, ajeno) = await _c.PuntoCerradoAsync(_c.D.O.Consejo, Domain.Lending.Attributions.LendingDecisionSubjects.WriteOffBatch, lote.PublicId,
            CreditDecisionOutcome.Approve);
        (await _c.AplicarAsync(lote.PublicId, ajeno.PublicId)).Error.Code.Should().Be(ErroresDeGobierno.CodigoPuntoNoCorresponde);
    }

    [Fact]
    public async Task Con_el_par_activo_quien_armo_el_lote_no_lo_aplica()
    {
        await _c.ParametroAsync(ParametrosDeCartera.SegregacionIncompatibilidades,
            "[{\"a\": \"ArmarLoteDeCastigo\", \"b\": \"AplicarCastigo\", \"activo\": true}]");
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync());
        var punto = await _c.AprobadoAsync(lote);

        var r = await _c.AplicarAsync(lote.PublicId, punto.PublicId, _c.Jefe);

        r.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto);
        (await _c.AplicarAsync(lote.PublicId, punto.PublicId)).IsSuccess.Should().BeTrue("otra persona sí lo aplica");
    }

    [Fact]
    public async Task Con_Bloquear_y_sin_regla_no_se_aplica()
    {
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync());
        var punto = await _c.AprobadoAsync(lote);
        _c.N.SinRegla(CreditoCastigadoV1.Type);

        var r = await _c.AplicarAsync(lote.PublicId, punto.PublicId);

        r.Error.Code.Should().Be("Lending.Prevalidation.NotPostable");
        (await _c.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _c.Credito.Id)).Status.Should().Be(LoanStatus.Active);
    }

    [Fact]
    public async Task Los_requisitos_se_vuelven_a_verificar_al_aplicar_y_si_ninguno_cumple_el_lote_no_se_aplica()
    {
        // Bloque D (decisión del dueño del 2026-10-15): el que ya no cumple se excluye; si no queda ninguno, el lote no se aplica.
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync());
        var punto = await _c.AprobadoAsync(lote);
        await _c.AsociadoAsync(_c.Credito, asociado: true);

        var r = await _c.AplicarAsync(lote.PublicId, punto.PublicId);

        r.Error.Code.Should().Be(ErroresDeCastigo.CodigoNingunoCumple);
        var excluido = CastigosDePrueba.Datos(r.Error).GetProperty("excluded")[0];
        excluido.GetProperty("loanNumber").GetString().Should().Be(_c.Credito.Number);
        excluido.GetProperty("missing")[0].GetString().Should().Be(RequisitosDeCastigo.NoEsAsociado);
        (await _c.Db.WriteOffBatches.AsNoTracking().SingleAsync(b => b.PublicId == lote.PublicId)).Status.Should().Be(WriteOffBatchStatus.Approved);
        (await _c.Db.WriteOffBatchItems.AsNoTracking().SingleAsync()).Status.Should().Be(WriteOffItemStatus.Included, "no se guardó nada");
    }

    [Fact]
    public async Task Al_aplicar_el_que_ya_no_cumple_se_excluye_con_su_motivo_y_el_resto_se_castiga()
    {
        var dario = await _c.P.C.DesembolsarAsync(_c.D.O.Solicitud(1_000_000m, 12, _c.D.O.Dario));
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync(), await _c.CastigableAsync(dario, 1_000_000m, 100_000m));
        lote.LoanCount.Should().Be(2);
        var punto = await _c.AprobadoAsync(lote);
        await _c.AsociadoAsync(dario, asociado: true);

        var r = await _c.AplicarAsync(lote.PublicId, punto.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Operations.Should().ContainSingle(o => o.LoanPublicId == _c.Credito.PublicId);
        var excluido = r.Value.Excluded.Should().ContainSingle().Subject;
        excluido.LoanPublicId.Should().Be(dario.PublicId);
        excluido.Missing.Should().Equal(RequisitosDeCastigo.NoEsAsociado);
        excluido.Reason.Should().Contain("ya no cumple");
        var item = await _c.Db.WriteOffBatchItems.AsNoTracking().SingleAsync(i => i.LoanId == dario.Id);
        (item.Status, item.RejectionReason).Should().Be((WriteOffItemStatus.Rejected, excluido.Reason));
        (await _c.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == dario.Id)).Status.Should().Be(LoanStatus.Active);
        var aplicado = await _c.Db.WriteOffBatches.AsNoTracking().SingleAsync(b => b.PublicId == lote.PublicId);
        (aplicado.Status, aplicado.LoanCount, aplicado.TotalPrincipal).Should().Be((WriteOffBatchStatus.Applied, 1, 3_000_000m));
        var eventos = (await _c.Db.AuditOutbox.AsNoTracking().ToListAsync()).Select(f => AuditoriaEncadenada.LeerCarga(f.PayloadJson!))
            .Where(e => e.Action == AuditEventTypes.LendingWriteOffItemExcluded).ToList();
        eventos.Should().ContainSingle().Which.EntityPublicId.Should().Be(lote.PublicId.ToString());
        var detalle = (await _c.Leer().Handle(new GetWriteOffBatchQuery(lote.PublicId), default)).Value;
        detalle.Rejected.Should().ContainSingle(x => x.LoanPublicId == dario.PublicId && x.Reason == excluido.Reason);
        await _c.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Anular_el_lote_mientras_no_se_aplique_libera_sus_creditos_y_exige_motivo()
    {
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync());
        await _c.AprobadoAsync(lote);
        new VoidWriteOffBatchCommandValidator().Validate(new VoidWriteOffBatchCommand(lote.PublicId, " ")).IsValid.Should().BeFalse("sin motivo no se anula");

        var r = await _c.Anular().Handle(new VoidWriteOffBatchCommand(lote.PublicId, "El consejo pidió revisarlo") { OperationKey = Guid.NewGuid() }, default);
        _c.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(WriteOffBatchStatus.Cancelled);
        (await _c.Anular().Handle(new VoidWriteOffBatchCommand(lote.PublicId, "Otra vez") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeCastigo.CodigoEstadoInvalido);
        (await _c.ArmarAsync(await _c.SoportesAsync())).IsSuccess.Should().BeTrue("el crédito quedó libre para otro lote");
    }

    [Fact]
    public async Task Armarlo_avisa_lo_de_la_norma_que_sigue_pendiente_sin_bloquear()
    {
        var sinNorma = await _c.ArmarAsync(await _c.CastigableAsync());

        sinNorma.IsSuccess.Should().BeTrue(sinNorma.IsFailure ? $"{sinNorma.Error.Code}: {sinNorma.Error.Message}" : string.Empty);
        sinNorma.Value.Warnings!.Select(a => a.Code).Should().BeEquivalentTo([NormaPendienteDeCartera.PlazoDelReporteDeCastigos,
            NormaPendienteDeCartera.AlturaMinimaDelCastigo]);

        await _c.TipoDeEntidadAsync();
        await _c.ParametroAsync(ParametrosDeCartera.CastigoDiasMinimosDeMora, "360");
        var dario = await _c.P.C.DesembolsarAsync(_c.D.O.Solicitud(1_000_000m, 12, _c.D.O.Dario));
        var conNorma = await _c.ArmarAsync(await _c.SoportesAsync(dario));
        conNorma.Value.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Aplicarlo_castiga_cada_credito_con_su_operacion_y_su_hecho_contra_el_deterioro()
    {
        await _c.P.CausarHastaAsync(_c.G.HoyLocal.AddDays(10));
        await _c.ParametroAsync(ParametrosDeCartera.CastigoExigeDeterioroTotal, "false");
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync(deterioroDeCapital: 2_400_000m, deterioroDeIntereses: 0m));
        var punto = await _c.AprobadoAsync(lote);

        var r = await _c.AplicarAsync(lote.PublicId, punto.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var op = r.Value.Operations.Should().ContainSingle().Subject;
        var operacion = await _c.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == op.OperationPublicId);
        operacion.Kind.Should().Be(LoanOperationKind.WriteOff);
        operacion.Number.Should().StartWith(CastigosDeCartera.PrefijoDeOperacion);
        (op.ImpairmentApplied, op.ImpairmentShortfall).Should().Be((2_400_000m, op.Amount - 2_400_000m));
        op.Amount.Should().BeGreaterThan(3_000_000m, "capital más el interés causado");
        (await _c.Db.LoanTransactions.AsNoTracking().AnyAsync(t => t.OperationId == operacion.Id)).Should().BeFalse("el castigo no cambia lo que se debe");

        var credito = await _c.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _c.Credito.Id);
        (credito.Status, credito.WrittenOffOn).Should().Be((LoanStatus.WrittenOff, (DateOnly?)_c.G.HoyLocal));
        var item = await _c.Db.WriteOffBatchItems.AsNoTracking().SingleAsync(i => i.LoanId == _c.Credito.Id);
        (item.Status, item.WriteOffOperationId, operacion.WriteOffBatchItemId).Should().Be((WriteOffItemStatus.WrittenOff, operacion.Id, item.Id));
        var aplicado = await _c.Db.WriteOffBatches.AsNoTracking().SingleAsync(b => b.PublicId == lote.PublicId);
        (aplicado.Status, aplicado.AppliedByUserId, aplicado.SupervisorReportRequired).Should().Be((WriteOffBatchStatus.Applied, _c.Tesorera.Id, false));

        var hecho = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoCastigadoV1.Type);
        r.Value.Messages.Should().ContainSingle(hecho.PublicId.ToString());
        hecho.PayloadJson.Should().Contain("impairmentApplied").And.Contain("impairmentShortfall").And.Contain(lote.PublicId.ToString());
        (await _c.Db.IntegrationMessages.AsNoTracking().AnyAsync(m => m.Type == CausacionSuspendidaV1.Type))
            .Should().BeFalse("causaba normal: la suspensión la emite el ciclo de mañana al detectar la transición");
        await _c.P.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task En_una_entidad_obligada_sella_el_plazo_alerta_y_el_reporte_se_registra_una_vez()
    {
        await _c.TipoDeEntidadAsync();
        var lote = await _c.EnviadoAsync(await _c.CastigableAsync());
        var punto = await _c.AprobadoAsync(lote);

        var r = await _c.AplicarAsync(lote.PublicId, punto.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (r.Value.SupervisorReportRequired, r.Value.SupervisorReportDueDate).Should().Be((true, (DateOnly?)_c.G.HoyLocal.AddDays(30)));
        _c.D.O.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.PlazoRegulatorioPorVencer && a.EntityPublicId == lote.PublicId);

        var ajeno = await _c.AdjuntoAsync(Guid.NewGuid(), AdjuntosDeModulo.ReporteDeCastigos);
        var malo = await _c.Reportar().Handle(new RegisterWriteOffSupervisorReportCommand(lote.PublicId, "RAD-1", ajeno) { OperationKey = Guid.NewGuid() }, default);
        malo.Error.Code.Should().Be(ErroresDeCastigo.CodigoSoporteInvalido);

        var soporte = await _c.AdjuntoAsync(lote.PublicId, AdjuntosDeModulo.ReporteDeCastigos);
        var bien = await _c.Reportar().Handle(new RegisterWriteOffSupervisorReportCommand(lote.PublicId, "RAD-2026-77", soporte) { OperationKey = Guid.NewGuid() }, default);
        bien.IsSuccess.Should().BeTrue(bien.IsFailure ? $"{bien.Error.Code}: {bien.Error.Message}" : string.Empty);
        (bien.Value.SupervisorReportReference, bien.Value.SupervisorReportAttachmentPublicId).Should().Be(("RAD-2026-77", (Guid?)soporte));
        _c.Db.ChangeTracker.Clear();

        var otraVez = await _c.Reportar().Handle(new RegisterWriteOffSupervisorReportCommand(lote.PublicId, "RAD-3") { OperationKey = Guid.NewGuid() }, default);
        otraVez.Error.Code.Should().Be(ErroresDeCastigo.CodigoEstadoInvalido);
    }

    [Fact]
    public async Task La_lista_respeta_el_alcance()
    {
        var lote = (await _c.ArmarAsync(await _c.CastigableAsync())).Value;

        (await _c.Listar().Handle(new ListWriteOffBatchesQuery(), default)).Value.Items.Should().ContainSingle(b => b.PublicId == lote.PublicId);
        (await _c.Listar().Handle(new ListWriteOffBatchesQuery(WriteOffBatchStatus.Applied), default)).Value.Items.Should().BeEmpty();

        _c.G.AlcanceActual = Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio;
        (await _c.Listar().Handle(new ListWriteOffBatchesQuery(), default)).Value.Items.Should().BeEmpty();
        (await _c.Leer().Handle(new GetWriteOffBatchQuery(lote.PublicId), default)).Error.Code.Should().Be(ErroresDeCastigo.CodigoInexistente);
    }
}
