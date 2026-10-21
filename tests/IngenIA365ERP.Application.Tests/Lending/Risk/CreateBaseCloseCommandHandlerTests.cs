using FluentAssertions;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0679 (T49, C60, C107; data-model §23.1–§23.5): el cierre base de la activación nace al corte con <c>Kind = Base</c>, versión
/// 1 y <c>ActivationId</c>; sus líneas salen de las filas de <c>LND_MigrationRiskData</c> —una por crédito desde US8 (T0734, plantillas.md §5.3.4)—
/// enlazadas al cierre (no a una corrida), con los saldos migrados por concepto; el general es la suma del <c>deterioroGeneral</c> de cada crédito
/// por la dimensión del crédito (oficina, modalidad, sub-segmento); los meses en E salen de la fecha de ingreso a E y el corte; las tandas
/// reemplazadas no cuentan; el historial de mora con origen <c>Migration</c>. Sin hechos ni partes (FR-140) y uno solo por cooperativa
/// (<c>BaseAlreadyExists</c>).
/// </summary>
public class CreateBaseCloseCommandHandlerTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;
    private LendingActivation _activacion = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync(conBase: false);
        var corte = CierreDePrueba.CorteDeLaActivacion;
        var carga = new MigrationLoad { Number = 1, CutoffDate = corte, Status = MigrationLoadStatus.Applied, CreatedByUserId = _c.Jefe.Id };
        _c.Db.MigrationLoads.Add(carga);
        await _c.Db.SaveChangesAsync();
        var parte = new MigrationLoadPart { MigrationLoadId = carga.Id, PartKey = "risk-data", BatchNumber = 1, FileName = "riesgo.xlsx", FileSha256 = new string('a', 64), ImportedByUserId = _c.Jefe.Id, ImportedAt = _c.G.UtcNow };
        _c.Db.MigrationLoadParts.Add(parte);
        await _c.Db.SaveChangesAsync();

        var credito = await _c.Db.Loans.SingleAsync(l => l.Id == _c.Credito.Id);
        credito.MigrationLoadId = carga.Id;
        var reemplazada = new MigrationLoadPart { MigrationLoadId = carga.Id, PartKey = "risk-data", BatchNumber = 2, FileName = "riesgo-viejo.xlsx",
            FileSha256 = new string('b', 64), ImportedByUserId = _c.Jefe.Id, ImportedAt = _c.G.UtcNow, Status = MigrationPartStatus.Superseded };
        _c.Db.MigrationLoadParts.Add(reemplazada);
        await _c.Db.SaveChangesAsync();
        _c.Db.MigrationRiskDataRows.AddRange(
            new MigrationRiskDataRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = credito.Number, DaysPastDue = 45, Rating = RatingCategory.B,
                Cause = RatingCause.Arrears, PrincipalImpairment = 30_000m, InterestImpairment = 1_000m, GeneralImpairment = 25_000m },
            // Una tanda reemplazada conserva sus filas pero no cuenta (C64).
            new MigrationRiskDataRow { MigrationLoadPartId = reemplazada.Id, RowNumber = 2, SourceLoanNumber = credito.Number, DaysPastDue = 400, Rating = RatingCategory.E,
                PrincipalImpairment = 2_900_000m, GeneralImpairment = 99_000m, EnteredEOn = new DateOnly(2025, 1, 1) });
        _c.Db.MigrationBalanceRows.AddRange(
            new MigrationBalanceRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = credito.Number, Concept = LoanConcept.Principal, Amount = 2_900_000m },
            new MigrationBalanceRow { MigrationLoadPartId = parte.Id, RowNumber = 3, SourceLoanNumber = credito.Number, Concept = LoanConcept.CurrentInterest, Amount = 40_000m });
        _c.Db.MigrationArrearsHistoryRows.Add(new MigrationArrearsHistoryRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = credito.Number,
            PeriodYear = 2026, PeriodMonth = 10, MaxDaysPastDue = 20, DaysPastDueAtMonthEnd = 15 });
        _activacion = new LendingActivation { MigrationLoadId = carga.Id, CutoffDate = corte, ActivationDate = corte.AddDays(1), RegisteredByUserId = _c.Jefe.Id, RegisteredAt = _c.G.UtcNow };
        _c.Db.LendingActivations.Add(_activacion);
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private CreateBaseCloseCommandHandler Handler() => new(_c.Db, new CierreBase(_c.Db, new LectorDeDocumentoNormativo(_c.Db), _c.G.Reloj));

    [Fact]
    public async Task Crea_el_base_al_corte_con_las_lineas_el_general_y_el_historial_migrados_sin_hechos()
    {
        var r = await Handler().Handle(new CreateBaseCloseCommand(_activacion.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var cierre = await _c.Db.MonthlyCloses.AsNoTracking().SingleAsync();
        cierre.Should().BeEquivalentTo(new
        {
            PublicId = r.Value, Kind = MonthlyCloseKind.Base, PeriodYear = (short)2026, PeriodMonth = (byte)11, Version = 1,
            CloseDate = CierreDePrueba.CorteDeLaActivacion, ActivationId = (int?)_activacion.Id, ProcessRunId = (int?)null, LoanCount = 1,
            TotalPrincipal = 2_900_000m, TotalIndividualImpairment = 31_000m,
        }, o => o.ExcludingMissingMembers());

        var linea = await _c.Db.MonthlyCloseLoans.AsNoTracking().SingleAsync();
        linea.Should().BeEquivalentTo(new
        {
            MonthlyCloseId = (int?)cierre.Id, ProcessRunId = (int?)null, LoanId = _c.Credito.Id, FinalRating = RatingCategory.B, ArrearsRating = RatingCategory.B,
            DaysPastDueAtClose = 45, PrincipalBalance = 2_900_000m, CurrentInterestBalance = 40_000m, PrincipalImpairment = 30_000m,
            InterestImpairment = 1_000m, TotalImpairment = 31_000m, RatingCause = RatingCause.Arrears, MonthsInE = (int?)null,
        }, o => o.ExcludingMissingMembers());

        // El general: la parte del crédito, por la dimensión del crédito (su oficina, su modalidad y su sub-segmento).
        var general = await _c.Db.MonthlyCloseGeneralImpairments.AsNoTracking().SingleAsync();
        (general.MonthlyCloseId, general.BranchId, general.ModalityCode, general.SubsegmentCode, general.TotalAmount, general.Delta).Should()
            .Be((cierre.Id, linea.BranchId, linea.ModalityCode, linea.SubsegmentCode, 25_000m, 25_000m));

        var historial = await _c.Db.LoanArrearsHistory.AsNoTracking().SingleAsync();
        (historial.LoanId, historial.PeriodMonth, historial.MaxDaysPastDue, historial.Origin).Should()
            .Be((_c.Credito.Id, (byte)10, 20, ArrearsHistoryOrigin.Migration));

        (await _c.Db.AccountingFactParts.CountAsync()).Should().Be(0, "el base no emite hechos: Contabilidad ya tenía esos saldos (FR-140)");
    }

    [Theory]
    [InlineData("2026-05-31", "2026-11-30", 6)]
    [InlineData("2026-06-15", "2026-11-30", 5)]
    [InlineData("2026-11-30", "2026-11-30", 0)]
    [InlineData(null, "2026-11-30", 0)]
    public void Los_meses_en_E_salen_de_la_fecha_de_ingreso_y_el_corte(string? ingreso, string corte, int meses) =>
        CierreBase.MesesEnE(ingreso is null ? null : DateOnly.Parse(ingreso, System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.Parse(corte, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(meses);

    [Fact]
    public async Task Un_segundo_base_responde_BaseAlreadyExists()
    {
        (await Handler().Handle(new CreateBaseCloseCommand(_activacion.PublicId), default)).IsSuccess.Should().BeTrue();

        var otra = await Handler().Handle(new CreateBaseCloseCommand(_activacion.PublicId), default);

        otra.Error.Code.Should().Be(ErroresDeRiesgo.CodigoBaseYaExiste);
        (await _c.Db.MonthlyCloses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Una_activacion_que_no_existe_responde_NotFound()
    {
        (await Handler().Handle(new CreateBaseCloseCommand(Guid.NewGuid()), default)).Error.Code.Should().Be("Lending.Activation.NotFound");
    }
}
