using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T467 (feature 012, US7; contracts/contabilidad.md §5.4; Principios III y X): el ciclo del lote. Iniciar pasa <c>Requested</c> a
/// <c>Running</c> una sola vez; cerrar lee los totales de las entregas del lote, lo deja <c>Completed</c>, <c>CompletedWithRejections</c>
/// o <c>Empty</c>, emite <c>Accounting.Inventory.BatchProcessed</c> y no vuelve a cerrar lo cerrado. Los dos se reintentan enteros ante
/// un choque de <c>RowVersion</c> y tienen validador.
/// </summary>
public class CicloDelLoteDeIntegracionTests
{
    private readonly BandejaDePrueba _b = new();
    private readonly IAuditAppendOnlyWriter _auditoria = Substitute.For<IAuditAppendOnlyWriter>();
    private readonly List<AuditEventDocument> _eventos = [];

    public CicloDelLoteDeIntegracionTests()
    {
        _auditoria.AppendAsync(Arg.Do<AuditEventDocument>(_eventos.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    private StartIntegrationBatchCommandHandler Inicio() => new(_b.Db, _b.Reloj);

    private CloseIntegrationBatchCommandHandler Cierre() => new(_b.Db, _b.Reloj,
        new AccountingAuditEmitter(_auditoria, Substitute.For<ICurrentUserService>(), _b.Reloj, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual));

    [Fact]
    public async Task Iniciar_pasa_de_Requested_a_Running_una_sola_vez()
    {
        var lote = _b.Lote();

        (await Inicio().Handle(new StartIntegrationBatchCommand(lote.PublicId), default)).Value.Should().BeTrue();
        (await Inicio().Handle(new StartIntegrationBatchCommand(lote.PublicId), default)).Value.Should().BeFalse("otra réplica ya lo arrancó");

        _b.Olvidar();
        var leido = await _b.Db.IntegrationBatches.SingleAsync();
        leido.Status.Should().Be(BatchStatus.Running);
        leido.StartedAt.Should().Be(BandejaDePrueba.Ahora);
    }

    [Fact]
    public async Task Iniciar_un_lote_que_no_existe_responde_NotFound()
    {
        (await Inicio().Handle(new StartIntegrationBatchCommand(Guid.NewGuid()), default)).Error.Code.Should().Be("Integration.Batch.NotFound");
    }

    [Fact]
    public async Task Cerrar_lee_los_totales_de_las_entregas_y_emite_BatchProcessed()
    {
        var lote = _b.Lote(estado: BatchStatus.Running, numero: 12);
        var comprobante = Guid.NewGuid();
        var venta = Guid.NewGuid();
        foreach (var tipo in new[] { "VentaFacturada", "CostoDeVentaReconocido" })
        {
            var e = _b.Entrega(_b.Mensaje(origen: venta, tipo: tipo), DeliveryStatus.Processed, lote: lote);
            e.ResultReference = comprobante.ToString("D");
            e.ResultVoucherTypeCode = "FV";
            e.ResultVoucherNumber = "FV-9";
        }

        var informativo = _b.Entrega(_b.Mensaje(tipo: "SaldoInicialCargado"), DeliveryStatus.Processed, lote: lote);
        informativo.ResultReference = ReferenciasDeResultado.Informativo;
        await _b.Db.SaveChangesAsync();

        var r = await Cierre().Handle(new CloseIntegrationBatchCommand(lote.PublicId, 1500m, 1500m), default);

        r.Value.Should().BeTrue();
        _b.Olvidar();
        var cerrado = await _b.Db.IntegrationBatches.SingleAsync();
        cerrado.Status.Should().Be(BatchStatus.Completed);
        cerrado.MessageCount.Should().Be(3);
        cerrado.DocumentCount.Should().Be(2);
        cerrado.ProcessedCount.Should().Be(3);
        cerrado.RejectedCount.Should().Be(0);
        cerrado.VoucherCount.Should().Be(1);
        cerrado.TotalDebit.Should().Be(1500m);
        cerrado.FinishedAt.Should().Be(BandejaDePrueba.Ahora);
        cerrado.ResultSummaryJson.Should().Contain("FV-9");
        _eventos.Should().ContainSingle().Which.Should().Match<AuditEventDocument>(e =>
            e.Action == "Accounting.Inventory.BatchProcessed" && e.EntityPublicId == lote.PublicId.ToString() && e.TenantId == CooperativaDePrueba.PublicIdN);
    }

    [Fact]
    public async Task Con_rechazos_queda_CompletedWithRejections()
    {
        var lote = _b.Lote(estado: BatchStatus.Running);
        _b.Entrega(_b.Mensaje(), DeliveryStatus.Processed, lote: lote);
        var rechazada = _b.Entrega(_b.Mensaje(numero: "CO-7"), DeliveryStatus.Rejected, lote: lote);
        rechazada.LastErrorCode = "Accounting.InventoryRule.Missing";
        await _b.Db.SaveChangesAsync();

        await Cierre().Handle(new CloseIntegrationBatchCommand(lote.PublicId), default);

        var cerrado = await _b.Db.IntegrationBatches.SingleAsync();
        cerrado.Status.Should().Be(BatchStatus.CompletedWithRejections);
        cerrado.RejectedCount.Should().Be(1);
        cerrado.ResultSummaryJson.Should().Contain("Accounting.InventoryRule.Missing").And.Contain("CO-7");
    }

    [Fact]
    public async Task Sin_mensajes_queda_Empty()
    {
        var lote = _b.Lote(estado: BatchStatus.Running);

        await Cierre().Handle(new CloseIntegrationBatchCommand(lote.PublicId), default);

        (await _b.Db.IntegrationBatches.SingleAsync()).Status.Should().Be(BatchStatus.Empty);
    }

    [Fact]
    public async Task Un_lote_cerrado_no_se_vuelve_a_cerrar()
    {
        var lote = _b.Lote(estado: BatchStatus.Running);
        await Cierre().Handle(new CloseIntegrationBatchCommand(lote.PublicId), default);

        var r = await Cierre().Handle(new CloseIntegrationBatchCommand(lote.PublicId), default);

        r.Value.Should().BeFalse();
        _eventos.Should().ContainSingle();
    }

    [Fact]
    public async Task Con_entregas_por_procesar_el_lote_sigue_en_curso()
    {
        var lote = _b.Lote(estado: BatchStatus.Running);
        _b.Entrega(_b.Mensaje(), DeliveryStatus.InBatch, lote: lote);

        var r = await Cierre().Handle(new CloseIntegrationBatchCommand(lote.PublicId), default);

        r.Error.Code.Should().Be("Integration.Batch.HasPendingDeliveries");
        (await _b.Db.IntegrationBatches.SingleAsync()).Status.Should().Be(BatchStatus.Running);
    }

    [Fact]
    public async Task Un_lote_que_no_corrio_no_se_cierra()
    {
        var lote = _b.Lote();

        (await Cierre().Handle(new CloseIntegrationBatchCommand(lote.PublicId), default)).Error.Code.Should().Be("Integration.Batch.NotRunning");
    }

    [Fact]
    public void Los_dos_se_reintentan_ante_un_choque_y_tienen_validador()
    {
        typeof(IReintentableAnteConcurrencia).IsAssignableFrom(typeof(StartIntegrationBatchCommand)).Should().BeTrue();
        typeof(IReintentableAnteConcurrencia).IsAssignableFrom(typeof(CloseIntegrationBatchCommand)).Should().BeTrue();
        new StartIntegrationBatchCommandValidator().Validate(new StartIntegrationBatchCommand(Guid.Empty)).IsValid.Should().BeFalse();
        new CloseIntegrationBatchCommandValidator().Validate(new CloseIntegrationBatchCommand(Guid.NewGuid(), -1m)).IsValid.Should().BeFalse();
    }
}
