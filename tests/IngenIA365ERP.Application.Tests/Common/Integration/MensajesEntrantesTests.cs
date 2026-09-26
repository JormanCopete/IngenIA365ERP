using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T497 (feature 012, US7; contracts/mensajes.md §4, §13): lo que recibe un consumidor. El sobre armado desde las columnas, el contenido
/// deserializado a su <c>…V1</c>, la huella verificada contra los bytes guardados y los datos de la entrega; un tipo fuera del catálogo
/// llega como <c>JsonElement</c> para que el destino lo rechace por versión.
/// </summary>
public class MensajesEntrantesTests
{
    private readonly BandejaDePrueba _b = new();

    [Fact]
    public async Task Lee_el_sobre_el_contenido_tipado_y_la_entrega_en_orden_de_emision()
    {
        var contenido = Encoding.UTF8.GetString(OpcionesDeMensajes.Serializar(new CompraRecibidaV1 { Operation = "CompraDirecta" }));
        var venta = Guid.NewGuid();
        var primero = _b.Mensaje(origen: venta, payload: contenido);
        var lote = _b.Lote(estado: BatchStatus.Running, numero: 4);
        _b.Entrega(primero, DeliveryStatus.InBatch, lote: lote, horario: "COMPRA|HoraDiaria|23:00|PorDocumento", intentos: 2);
        var segundo = _b.Mensaje(origen: venta, tipo: "Inexistente");
        _b.Entrega(segundo);

        var r = await new MensajesEntrantes(_b.Db).LeerAsync([segundo.PublicId, primero.PublicId], IntegrationDestinations.Accounting, default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Select(m => m.MessageId).Should().Equal(primero.PublicId, segundo.PublicId);
        var leido = r.Value[0];
        leido.Payload.Should().BeOfType<CompraRecibidaV1>().Which.Operation.Should().Be("CompraDirecta");
        leido.Envelope.Payload.Should().BeSameAs(leido.Payload);
        leido.Envelope.Origin.PublicId.Should().Be(venta);
        leido.Envelope.Origin.DocumentClass.Should().Be(DocumentClass.PurchaseReceipt);
        leido.Envelope.Origin.DocumentTypeCode.Should().Be("COMPRA");
        leido.Envelope.BranchPublicId.Should().Be(BandejaDePrueba.Sucursal);
        leido.Envelope.OriginUser.Name.Should().Be("Ana Compradora");
        leido.PrevalidationOutcome.Should().Be(PrevalidationOutcome.Postable);
        leido.Entrega.Should().Be(new EntregaEntrante(IntegrationDestinations.Accounting, DeliveryMode.Batch, DeliveryStatus.InBatch,
            "COMPRA|HoraDiaria|23:00|PorDocumento", null, lote.PublicId, 2, null));
        r.Value[1].Payload.Should().BeOfType<JsonElement>("un tipo fuera del catálogo lo rechaza el destino por versión");
    }

    [Fact]
    public async Task Un_contenido_alterado_no_se_entrega()
    {
        var m = _b.Mensaje(payload: "{\"operation\":\"CompraDirecta\"}");
        _b.Entrega(m);
        m.GetType().GetProperty(nameof(m.PayloadJson))!.SetValue(m, "{\"operation\":\"Otra\"}");
        await _b.Db.SaveChangesAsync();

        var r = await new MensajesEntrantes(_b.Db).LeerAsync([m.PublicId], IntegrationDestinations.Accounting, default);

        r.Error.Code.Should().Be("Integration.Message.PayloadAltered");
    }

    [Fact]
    public async Task Sin_entrega_a_ese_destino_es_NotFound()
    {
        var m = _b.Mensaje();
        _b.Entrega(m, destino: IntegrationDestinations.Lending, modo: DeliveryMode.Always);

        var r = await new MensajesEntrantes(_b.Db).LeerAsync([m.PublicId], IntegrationDestinations.Accounting, default);

        r.Error.Code.Should().Be("Integration.Message.NotFound");
    }
}
