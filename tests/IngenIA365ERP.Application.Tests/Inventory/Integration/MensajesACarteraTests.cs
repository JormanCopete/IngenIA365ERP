using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 012, I3, T648 (contracts/mensajes.md §8, §9; T32): los mensajes a Cartera del crédito provisional. <b>Una</b>
/// <c>VentaACreditoRegistrada</c> por pago de crédito con clave <c>Confirmation:{pago:N}</c> y entrega <c>Lending</c> / <c>Always</c> /
/// <c>Pending</c>, las condiciones del medio, sin línea de Cartera, la sugerida, «pendiente de validar», origen provisional y la aprobación de
/// quien aprobó (nunca el cajero); <c>VentaFacturada</c> lleva el pago de crédito con el cliente como tercero y la misma marca. Las notas y la
/// anulación emiten <c>AjusteDeVentaACredito</c> (<c>CreditNote</c>, <c>Return</c>, <c>Voiding</c>) con monto negativo, el mensaje original,
/// el mismo sello y dependencia hacia él; una nota que reintegra sólo por medios de contado no emite ajuste.
/// </summary>
public class MensajesACarteraTests
{
    /// <summary>Venta de 120.000 al asociado X (60 × P1), a crédito (o mixta), aprobada por el supervisor.</summary>
    private static async Task<InventoryDocument> VentaAprobadaAsync(CreditoDePrueba c, decimal? efectivo = null)
    {
        var venta = await c.VentaAsync(c.AsociadoX,
            total => efectivo is { } e ? [c.Credito(c.CredAsoc, total - e), c.V.Pago(c.V.Efectivo, e)] : [c.Credito(c.CredAsoc, total)],
            c.V.Linea(c.V.P1, 60m));
        var r = await c.ConfirmarAsync(venta);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var aprobada = await c.AprobarAsync(c.SolicitudDeCredito(venta), CreditoDePrueba.Supervisor);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? aprobada.Error.Message : string.Empty);
        c.Db.ChangeTracker.Clear();
        c.ComoUsuario(CreditoDePrueba.Cajera);
        return c.V.Documento(venta);
    }

    private static List<Domain.Entities.Integration.Transactions.IntegrationMessage> Mensajes(CreditoDePrueba c, Guid origen, string tipo) =>
        c.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == origen && m.Type == tipo).OrderBy(m => m.Id).ToList();

    private static async Task<InventoryDocument> NotaAsync(CreditoDePrueba c, InventoryDocument venta, bool devolucion,
        IReadOnlyList<DocumentPaymentInput>? reintegros = null)
    {
        var linea = venta.Lines.Single(l => !l.IsDeleted);
        var nota = await c.V.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Ajuste al cliente", false, devolucion,
            [devolucion ? new CreditNoteLineInput(linea.PublicId, 10m) : new CreditNoteLineInput(linea.PublicId, Amount: 20_000m)], reintegros ?? []));
        nota.IsSuccess.Should().BeTrue(nota.IsFailure ? nota.Error.Message : string.Empty);
        var r = await c.ConfirmarAsync(nota.Value.PublicId);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        c.Db.ChangeTracker.Clear();
        return c.V.Documento(nota.Value.PublicId);
    }

    [Fact]
    public async Task Una_VentaACreditoRegistrada_por_pago_de_credito_hacia_Cartera_pendiente()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaAprobadaAsync(c);
        var pago = c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == venta.Id && !p.IsDeleted);

        var mensaje = Mensajes(c, venta.PublicId, VentaACreditoRegistradaV1.Type).Should().ContainSingle().Subject;
        mensaje.OriginEventKey.Should().Be(ClavesDeEvento.ConfirmacionPor(pago.PublicId));
        mensaje.PersonPublicId.Should().Be(c.AsociadoX.PublicId);
        var entrega = c.Db.IntegrationMessageDeliveries.AsNoTracking().Single(e => e.MessageId == mensaje.Id);
        (entrega.Destination, entrega.Mode, entrega.Status).Should().Be((IntegrationDestinations.Lending, DeliveryMode.Always, DeliveryStatus.Pending));
        c.Db.IntegrationDeliveryAttempts.AsNoTracking().Should().BeEmpty("mientras IC esté pendiente nadie intenta entregarlo");

        var contenido = JsonNode.Parse(mensaje.PayloadJson)!;
        contenido["thirdPartyKind"]!.GetValue<string>().Should().Be("Associate");
        contenido["creditPayment"]!["paymentPublicId"]!.GetValue<Guid>().Should().Be(pago.PublicId);
        contenido["creditPayment"]!["amount"]!.GetValue<decimal>().Should().Be(120_000m);
        contenido["terms"]!["term"]!.GetValue<int>().Should().Be(30);
        contenido["terms"]!["installments"]!.GetValue<int>().Should().Be(1);
        contenido["terms"]!["periodicity"]!.GetValue<string>().Should().Be("SinglePayment");
        contenido["creditLineCode"].Should().BeNull();
        contenido["suggestedCreditLineCode"]!.GetValue<string>().Should().Be("CONSUMO");
        contenido["pendingValidation"]!.GetValue<bool>().Should().BeTrue();
        contenido["origin"]!.GetValue<string>().Should().Be("ProvisionalCredit");
        contenido["accountsReceivableRecordedBy"]!.GetValue<string>().Should().Be("Contabilidad");
        contenido["approval"]!["approvedBy"]!["name"]!.GetValue<string>().Should().Be("supervisor", "aprueba el supervisor, nunca el cajero");
    }

    [Fact]
    public async Task VentaFacturada_lleva_el_pago_de_credito_con_el_cliente_como_tercero()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaAprobadaAsync(c, efectivo: 20_000m);

        var contenido = JsonNode.Parse(c.V.ContenidoDe(venta.PublicId, VentaFacturadaV1.Type))!;
        var pagos = contenido["payments"]!.AsArray();
        var credito = pagos.Single(p => p!["paymentMeansCode"]!.GetValue<string>() == "CREDASOC")!;
        credito["thirdPartyPersonPublicId"]!.GetValue<Guid>().Should().Be(c.AsociadoX.PublicId);
        credito["pendingValidation"]!.GetValue<bool>().Should().BeTrue();
        credito["amount"]!.GetValue<decimal>().Should().Be(100_000m);
        Mensajes(c, venta.PublicId, VentaACreditoRegistradaV1.Type).Should().ContainSingle("el efectivo no es crédito");
    }

    [Theory]
    [InlineData(false, "CreditNote", 20_000)]
    [InlineData(true, "Return", 20_000)]
    public async Task La_nota_ajusta_el_credito_con_monto_negativo_y_depende_del_original(bool devolucion, string clase, int monto)
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaAprobadaAsync(c);
        var original = Mensajes(c, venta.PublicId, VentaACreditoRegistradaV1.Type).Single();
        var pago = c.Db.DocumentPayments.AsNoTracking().Single(p => p.DocumentId == venta.Id && !p.IsDeleted);

        var nota = await NotaAsync(c, venta, devolucion);

        var ajuste = Mensajes(c, nota.PublicId, AjusteDeVentaACreditoV1.Type).Should().ContainSingle().Subject;
        ajuste.OriginEventKey.Should().Be(ClavesDeEvento.ConfirmacionPor(pago.PublicId));
        ajuste.RelatedPublicId.Should().Be(venta.PublicId);
        var contenido = JsonNode.Parse(ajuste.PayloadJson)!;
        contenido["adjustmentClass"]!.GetValue<string>().Should().Be(clase);
        contenido["amount"]!.GetValue<decimal>().Should().Be(-monto);
        contenido["originalMessageId"]!.GetValue<Guid>().Should().Be(original.PublicId);
        contenido["accountsReceivableRecordedBy"]!.GetValue<string>().Should().Be("Contabilidad");
        c.Db.IntegrationMessageDependencies.AsNoTracking().Should().Contain(d => d.MessageId == ajuste.Id && d.DependsOnMessageId == original.Id);
        var entrega = c.Db.IntegrationMessageDeliveries.AsNoTracking().Single(e => e.MessageId == ajuste.Id);
        (entrega.Destination, entrega.Mode, entrega.Status).Should().Be((IntegrationDestinations.Lending, DeliveryMode.Always, DeliveryStatus.Pending));
    }

    [Fact]
    public async Task Anular_el_comprobante_no_electronico_ajusta_todo_el_credito()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaAprobadaAsync(c);
        var original = Mensajes(c, venta.PublicId, VentaACreditoRegistradaV1.Type).Single();

        var anulada = await new VoidInventoryDocumentCommandHandler(c.Db, c.V.K.Actor, c.V.Compras.C.Reloj, c.V.K.Vista(), c.Confirmacion())
            .Handle(new VoidInventoryDocumentCommand(venta.PublicId, DocumentClassGroup.Sales, "Venta duplicada"), default);

        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? anulada.Error.Message : string.Empty);
        var ajuste = c.Db.IntegrationMessages.AsNoTracking().Single(m => m.Type == AjusteDeVentaACreditoV1.Type);
        var contenido = JsonNode.Parse(ajuste.PayloadJson)!;
        contenido["adjustmentClass"]!.GetValue<string>().Should().Be("Voiding");
        contenido["amount"]!.GetValue<decimal>().Should().Be(-120_000m);
        contenido["originalMessageId"]!.GetValue<Guid>().Should().Be(original.PublicId);
        c.Db.IntegrationMessageDependencies.AsNoTracking().Should().Contain(d => d.MessageId == ajuste.Id && d.DependsOnMessageId == original.Id);
    }

    [Fact]
    public async Task Una_nota_que_reintegra_solo_por_medios_de_contado_no_emite_ajuste()
    {
        var c = await CreditoDePrueba.CrearAsync();
        var venta = await VentaAprobadaAsync(c, efectivo: 60_000m);

        var nota = await NotaAsync(c, venta, false, [c.V.Pago(c.V.Efectivo, 20_000m)]);

        Mensajes(c, nota.PublicId, AjusteDeVentaACreditoV1.Type).Should().BeEmpty();
        c.V.MensajesDe(nota.PublicId).Should().Contain(NotaCreditoEmitidaV1.Type);
    }
}
