using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Tests.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, US8, T0725 (FR-143, C63, C102; data-model §21.5; mensajes.md <c>ObligacionCreada</c>/<c>ObligacionRechazada</c>): de punta a punta con
/// la conciliación de la carga (<see cref="ConciliacionDeVentasDeMigracion"/>, versión 1) y una activación que la usó. Después de activar, el
/// consumidor de ventas de US6 recibe las <c>VentaACreditoRegistrada</c> acumuladas y responde según su fila de la versión aceptada —la migrada con
/// <c>ObligacionCreada</c> y el número migrado; la no migrada con saldo aceptada, <c>ObligacionRechazada</c> no reprocesable— y ninguna venta anterior
/// al corte se procesa en vivo; <c>EstadoDeValidacionAsync</c> informa la migrada como validada y la aceptada sin migrar como no incorporada. Los
/// casos sueltos (sin fila, ajustes hasta y después del corte, <c>SinObligacion</c>) los fijan <c>VentasAnterioresAlCorteTests</c> y
/// <c>AjusteDeVentaACreditoTests</c> (US6). (nuevo)
/// </summary>
public sealed class VentasDeLaMigracionEnElConsumidorTests : IAsyncLifetime
{
    private static readonly DateOnly Corte = VentasACreditoDePrueba.Hoy.AddDays(-30);
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync() => _v = await VentasACreditoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Tras_activar_el_consumidor_responde_cada_venta_acumulada_segun_su_conciliacion()
    {
        var migrada = _v.NuevaVenta(500_000m, fecha: Corte.AddDays(-10), numero: "PV01-MIG");
        var sinMigrar = _v.NuevaVenta(300_000m, fecha: Corte.AddDays(-5), numero: "PV01-SIN");

        // La carga aplicada y su conciliación (versión 1), con las ventas acumuladas sin consumir.
        var carga = new MigrationLoad { Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Applied, CreatedByUserId = _v.Cajera.Id, CurrentCheckVersion = 1 };
        _v.Db.MigrationLoads.Add(carga);
        _v.Db.SaveChanges();
        var parte = new MigrationLoadPart { MigrationLoadId = carga.Id, PartKey = "loans", BatchNumber = 1, FileName = "loans.xlsx", FileSha256 = "x",
            ImportedByUserId = _v.Cajera.Id, ImportedAt = _v.G.UtcNow, RowCount = 1 };
        _v.Db.MigrationLoadParts.Add(parte);
        _v.Db.SaveChanges();
        _v.Db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = "CR-MIG", PersonId = _v.Ana.Id,
            CreditLineCode = "CONLIB", InvoiceNumber = "PV01-MIG", DisbursementDate = migrada.Fecha, OriginalAmount = 500_000m });
        _v.Db.SaveChanges();
        var mensajeMigrada = await _v.EmitirAsync(migrada, VentasACreditoDePrueba.ContenidoV2(migrada));
        var mensajeSinMigrar = await _v.EmitirAsync(sinMigrar, VentasACreditoDePrueba.ContenidoV2(sinMigrar));
        var conciliada = await new ConciliacionDeVentasDeMigracion(_v.Db, new ConciliacionDeVentasDeLaCarga(_v.Db, new MensajesEntrantes(_v.Db)))
            .ConciliarAsync(await _v.Db.MigrationLoads.SingleAsync(), 1, default);
        conciliada.IsSuccess.Should().BeTrue();
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        (await _v.Db.LendingInboundMessages.CountAsync()).Should().Be(0, "conciliar no consume nada");

        // La activación: la diferencia de la no migrada se aceptó, la migrada nació con su número (la activación la enlaza a su fila).
        var diferencia = await _v.Db.MigrationLoadDifferences.SingleAsync();
        diferencia.Aceptar(_v.Cajera.Id, "Venta aceptada sin migrar", 1, _v.G.UtcNow);
        var credito = _v.O.Credito(_v.Ana, 500_000m, "CR-MIG");
        var filaMigrada = await _v.Db.MigrationSaleReconciliations.SingleAsync(r => r.InvoiceNumber == "PV01-MIG");
        filaMigrada.ResultingLoanId = credito.Id;
        var activacion = new LendingActivation
        {
            MigrationLoadId = carga.Id, CutoffDate = Corte, ActivationDate = Corte.AddDays(1), RegisteredByUserId = _v.Cajera.Id, RegisteredAt = _v.G.UtcNow.AddDays(-29),
            SessionItemId = 1, CheckVersion = 1, BalanceCheckSha256 = "x", SalesReconciliationSha256 = conciliada.Value.Huella,
        };
        _v.Db.LendingActivations.Add(activacion);
        await _v.Db.SaveChangesAsync();
        (await _v.Db.LendingSetups.SingleAsync()).ActivationId = activacion.Id;
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();

        var deLaMigrada = await _v.ConsumirAsync(mensajeMigrada);
        var deLaNoMigrada = await _v.ConsumirAsync(mensajeSinMigrar);

        deLaMigrada.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(credito.PublicId, null, "CR-MIG"));
        deLaNoMigrada.Should().BeEquivalentTo(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.NotIncorporated));
        var creada = JsonDocument.Parse((await _v.MensajesAsync(ObligacionCreadaV1.Type)).Single().PayloadJson).RootElement;
        (creada.GetProperty("status").GetString(), creada.GetProperty("loan").GetProperty("loanNumber").GetString()).Should().Be(("Migrated", "CR-MIG"));
        var rechazada = JsonDocument.Parse((await _v.MensajesAsync(ObligacionRechazadaV1.Type)).Single().PayloadJson).RootElement;
        (rechazada.GetProperty("motive").GetString(), rechazada.GetProperty("retryable").GetBoolean()).Should().Be(("AceptadaSinMigrar", false));
        (await _v.Db.Loans.CountAsync()).Should().Be(1, "ninguna venta anterior al corte se procesa en vivo");

        var consultas = _v.Consultas();
        (await consultas.EstadoDeValidacionAsync(mensajeMigrada, default)).Status.Should().Be(EstadosDeValidacion.Validated);
        (await consultas.EstadoDeValidacionAsync(mensajeSinMigrar, default)).Status.Should().Be(EstadosDeValidacion.NotIncorporated);
    }
}
