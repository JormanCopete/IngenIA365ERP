using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T329 (FR-044, FR-051, US9-3; contracts/api.md §14.6; caso dorado 03): la devolución a proveedor. Toda línea va
/// enlazada a una de recepción y no pasa de lo recibido menos lo ya devuelto; sale al costo de la recepción aunque el promedio
/// haya cambiado, con la diferencia como <c>VoidDifference</c> y su «AjusteDeCostoReconocido»; emite «DevolucionRegistrada» con la
/// recepción como relacionado y su modo; sin disponible, <c>Inventory.Stock.Insufficient</c>.
/// </summary>
public class DevolucionAProveedorTests
{
    private static async Task<SaveInventoryDraftRequest> DevolucionAsync(ComprasDePrueba c, InventoryDocumentDto recepcion, decimal cantidad)
    {
        var linea = (await c.LineasAsync(recepcion.PublicId)).Single();
        return new SaveInventoryDraftRequest(c.Tipo("DVP"), null, c.K.Principal.PublicId, null, null, null, null, "Producto averiado", null, null,
            null, null, null, [new SaveInventoryDraftLine(null, Guid.Empty, c.Base(c.P1), cantidad, ReceiptLinePublicId: linea.PublicId)],
            SupplierPersonPublicId: c.ProveedorA.PublicId);
    }

    [Fact]
    public async Task Toda_linea_va_enlazada_a_una_linea_de_recepcion()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var sinRecepcion = new SaveInventoryDraftRequest(c.Tipo("DVP"), null, c.K.Principal.PublicId, null, null, null, null, "x", null, null, null, null, null,
            [c.Linea(c.P1, 6m, 0m)], SupplierPersonPublicId: c.ProveedorA.PublicId);
        (await c.GuardarAsync(sinRecepcion)).Error.Code.Should().Be("Inventory.Return.ReceiptLineRequired");
    }

    [Fact]
    public async Task No_pasa_de_lo_recibido_menos_lo_ya_devuelto()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        await c.ConfirmadoAsync(await DevolucionAsync(c, recepcion, 30m));

        var segunda = await c.GuardarAsync(await DevolucionAsync(c, recepcion, 10m));
        segunda.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Return.ExceedsReceived");
        var r = await c.ConfirmarAsync(segunda.Value.PublicId);
        r.Error.Code.Should().Be("Inventory.Return.ExceedsReceived");
        JsonSerializer.Serialize(CatalogoDePrueba.Datos(r.Error)).Should().Contain("\"received\":36").And.Contain("\"alreadyReturned\":30");
    }

    [Fact]
    public async Task Sale_al_costo_con_que_entro_y_la_diferencia_con_el_promedio_es_ajuste_de_costo()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();           // 36 a 1.300
        await c.K.EntradaAsync(c.P1, 36m, 1_700m);                       // promedio 1.500
        var devolucion = await c.ConfirmadoAsync(await DevolucionAsync(c, recepcion, 6m));

        var doc = c.Documento(devolucion.PublicId);
        var kardex = await c.C.Db.KardexEntries.Where(k => k.DocumentId == doc.Id).ToListAsync();
        kardex.Should().Contain(k => k.Kind == KardexEntryKind.Exit && k.QuantityBase == -6m && k.UnitCost == 1_300m);
        kardex.Should().ContainSingle(k => k.Reason == KardexReason.VoidDifference).Which.TotalCost.Should().Be(-1_200m);
        var estado = await c.C.Db.CostStates.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P1));
        estado.Quantity.Should().Be(66m);
        estado.AverageCost.Should().Be(1_500m, "el promedio no cambia: la diferencia la absorbe el ajuste");

        c.MensajesDe(devolucion.PublicId).Should().Equal("DevolucionRegistrada", "AjusteDeCostoReconocido");
        var m = JsonNode.Parse(c.ContenidoDe(devolucion.PublicId, "DevolucionRegistrada"))!;
        m["operation"]!.GetValue<string>().Should().Be("DevolucionAProveedor");
        m["lines"]![0]!["quantityBase"]!.GetValue<decimal>().Should().Be(6m);
        m["lines"]![0]!["cost"]!.GetValue<decimal>().Should().Be(7_800m);
        var ajuste = JsonNode.Parse(c.ContenidoDe(devolucion.PublicId, "AjusteDeCostoReconocido"))!;
        ajuste["affectedDocument"]!["publicId"]!.GetValue<Guid>().Should().Be(recepcion.PublicId);
        ajuste["lines"]![0]!["inventoryAmount"]!.GetValue<decimal>().Should().Be(-1_200m);

        var mensaje = await c.C.Db.IntegrationMessages.SingleAsync(x => x.OriginPublicId == devolucion.PublicId && x.Type == "DevolucionRegistrada");
        mensaje.RelatedPublicId.Should().Be(recepcion.PublicId);
        doc.PostingMode.Should().Be(c.Documento(recepcion.PublicId).PostingMode);
    }

    [Fact]
    public async Task Sin_disponible_suficiente_no_sale()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.RecepcionDeTresDocenasAsync();
        var salida = await c.K.AjusteAsync(c.K.Borrador("AJN", causa: c.K.Causa(), lineas: [c.K.Linea(c.P1, 34m)]));
        salida.Confirmacion.IsSuccess.Should().BeTrue();

        var guardada = await c.GuardarAsync(await DevolucionAsync(c, recepcion, 6m));
        var r = await c.ConfirmarAsync(guardada.Value.PublicId);
        r.Error.Code.Should().Be("Inventory.Stock.Insufficient");
    }

    [Fact]
    public async Task No_pide_nota_de_ajuste_del_documento_soporte_hasta_I4()
    {
        Application.Inventory.Documents.Efectos.EfectoDevolucionAProveedor.SupportDocumentAdjustmentNoteRequired.Should().BeFalse();
        await Task.CompletedTask;
    }
}
