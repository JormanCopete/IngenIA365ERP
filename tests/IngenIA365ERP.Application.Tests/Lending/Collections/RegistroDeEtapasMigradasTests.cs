using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Lending.Collections.Stages;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Collections;

/// <summary>
/// El registro de la etapa migrada al desplegar K4 (feature 013, K4, T0930; C64, T0944): cada crédito vivo que vino de una carga con su
/// <c>CollectionStageCode</c> recibe su primera entrada <c>Source = Migration</c> —por el código de la etapa; si no casa, por sus días de mora a la
/// fecha—, la corrida es idempotente (repetirla no registra dos veces la etapa de un crédito), deja el resumen con la tabla de equivalencia de códigos, y los
/// que no tenían etapa quedan para el ciclo. (nuevo)
/// </summary>
public sealed class RegistroDeEtapasMigradasTests : IAsyncLifetime
{
    private readonly CobranzaDePrueba _c = new();
    private MigrationLoad _carga = null!;
    private MigrationLoadPart _parte = null!;
    private int _fila;

    public async Task InitializeAsync()
    {
        await _c.SembrarEtapasAsync();
        _carga = new MigrationLoad { Number = 1, CutoffDate = _c.Hoy.AddDays(-10), Status = MigrationLoadStatus.Applied, CreatedByUserId = _c.Gestor.Id };
        _c.Db.MigrationLoads.Add(_carga);
        await _c.Db.SaveChangesAsync();
        _parte = new MigrationLoadPart
        {
            MigrationLoadId = _carga.Id, PartKey = "loans", BatchNumber = 1, Status = MigrationPartStatus.Imported, FileName = "creditos.xlsx",
            FileSha256 = new string('a', 64), RowCount = 3, ImportedByUserId = _c.Gestor.Id, ImportedAt = _c.G.UtcNow,
        };
        _c.Db.MigrationLoadParts.Add(_parte);
        await _c.Db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Loan> MigradoAsync(string numero, string? codigoDeEtapa, int diasDeMora)
    {
        var deudor = _c.G.Persona("Mig" + numero, "Migrado", "9" + numero.GetHashCode().ToString("X").PadLeft(8, '0'));
        var credito = _c.E.NuevoCredito(numero, MensajesDePrueba.Norte, deudor);
        credito.MigrationLoadId = _carga.Id;
        _c.E.Posicion(credito, diasDeMora, _c.Hoy);
        _c.Db.MigrationLoanRows.Add(new MigrationLoanRow
        {
            MigrationLoadPartId = _parte.Id, RowNumber = ++_fila, SourceLoanNumber = numero, PersonId = deudor.Id, CreditLineCode = "CONSUMO",
            OfficePublicId = Guid.NewGuid(), DisbursementDate = new DateOnly(2025, 1, 10), OriginalAmount = 1_000_000m, CollectionStageCode = codigoDeEtapa,
        });
        await _c.Db.SaveChangesAsync();
        return credito;
    }

    private async Task<LendingProcessRun> NuevaCorridaAsync()
    {
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.MigratedCollectionStages, ProcessDate = _c.Hoy, Status = LendingProcessRunStatus.Requested, RequestedByKind = ActorKind.Process,
            ExecutionOrigin = "Tarea:cartera.etapas-migradas-k4",
        };
        _c.Db.LendingProcessRuns.Add(corrida);
        await _c.Db.SaveChangesAsync();
        return corrida;
    }

    private RegisterMigratedCollectionStagesCommandHandler Manejador() => new(_c.Db, _c.G.Auditoria, _c.G.Reloj);

    /// <summary>Corre las tandas hasta que la corrida termina (como lo hace el ejecutor).</summary>
    private async Task<LendingProcessRun> CorrerAsync()
    {
        var corrida = await NuevaCorridaAsync();
        for (var i = 0; i < 5; i++)
        {
            var r = await Manejador().Handle(new RegisterMigratedCollectionStagesCommand(corrida.PublicId), default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
            if (r.Value == AvanceDeCorrida.Terminada) break;
        }
        _c.Db.ChangeTracker.Clear();
        return await _c.Db.LendingProcessRuns.AsNoTracking().SingleAsync(r => r.Id == corrida.Id);
    }

    private static TotalesDeEtapasMigradas Totales(LendingProcessRun corrida) =>
        JsonSerializer.Deserialize<TotalesDeEtapasMigradas>(corrida.TotalsJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    // ----------------------------------------------------------------------------------------------------------- registro --

    [Fact]
    public async Task El_credito_con_codigo_de_etapa_recibe_su_primera_entrada_de_migracion_en_la_fecha_de_corte()
    {
        var credito = await MigradoAsync("M-001", "ADMIN", 12);

        var corrida = await CorrerAsync();

        corrida.Status.Should().Be(LendingProcessRunStatus.Completed);
        var entrada = await _c.Db.CollectionStageHistory.AsNoTracking().SingleAsync(h => h.LoanId == credito.Id);
        (entrada.StageCode, entrada.Source, entrada.EnteredOn, entrada.ProcessRunId).Should()
            .Be(("ADMIN", CollectionStageHistoryEntry.FuenteMigracion, _carga.CutoffDate, corrida.Id));
        entrada.Reason.Should().Contain("ADMIN");
        Totales(corrida).Registered.Should().Be(1);
    }

    [Fact]
    public async Task El_codigo_que_no_casa_se_resuelve_por_los_dias_de_mora_y_queda_en_la_equivalencia()
    {
        var credito = await MigradoAsync("M-002", "COBROPRE", 40);

        var corrida = await CorrerAsync();

        var entrada = await _c.Db.CollectionStageHistory.AsNoTracking().SingleAsync(h => h.LoanId == credito.Id);
        entrada.StageCode.Should().Be("PREJURID");
        var totales = Totales(corrida);
        (totales.Registered, totales.MatchedByDays).Should().Be((1, 1));
        totales.Equivalence.Should().ContainKey("COBROPRE").WhoseValue.Should().Be("PREJURID");
    }

    [Fact]
    public async Task El_que_no_tenia_etapa_no_se_registra_y_entra_por_el_ciclo()
    {
        var credito = await MigradoAsync("M-003", null, 20);

        var corrida = await CorrerAsync();

        (await _c.Db.CollectionStageHistory.CountAsync(h => h.LoanId == credito.Id)).Should().Be(0);
        Totales(corrida).WithoutCode.Should().Be(1);
    }

    [Fact]
    public async Task El_codigo_sin_etapa_ni_mora_que_lo_resuelva_se_cuenta_como_sin_equivalencia()
    {
        // Mora de -50 días: ninguna etapa vigente cubre ese rango y el código tampoco existe.
        var credito = await MigradoAsync("M-004", "RARA", -50);

        var corrida = await CorrerAsync();

        (await _c.Db.CollectionStageHistory.CountAsync(h => h.LoanId == credito.Id)).Should().Be(0);
        var totales = Totales(corrida);
        totales.Unmatched.Should().Be(1);
        totales.Equivalence.Should().ContainKey("RARA").WhoseValue.Should().BeEmpty();
    }

    [Fact]
    public async Task Repetir_la_corrida_no_duplica_la_etapa_de_ningun_credito()
    {
        var a = await MigradoAsync("M-005", "ADMIN", 10);
        var b = await MigradoAsync("M-006", "JURIDICA", 80);
        await CorrerAsync();

        var segunda = await CorrerAsync();

        (await _c.Db.CollectionStageHistory.CountAsync(h => h.Source == CollectionStageHistoryEntry.FuenteMigracion)).Should().Be(2);
        (await _c.Db.CollectionStageHistory.CountAsync(h => h.LoanId == a.Id)).Should().Be(1);
        (await _c.Db.CollectionStageHistory.CountAsync(h => h.LoanId == b.Id)).Should().Be(1);
        var totales = Totales(segunda);
        (totales.Registered, totales.AlreadyRegistered).Should().Be((0, 2));
    }

    [Fact]
    public async Task Un_credito_que_no_vino_de_una_carga_no_entra_a_la_corrida()
    {
        var propio = _c.E.NuevoCredito("P-001", MensajesDePrueba.Norte, _c.Ana);
        _c.E.Posicion(propio, 15, _c.Hoy);
        await _c.Db.SaveChangesAsync();

        var corrida = await CorrerAsync();

        (await _c.Db.CollectionStageHistory.CountAsync()).Should().Be(0);
        corrida.Status.Should().Be(LendingProcessRunStatus.Completed);
    }

    [Fact]
    public async Task Una_corrida_de_otra_clase_o_inexistente_se_rechaza()
    {
        var otra = new LendingProcessRun { Kind = LendingProcessKind.DailyCycle, ExecutionOrigin = "x", RequestedByKind = ActorKind.Process };
        _c.Db.LendingProcessRuns.Add(otra);
        await _c.Db.SaveChangesAsync();

        var r = await Manejador().Handle(new RegisterMigratedCollectionStagesCommand(otra.PublicId), default);
        var nada = await Manejador().Handle(new RegisterMigratedCollectionStagesCommand(Guid.NewGuid()), default);

        (r.IsFailure, nada.IsFailure).Should().Be((true, true));
    }

    // ------------------------------------------------------------------------------------------------------ la tarea --

    [Fact]
    public async Task La_tarea_de_una_sola_vez_pide_la_corrida_si_hay_migrados_y_no_la_pide_dos_veces()
    {
        await MigradoAsync("M-007", "ADMIN", 10);
        var tarea = new ScheduleMigratedCollectionStagesCommandHandler(_c.Db, _c.G.Reloj);

        var primera = await tarea.Handle(new ScheduleMigratedCollectionStagesCommand(), default);
        var segunda = await tarea.Handle(new ScheduleMigratedCollectionStagesCommand(), default);

        (primera.Value, segunda.Value).Should().Be((true, false));
        (await _c.Db.LendingProcessRuns.CountAsync(r => r.Kind == LendingProcessKind.MigratedCollectionStages)).Should().Be(1);
    }

    [Fact]
    public async Task La_tarea_no_hace_nada_si_no_hay_creditos_migrados_ni_Cartera_activada()
    {
        var sinMigrados = await new ScheduleMigratedCollectionStagesCommandHandler(_c.Db, _c.G.Reloj).Handle(new ScheduleMigratedCollectionStagesCommand(), default);
        sinMigrados.Value.Should().BeFalse();

        using var sinActivar = new CobranzaDePrueba(activada: false);
        var r = await new ScheduleMigratedCollectionStagesCommandHandler(sinActivar.Db, sinActivar.G.Reloj).Handle(new ScheduleMigratedCollectionStagesCommand(), default);
        r.Value.Should().BeFalse();
    }
}
