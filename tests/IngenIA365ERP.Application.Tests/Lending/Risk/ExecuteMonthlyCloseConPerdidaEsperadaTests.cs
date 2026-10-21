using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, K6a, T1019 (amplía <see cref="ExecuteMonthlyCloseCommandHandlerTests"/>; T47, T48; FR-099, FR-105, FR-106; US9-6, US9-7, US9-9): el cierre
/// de diciembre del crédito de Ana (consumo sin libranza) con la pérdida esperada. Obligada (perfil COOP_AHORRO_CREDITO, regla y Anexo 2 sembrados
/// vigentes): la línea lleva PI, PDI, exposición, puntaje, clave de la PI, tipo de garantía y la calificación contable homologada; el deterioro es
/// PI × PDI × exposición por concepto; el general también se calcula para la pérdida esperada (el documento dice que aplica); <c>DocumentsUsedJson</c>
/// incluye <c>SarcAnexo2</c> y los hechos son los mismos de K1. Con meses de historial faltantes, el cierre aplica la regla de meses faltantes y lista el
/// crédito (marca, JSON y reporte de cambios). Por adopción voluntaria la PI es la <c>VOLUNTARIA</c> desde su <c>ValidFrom</c>; antes, el Anexo 1.
/// </summary>
public class ExecuteMonthlyCloseConPerdidaEsperadaTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        await PerdidaEsperadaDePrueba.ReglaAsync(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1));
        PerdidaEsperadaDePrueba.Smmlv(_c.G);
        await _c.CausarDiciembreAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private static Anexo2 ElAnexo2(bool conVoluntaria = false) => Anexo2.Leer(PerdidaEsperadaDePrueba.Anexo2(conVoluntaria), 1).Documento;

    private async Task<int> PuntoAsync()
    {
        var punto = PerdidaEsperadaDePrueba.Acta(_c.Db, CierreDePrueba.Hoy, Guid.NewGuid());
        return (await _c.Db.SessionItems.AsNoTracking().SingleAsync(i => i.PublicId == punto)).Id;
    }

    private async Task AdopcionAsync(DateOnly desde, string segmentos)
    {
        _c.Db.ImpairmentModelElections.Add(new ImpairmentModelElection
        {
            Kind = ImpairmentModelElectionKind.VoluntaryAdoption, ValidFrom = desde, SegmentCodes = segmentos, SessionItemId = await PuntoAsync(),
            ImpactStudyAttachmentPublicId = Guid.NewGuid(), SupervisorNoticeDate = desde.AddDays(-30), SupervisorNoticeAttachmentPublicId = Guid.NewGuid(),
            NoticeLeadDays = 15, RegulatoryDocumentPublicId = Guid.NewGuid(), Reason = "Adopción del consejo", RegisteredByUserId = _c.Jefe.Id,
            RegisteredAt = _c.G.UtcNow.AddDays(-1),
        });
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Una_obligada_deteriora_por_perdida_esperada_con_PI_PDI_exposicion_y_la_homologada_y_los_mismos_hechos()
    {
        PerdidaEsperadaDePrueba.Perfil(_c.G, "COOP_AHORRO_CREDITO");
        await PerdidaEsperadaDePrueba.Anexo2Async(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1));

        var cierre = await _c.CerrarAsync(2026, 12);

        var l = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync(x => x.ProcessRunId == cierre.ProcessRunId);
        (l.ImpairmentModel, l.ModelBasis, l.ExpectedLossSegmentCode).Should().Be((ImpairmentModel.ExpectedLossAnnex2, ModelBasis.Obligated, "CONSUMO_SIN_LIBRANZA"));
        l.ProbabilityOfDefaultKey.Should().Be("COOP_AHORRO_CREDITO");
        l.ProbabilityOfDefault.Should().Be(ElAnexo2().TablaDePi("CONSUMO_SIN_LIBRANZA", "COOP_AHORRO_CREDITO")!.De(l.FinalRating), "la PI de la calificación final");
        l.LossGivenDefaultCollateralType.Should().Be("SIN_GARANTIA", "sin garantías ni codeudores: firma personal");
        l.LossGivenDefault.Should().Be(0.50m, "la PDI sin incumplimiento de «sin garantía» en consumo sin libranza (semilla)");
        (l.IsInDefault, l.DaysInDefault, l.IncompleteArrearsHistory).Should().Be(((bool?)false, (int?)0, (bool?)false));
        l.ExpectedLossScore.Should().NotBeNull().And.BeInRange(0m, 1m);
        l.ExpectedLossZ.Should().NotBeNull();
        l.ModelRating.Should().NotBeNull("la candidata del modelo entra a la calificación (FR-099)");
        l.ExposureAtDefault.Should().Be(l.PrincipalBalance + l.CurrentInterestBalance + l.DefaultInterestBalance + l.OtherBalance);
        l.PrincipalImpairment.Should().Be(decimal.Round(l.PrincipalBalance * l.ProbabilityOfDefault!.Value * l.LossGivenDefault!.Value, 2, MidpointRounding.AwayFromZero));
        l.TotalImpairment.Should().BeGreaterThan(0m);
        l.ExplanationJson.Should().Contain("PI =").And.Contain("PDI =").And.Contain("Puntaje");

        cierre.ModelBySegmentJson.Should().Contain("ExpectedLossAnnex2");
        (cierre.ObligatoryModelUnavailable, cierre.IncompleteHistory).Should().Be((false, false));
        JsonDocument.Parse(cierre.DocumentsUsedJson).RootElement.EnumerateArray().Select(d => d.GetProperty("kind").GetString())
            .Should().Contain(["SarcAnexo2", "ReglaDePerdidaEsperada"]);
        cierre.UnvalidatedParametersJson.Should().Contain("document:SarcAnexo2", "el Anexo 2 sembrado está pendiente de validar (E8)");
        (await _c.Db.MonthlyCloseGeneralImpairments.AsNoTracking().Where(g => g.MonthlyCloseId == cierre.Id).Select(g => g.ImpairmentModel).ToListAsync())
            .Should().Contain(ImpairmentModel.ExpectedLossAnnex2, "el Anexo 2 sembrado dice que el general aplica (pendiente de validar)");
        (await _c.Db.AccountingFactParts.AsNoTracking().Where(p => p.MonthlyCloseId == cierre.Id).Select(p => p.FactType).Distinct().ToListAsync())
            .Should().BeSubsetOf([DeterioroCalculadoV1.Type, CarteraReclasificadaPorCalificacionV1.Type], "K6a no trae hechos nuevos");
    }

    [Fact]
    public async Task Sin_Anexo_2_vigente_la_obligada_no_cierra()
    {
        PerdidaEsperadaDePrueba.Perfil(_c.G, "COOP_AHORRO_CREDITO");
        var orden = await _c.OrdenarAsync(2026, 12);

        var tanda = await _c.Ejecutar().Handle(new ExecuteMonthlyCloseCommand(orden.Value.ProcessRunPublicId), default);

        tanda.Error.Code.Should().Be(ErroresDeRiesgo.CodigoCierreSinNorma, "nunca un anexo supuesto (Principio IX)");
        tanda.Error.Message.Should().Contain("Anexo 2");
    }

    [Fact]
    public async Task Un_historial_incompleto_se_completa_con_la_regla_y_queda_listado_en_el_cierre_y_en_el_reporte_de_cambios()
    {
        PerdidaEsperadaDePrueba.Perfil(_c.G, "COOP_AHORRO_CREDITO");
        await PerdidaEsperadaDePrueba.Anexo2Async(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1));
        // El crédito como migrado con corte al 30 de noviembre y desembolsado en septiembre, sin historial de septiembre ni octubre.
        var carga = new MigrationLoad { Number = 9, CutoffDate = CierreDePrueba.CorteDeLaActivacion, Status = MigrationLoadStatus.Activated, CreatedByUserId = _c.Jefe.Id };
        _c.Db.MigrationLoads.Add(carga);
        await _c.Db.SaveChangesAsync();
        var credito = await _c.Db.Loans.SingleAsync(x => x.Id == _c.Credito.Id);
        credito.MigrationLoadId = carga.Id;
        credito.OpenedOn = new DateOnly(2026, 9, 15);
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();

        var cierre = await _c.CerrarAsync(2026, 12);

        var l = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync(x => x.ProcessRunId == cierre.ProcessRunId);
        l.IncompleteArrearsHistory.Should().BeTrue();
        l.ExplanationJson.Should().Contain("Faltan 2 meses").And.Contain("MayorObservada");
        cierre.IncompleteHistory.Should().BeTrue();
        cierre.IncompleteHistoryJson.Should().Contain(credito.Number).And.Contain("MayorObservada");

        var cambios = await new GetMonthlyCloseChangesQueryHandler(_c.Db, _c.G.Alcance).Handle(new GetMonthlyCloseChangesQuery(cierre.PublicId), default);
        cambios.Value.Items.Should().ContainSingle(x => x.LoanNumber == credito.Number).Which.IncompleteHistory.Should().BeTrue();
    }

    [Fact]
    public async Task Por_adopcion_voluntaria_la_PI_es_la_voluntaria_desde_su_fecha()
    {
        PerdidaEsperadaDePrueba.Perfil(_c.G, "COOP_MULTIACTIVA");
        await PerdidaEsperadaDePrueba.Anexo2Async(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1), conVoluntaria: true);
        await AdopcionAsync(new DateOnly(2026, 12, 1), "CONSUMO_LIBRANZA,CONSUMO_SIN_LIBRANZA,COMERCIAL_PN");

        var cierre = await _c.CerrarAsync(2026, 12);

        var l = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync(x => x.ProcessRunId == cierre.ProcessRunId);
        (l.ImpairmentModel, l.ModelBasis, l.ProbabilityOfDefaultKey).Should().Be((ImpairmentModel.ExpectedLossAnnex2, ModelBasis.Voluntary, "VOLUNTARIA"));
        l.ProbabilityOfDefault.Should().Be(ElAnexo2(conVoluntaria: true).TablaDePi("CONSUMO_SIN_LIBRANZA", "VOLUNTARIA")!.De(l.FinalRating));
        cierre.ModelBySegmentJson.Should().Contain("Voluntary");
    }

    [Fact]
    public async Task Una_adopcion_que_rige_despues_del_cierre_no_lo_cambia()
    {
        PerdidaEsperadaDePrueba.Perfil(_c.G, "COOP_MULTIACTIVA");
        await PerdidaEsperadaDePrueba.Anexo2Async(_c.Db, _c.G.UtcNow, new DateOnly(2026, 1, 1), conVoluntaria: true);
        await AdopcionAsync(new DateOnly(2027, 1, 1), "CONSUMO_SIN_LIBRANZA");

        var cierre = await _c.CerrarAsync(2026, 12);

        var l = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync(x => x.ProcessRunId == cierre.ProcessRunId);
        (l.ImpairmentModel, l.ProbabilityOfDefault).Should().Be((ImpairmentModel.ArrearsAnnex1, (decimal?)null), "la adopción rige desde enero: nunca hacia atrás");
    }
}
