using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T518 (api.md §26.4 y §26.5): los lotes vistos desde Contabilidad (lista con <c>late</c>; detalle con documentos,
/// comprobantes y rechazos con quién corrige) y los recibos de contabilización (el vínculo documento ↔ comprobante), sin exponer
/// el <c>Id</c> interno.
/// </summary>
public class InventoryBatchQueriesTests
{
    private readonly EscenarioContable E = new();

    private async Task ProcesarAsync(IReadOnlyList<MensajeDeUnidad> unidad, Guid? lote)
    {
        var handler = new PostInventoryMessagesCommandHandler(E.D.Db, new MensajesEntrantes(E.D.Db),
            new ConsumoDeInventario(E.D.Db, E.ActorActual, E.D.Clock, E.Emisor), E.D.Poster, new ResolutorDeReglas(E.D.Db),
            new TiposDeComprobanteDeInventario(E.D.Db), E.D.Clock);
        await handler.Handle(new PostInventoryMessagesCommand(unidad.Select(m => m.Sobre.MessageId).ToList(), lote), default);
        E.D.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task El_detalle_del_lote_trae_documentos_comprobantes_y_rechazos()
    {
        var lote = E.Lote(4);
        var bueno = EscenarioContable.Compra(1000m, numero: "REC-1");
        var malo = EscenarioContable.Compra(300m, "CARNES", numero: "REC-2");
        E.Emitir(bueno, DeliveryStatus.InBatch, lote);
        var mensajeMalo = E.Emitir(malo, DeliveryStatus.InBatch, lote)[0];
        await ProcesarAsync(bueno, lote.PublicId);
        var entrega = await E.D.Db.IntegrationMessageDeliveries.SingleAsync(d => d.MessageId == mensajeMalo.Id);
        entrega.Status = DeliveryStatus.Rejected;
        entrega.LastErrorCode = "Accounting.InventoryRule.Missing";
        entrega.LastErrorMessage = "Sin regla";
        E.D.Db.SaveChanges();

        var r = await new GetInventoryBatchQueryHandler(E.D.Db, E.D.Clock).Handle(new GetInventoryBatchQuery(lote.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Batch.Number.Should().Be(4);
        r.Value.Documents.Select(d => d.Number).Should().Equal("REC-1", "REC-2");
        r.Value.Documents[0].Total.Should().Be(1000m);
        var comprobante = r.Value.Vouchers.Should().ContainSingle().Subject;
        comprobante.VoucherTypeCode.Should().Be("EI");
        comprobante.Granularity.Should().Be(PostingGranularity.PerDocument);
        comprobante.DocumentsCount.Should().Be(1);
        var rechazo = r.Value.Rejected.Should().ContainSingle().Subject;
        rechazo.MessagePublicId.Should().Be(malo[0].Sobre.MessageId);
        rechazo.WhoFixes!.Page.Should().Be("/contabilidad/inventario/matriz");
    }

    [Fact]
    public async Task Un_lote_programado_vencido_sale_atrasado()
    {
        var atrasado = new IntegrationBatch
        {
            Number = 9, Destination = IntegrationDestinations.Accounting, Trigger = BatchTrigger.Scheduled, ScheduleKey = "CO|HoraDiaria|22:00|Resumido",
            ScheduledFor = new DateTime(2026, 3, 19, 22, 0, 0), RequestedByKind = ActorKind.Process, RequestedAt = ContabilidadTestData.Ahora, CreatedBy = "test",
        };
        E.D.Db.IntegrationBatches.Add(atrasado);
        E.D.Db.SaveChanges();
        E.D.Clock.AhoraLocal.Returns(new DateTimeOffset(2026, 3, 20, 9, 0, 0, TimeSpan.FromHours(-5)));

        var r = await new ListInventoryBatchesQueryHandler(E.D.Db, E.D.Clock).Handle(new ListInventoryBatchesQuery(Trigger: BatchTrigger.Scheduled), default);

        r.Value.Items.Should().ContainSingle().Which.Late.Should().BeTrue();
    }

    [Fact]
    public async Task Los_recibos_de_un_documento_llevan_su_comprobante()
    {
        var compra = EscenarioContable.Compra(1000m, numero: "REC-1");
        E.Emitir(compra);
        await ProcesarAsync(compra, null);

        var r = await new ListInventoryPostingsQueryHandler(E.D.Db).Handle(new ListInventoryPostingsQuery(Document: compra[0].Sobre.Origin.PublicId), default);

        var recibo = r.Value.Items.Should().ContainSingle().Subject;
        recibo.MessagePublicId.Should().Be(compra[0].Sobre.MessageId);
        recibo.Voucher.Should().Be(new ComprobanteDelReciboDto("EI", 1));
        recibo.AccountingDocumentPublicId.Should().NotBeNull();
        recibo.WithoutVoucher.Should().BeNull();
        recibo.Actor.Name.Should().Be("Proceso de integración");
        (await new ListInventoryPostingsQueryHandler(E.D.Db).Handle(new ListInventoryPostingsQuery(Message: Guid.NewGuid()), default)).Value.Items.Should().BeEmpty();
    }
}
