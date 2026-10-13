using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0677 (T47, T48; contracts/contabilidad.md §11; mensajes.md §10.17, §10.23; data-model §23.1–§23.5): el cierre mensual en sus
/// dos pasos sobre el crédito de Ana (<see cref="CierreDePrueba"/>). Las tandas guardan las líneas y el historial del mes enlazados a la corrida y
/// <b>sin</b> mensajes; la transacción final, con <c>LND_Setup</c> exclusivo, inserta el encabezado (R, marca de agua, documentos con SHA-256,
/// modelo por segmento y marcas), el general, las partes —la reclasificación antes que el deterioro, clave <c>Close:{versión}</c>, fechadas el
/// último día del mes—, <c>CierreDeCarteraRealizado</c> y el lote <c>LendingMonthlyClose</c>. El primer cierre tras activar se compara con el base y
/// no reconoce otra vez el deterioro migrado: una disminución frente al base va a recuperación (no hubo gasto en el ejercicio). Un crédito que no se
/// puede calcular deja la corrida con errores y sin cierre; el reintento conserva R. (Preguntas C2 y C4: rigen «mismo ejercicio primero» y la
/// reclasificación completa.)
/// </summary>
public class ExecuteMonthlyCloseCommandHandlerTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        await _c.CausarDiciembreAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Las_tandas_guardan_las_lineas_y_el_historial_del_mes_enlazados_a_la_corrida_sin_mensajes_ni_encabezado()
    {
        var orden = await _c.OrdenarAsync(2026, 12);
        var mensajesAntes = await _c.Db.IntegrationMessages.CountAsync();

        var tanda = await _c.Ejecutar().Handle(new ExecuteMonthlyCloseCommand(orden.Value.ProcessRunPublicId), default);

        tanda.Value.Should().Be(AvanceDeCorrida.Pendiente);
        var corrida = await _c.Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.PublicId == orden.Value.ProcessRunPublicId);
        corrida.InformationCutoffAt.Should().NotBeNull("R se sella en la primera tanda (T47)");
        corrida.NationalValuesWatermark.Should().NotBeNull();
        corrida.CursorLoanId.Should().Be(_c.Credito.Id);
        corrida.CurrentStep.Should().Be(PasosDelCierre.Lineas);
        var linea = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync();
        (linea.ProcessRunId, linea.MonthlyCloseId, linea.LoanId, linea.BatchNumber).Should().Be(((int?)corrida.Id, (int?)null, _c.Credito.Id, (int?)1));
        var historial = await _c.Db.LoanArrearsHistory.AsNoTracking().SingleAsync();
        (historial.Origin, historial.PeriodYear, historial.PeriodMonth, historial.ProcessRunId).Should()
            .Be((ArrearsHistoryOrigin.MonthlyClose, (short)2026, (byte)12, (int?)corrida.Id));
        (await _c.Db.MonthlyCloses.CountAsync(c => c.Kind == MonthlyCloseKind.Monthly)).Should().Be(0, "líneas sin encabezado no cuentan");
        (await _c.Db.IntegrationMessages.CountAsync()).Should().Be(mensajesAntes, "las tandas no emiten (T47)");
    }

    [Fact]
    public async Task El_primer_cierre_tras_activar_se_compara_con_el_base_y_deja_encabezado_linea_general_partes_informativo_y_lote()
    {
        var cierre = await _c.CerrarAsync(2026, 12);

        var corrida = await _c.Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.Id == cierre.ProcessRunId);
        cierre.Should().BeEquivalentTo(new
        {
            Kind = MonthlyCloseKind.Monthly, PeriodYear = (short)2026, PeriodMonth = (byte)12, Version = 1, CloseDate = CierreDePrueba.FinDeDiciembre,
            PreviousCloseId = (int?)_c.Base!.Id, SupersedesCloseId = (int?)null, LoanCount = 1, TotalPrincipal = 3_000_000m,
            InformationCutoffAt = corrida.InformationCutoffAt!.Value, NationalValuesWatermark = corrida.NationalValuesWatermark!.Value,
            ObligatoryModelUnavailable = false, IncompleteHistory = false,
        }, o => o.ExcludingMissingMembers());
        cierre.UnvalidatedParameters.Should().BeTrue("el Anexo 1 sembrado está sin cotejar (E1, C76): marca, no bloquea");
        cierre.UnvalidatedParametersJson.Should().Contain("table:SarcAnexo1:segments.CONSUMO");
        var documentos = JsonDocument.Parse(cierre.DocumentsUsedJson).RootElement.EnumerateArray().Select(d => d.GetProperty("kind").GetString()).ToList();
        documentos.Should().Contain(["SarcAnexo1", "CatalogoDeModalidades"]);
        cierre.ModelBySegmentJson.Should().Contain("CONSUMO_SIN_LIBRANZA").And.Contain("ArrearsAnnex1");
        cierre.LinesSha256.Should().HaveLength(64);
        corrida.Status.Should().Be(LendingProcessRunStatus.Completed);
        corrida.ResultSha256.Should().Be(cierre.LinesSha256);
        corrida.TotalsJson.Should().Contain(cierre.PublicId.ToString("D"));

        var linea = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync();
        linea.Should().BeEquivalentTo(new
        {
            FinalRating = RatingCategory.A, AccountingRating = RatingCategory.A, RatingCause = RatingCause.NewLoan, PreviousAccountingRating = (RatingCategory?)null,
            SarcSegmentCode = "CONSUMO", ImpairmentModel = ImpairmentModel.ArrearsAnnex1, ModelBasis = ModelBasis.NotObligated, DaysPastDueAtClose = 0,
            PrincipalBalance = 3_000_000m, TotalImpairment = 0m, PreviousTotalImpairment = 0m, ContributionsDeducted = 0m,
        }, o => o.ExcludingMissingMembers());
        linea.CandidatesJson.Should().Contain("NewLoan");
        linea.ExplanationJson.Should().Contain("rige D19");

        var general = await _c.Db.MonthlyCloseGeneralImpairments.AsNoTracking().SingleAsync();
        general.Should().BeEquivalentTo(new
        {
            MonthlyCloseId = cierre.Id, BranchId = _c.Credito.BranchId, ModalityCode = "CONSUMO", GrossPortfolio = 3_000_000m, MinimumPercentage = 0.01m,
            MinimumAmount = 30_000m, TotalAmount = 30_000m, PreviousAmount = 0m, Delta = 30_000m,
        }, o => o.ExcludingMissingMembers());

        var parte = await _c.Db.AccountingFactParts.AsNoTracking().SingleAsync(p => p.MonthlyCloseId == cierre.Id);
        parte.Should().BeEquivalentTo(new
        {
            SourceKind = AccountingFactPartSource.MonthlyClose, FactType = DeterioroCalculadoV1.Type, CloseVersion = (int?)1, OriginEventKey = "Close:1",
            AccountingDate = CierreDePrueba.FinDeDiciembre, ValueDate = CierreDePrueba.FinDeDiciembre, ProcessRunId = (int?)corrida.Id, TotalAmount = 30_000m,
        }, o => o.ExcludingMissingMembers());
        var deterioro = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.PublicId == parte.MessagePublicId);
        var contenido = JsonSerializer.Deserialize<DeterioroCalculadoV1>(deterioro.PayloadJson!, OpcionesDeMensajes.Opciones)!;
        contenido.GeneralImpairment.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Kind = "General", ModalityCode = "CONSUMO", Amount = 30_000m, ReasonCode = "Increase", ImpairmentModel = "ArrearsAnnex1",
        }, o => o.ExcludingMissingMembers());
        (deterioro.OperationDate, deterioro.OriginEventKey).Should().Be((CierreDePrueba.FinDeDiciembre, "Close:1"));

        var informativo = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CierreDeCarteraRealizadoV1.Type);
        (informativo.OriginPublicId, informativo.OriginNumber, informativo.OriginEventKey, informativo.OperationDate)
            .Should().Be((cierre.PublicId, "2026-12", "Close:1", CierreDePrueba.FinDeDiciembre));
        informativo.PayloadJson.Should().Contain("\"marks\":[\"UnvalidatedParameters\"]").And.Contain("\"type\":\"DeterioroCalculado\"");

        var entrega = await _c.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => e.MessageId == deterioro.Id);
        entrega.BatchScopeKey.Should().Be("LendingClose:2026-12");
        entrega.BatchId.Should().NotBeNull("la transacción final crea el lote del cierre");
        (await _c.Db.IntegrationBatches.AsNoTracking().SingleAsync(b => b.Id == entrega.BatchId)).Trigger.Should().Be(BatchTrigger.LendingMonthlyClose);
    }

    [Fact]
    public async Task Un_credito_que_cambio_de_letra_reclasifica_todo_saldo_con_categoria_antes_del_deterioro_en_delta()
    {
        await _c.LineaDelBaseAsync(RatingCategory.C, deterioroDeCapital: 100_000m);

        var cierre = await _c.CerrarAsync(2026, 12);

        var linea = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync(l => l.ProcessRunId == cierre.ProcessRunId);
        (linea.AccountingRating, linea.PreviousAccountingRating, linea.PreviousTotalImpairment, linea.ImpairmentDelta)
            .Should().Be((RatingCategory.A, (RatingCategory?)RatingCategory.C, 100_000m, -100_000m));
        linea.RatingCause.Should().Be(RatingCause.Arrears, "tiene línea en el base: no es crédito nuevo");

        var partes = await _c.Db.AccountingFactParts.AsNoTracking().Where(p => p.MonthlyCloseId == cierre.Id).OrderBy(p => p.Id).ToListAsync();
        partes.Select(p => p.FactType).Should().StartWith([CarteraReclasificadaPorCalificacionV1.Type], "la reclasificación se emite antes que el deterioro (T48)");
        var mensajes = await _c.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == CarteraReclasificadaPorCalificacionV1.Type || m.Type == DeterioroCalculadoV1.Type)
            .OrderBy(m => m.Id).ToListAsync();
        mensajes.Select(m => m.Type).Should().Equal(CarteraReclasificadaPorCalificacionV1.Type, DeterioroCalculadoV1.Type, DeterioroCalculadoV1.Type);
        partes.Select(p => p.BatchNumber).Distinct().Should().HaveCount(2, "el general va en la parte de una tanda propia, después de las de los créditos");

        var rcl = JsonSerializer.Deserialize<CarteraReclasificadaPorCalificacionV1>(mensajes[0].PayloadJson!, OpcionesDeMensajes.Opciones)!;
        var l = rcl.Lines.Should().ContainSingle().Subject;
        (l.PreviousDimensions!.RiskCategory, l.Dimensions.RiskCategory).Should().Be(("C", "A"));
        l.Amounts.Should().Contain(a => a.Concept == "Principal" && a.Amount == 3_000_000m && a.ReasonCode == null);
        l.Amounts.Should().Contain(a => a.Concept == "CurrentInterest" && a.Amount > 0m, "los intereses causados también van por categoría (C4)");
        l.Amounts.Should().Contain(a => a.Concept == "Principal" && a.Amount == 100_000m && a.ReasonCode == PartesDelCierre.Individual,
            "el deterioro individual viaja con su categoría (§11.2)");
        (rcl.Year, rcl.Month, rcl.CloseVersion, rcl.FactDate).Should().Be((2026, 12, (int?)1, CierreDePrueba.FinDeDiciembre));

        var det = JsonSerializer.Deserialize<DeterioroCalculadoV1>(mensajes[1].PayloadJson!, OpcionesDeMensajes.Opciones)!;
        det.Lines.Should().ContainSingle().Which.Amounts.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Concept = "Principal", Amount = 100_000m, ReasonCode = "DecreasePriorYears",
        }, o => o.ExcludingMissingMembers(), "frente al base no hubo gasto en el ejercicio: la disminución va a recuperación (US8-5, SC-013)");
        det.Lines[0].Dimensions.RiskCategory.Should().Be("A", "el delta va en la categoría nueva (§11.3)");
    }

    [Fact]
    public async Task Sin_cambio_de_letra_no_hay_reclasificacion_y_el_deterioro_igual_al_base_no_se_reconoce_otra_vez()
    {
        await _c.LineaDelBaseAsync(RatingCategory.A, deterioroDeCapital: 0m);

        var cierre = await _c.CerrarAsync(2026, 12);

        var tipos = await _c.Db.AccountingFactParts.AsNoTracking().Where(p => p.MonthlyCloseId == cierre.Id).Select(p => p.FactType).ToListAsync();
        tipos.Should().NotContain(CarteraReclasificadaPorCalificacionV1.Type);
        var det = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == DeterioroCalculadoV1.Type);
        JsonSerializer.Deserialize<DeterioroCalculadoV1>(det.PayloadJson!, OpcionesDeMensajes.Opciones)!.Lines
            .Should().BeEmpty("el deterioro individual no cambió frente al base: sólo sale el general");
    }

    [Fact]
    public async Task Un_credito_que_no_se_puede_calcular_deja_la_corrida_con_errores_sin_cierre_y_el_reintento_conserva_R()
    {
        var malo = _c.D.O.Credito(_c.D.O.Beto, 1_000_000m, "SINCOND-1");
        var orden = await _c.OrdenarAsync(2026, 12);

        var corrida = await _c.ConducirAsync(orden.Value.ProcessRunPublicId);

        corrida.Status.Should().Be(LendingProcessRunStatus.CompletedWithErrors);
        corrida.FailedLoans.Should().Be(1);
        (await _c.Db.MonthlyCloses.CountAsync(c => c.Kind == MonthlyCloseKind.Monthly)).Should().Be(0, "sin una línea por crédito vivo no hay cierre");
        var error = await _c.Db.LendingProcessRunErrors.AsNoTracking().SingleAsync(e => e.ProcessRunId == corrida.Id);
        (error.LoanId, error.Step).Should().Be(((int?)malo.Id, "CierreLineas"));

        var reintento = await new RetryLendingProcessRunCommandHandler(_c.Db, _c.G.Actor, _c.G.Reloj)
            .Handle(new RetryLendingProcessRunCommand(corrida.PublicId, "Se corrigieron las condiciones") { OperationKey = Guid.NewGuid() }, default);

        reintento.IsSuccess.Should().BeTrue();
        var releida = await _c.Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.Id == corrida.Id);
        releida.InformationCutoffAt.Should().Be(corrida.InformationCutoffAt, "el cierre conserva su R: sus líneas guardadas valen");
        (releida.CursorLoanId, releida.FailedLoans, releida.Status).Should().Be(((int?)null, 0, LendingProcessRunStatus.Requested));
    }

    [Fact]
    public async Task Si_Contabilidad_cerro_el_mes_la_primera_tanda_se_niega_con_el_codigo_del_requisito()
    {
        var orden = await _c.OrdenarAsync(2026, 12);
        _c.D.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Closed, new DateOnly(2027, 1, 1)))));

        var tanda = await _c.Ejecutar().Handle(new ExecuteMonthlyCloseCommand(orden.Value.ProcessRunPublicId), default);

        tanda.Error.Code.Should().Be(ErroresDeRiesgo.CodigoPeriodoContableCerrado, "la corrida revalida los requisitos al empezar (T47)");
        (await _c.Db.MonthlyCloseLoans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_una_tanda_por_credito_cada_tanda_avanza_el_cursor_y_la_ultima_cierra()
    {
        using var c = await CierreDePrueba.CrearAsync();
        var otro = await c.C.DesembolsarAsync();
        c.C.Tanda.TamanoDeTanda = 1;
        await c.CausarDiciembreAsync();

        var cierre = await c.CerrarAsync(2026, 12);

        cierre.LoanCount.Should().Be(2);
        var lineas = await c.Db.MonthlyCloseLoans.AsNoTracking().Where(l => l.ProcessRunId == cierre.ProcessRunId).OrderBy(l => l.LoanId).ToListAsync();
        lineas.Select(l => l.LoanId).Should().Equal(c.Credito.Id, otro.Id);
        lineas.Select(l => l.BatchNumber).Distinct().Should().HaveCount(2, "una tanda por crédito");
        lineas.Should().OnlyContain(l => l.FinalRating == RatingCategory.A);
    }
}
