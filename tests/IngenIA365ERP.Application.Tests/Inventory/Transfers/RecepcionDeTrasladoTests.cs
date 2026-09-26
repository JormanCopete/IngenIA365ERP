using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Transfers;

/// <summary>
/// Feature 012, T360 (US10-2; contracts/api.md §11; mensajes.md §6.7): recibir crea y confirma la recepción en una operación; lo
/// recibido sale del tránsito al costo de la línea de despacho y entra al destino; lo que no llegó queda como faltante pendiente en
/// tránsito y lo que llegó de más como sobrante pendiente, fuera de la existencia. Una recepción por despacho, nunca más de lo
/// despachado, nunca antes del despacho, sólo de un despacho en tránsito; un despacho de un mes cerrado se recibe en el período
/// abierto; un producto bloqueado se recibe; la recepción copia el modo de su despacho y exige el destino en el alcance.
/// </summary>
public class RecepcionDeTrasladoTests
{
    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task Recibir_9_de_10_entra_9_al_costo_del_despacho_y_deja_1_faltante_en_transito()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        await t.K.EntradaAsync(t.K.P1, 10m, 2_000m, t.PV2); // el promedio del ámbito cambia: la recepción no lo mira
        var valorAntes = t.ValorTotal(t.K.P1);

        var r = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 9m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.DisplayNumber.Should().Be("TRR1");
        r.Value.OperationDate.Should().Be(t.K.C.Reloj.HoyLocal);
        r.Value.Surpluses.Should().BeEmpty();
        r.Value.Shortages.Should().ContainSingle().Which.QuantityBase.Should().Be(1m);

        t.Fisico(t.K.P1, t.PV2).Should().Be(19m);
        t.Fisico(t.K.P1, t.TR01).Should().Be(1m, "el faltante sigue en tránsito");
        t.ValorTotal(t.K.P1).Should().Be(valorAntes);

        var recepcion = t.Documento(r.Value.ReceiptPublicId);
        recepcion.Class.Should().Be(DocumentClass.TransferReceipt);
        (recepcion.WarehouseId, recepcion.DestinationWarehouseId, recepcion.TransitWarehouseId).Should().Be((t.PRIN.Id, t.PV2.Id, t.TR01.Id));
        var kardex = await t.Db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == recepcion.Id && k.Kind != KardexEntryKind.CostAdjustment)
            .OrderBy(k => k.Id).ToListAsync();
        kardex.Select(k => (k.WarehouseId, k.QuantityBase, k.UnitCost)).Should().Equal((t.TR01.Id, -9m, 1_000m), (t.PV2.Id, 9m, 1_000m));

        var faltante = t.Diferencia(r.Value.Shortages[0].DiscrepancyPublicId);
        faltante.Kind.Should().Be(TransferDiscrepancyKind.Shortage);
        faltante.UnitCost.Should().Be(1_000m);
        faltante.ResolvedAt.Should().BeNull();
        faltante.ReceiptDocumentId.Should().Be(recepcion.Id);

        var vinculo = await t.Db.DocumentLinks.AsNoTracking().SingleAsync(l => l.TargetDocumentId == recepcion.Id);
        vinculo.Kind.Should().Be(DocumentLinkKind.ReceiptOf);
        vinculo.SourceDocumentId.Should().Be(t.Documento(despacho).Id);
    }

    [Fact]
    public async Task Es_derivada_copia_el_modo_del_despacho_y_emite_TrasladoRecibido()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);

        var r = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 10m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        t.Documento(r.Value.ReceiptPublicId).PostingMode.Should().Be(t.Documento(despacho).PostingMode);
        var mensaje = await t.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == r.Value.ReceiptPublicId);
        mensaje.Type.Should().Be(TrasladoRecibidoV1.Type);
        using var json = JsonDocument.Parse(mensaje.PayloadJson);
        json.RootElement.GetProperty("operation").GetString().Should().Be("RecepcionTraslado");
        json.RootElement.GetProperty("derivedFrom").EnumerateArray().Single().GetProperty("publicId").GetGuid().Should().Be(despacho);
        var linea = json.RootElement.GetProperty("lines").EnumerateArray().Single();
        linea.GetProperty("fromWarehouseCode").GetString().Should().Be("TR01");
        linea.GetProperty("toWarehouseCode").GetString().Should().Be("PV2");
        linea.GetProperty("cost").GetDecimal().Should().Be(10_000m);
    }

    [Fact]
    public async Task El_sobrante_queda_pendiente_fuera_de_la_existencia()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);

        var r = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 10m, sobrante: 1m));

        r.Value.Shortages.Should().BeEmpty();
        r.Value.Surpluses.Should().ContainSingle().Which.QuantityBase.Should().Be(1m);
        t.Fisico(t.K.P1, t.PV2).Should().Be(10m, "el sobrante no entra hasta aprobar su ajuste");
        t.Diferencia(r.Value.Surpluses[0].DiscrepancyPublicId).UnitCost.Should().BeNull("su costo es el vigente al resolverse");
    }

    [Fact]
    public async Task No_se_recibe_mas_de_lo_despachado()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);

        var r = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 11m));

        t.Codigo(r).Should().Be("Inventory.Transfer.ReceiveExceedsDispatched");
        Datos(r.Error).GetProperty("dispatchedBase").GetDecimal().Should().Be(10m);
    }

    [Fact]
    public async Task Una_sola_recepcion_por_despacho()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        var primera = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 9m));

        var segunda = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 1m));

        t.Codigo(segunda).Should().Be("Inventory.Transfer.AlreadyReceived");
        Datos(segunda.Error).GetProperty("receiptPublicId").GetGuid().Should().Be(primera.Value.ReceiptPublicId);
    }

    [Fact]
    public async Task Un_despacho_sin_confirmar_o_anulado_no_esta_en_transito()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 20m, 1_000m);
        var borrador = await t.GuardarDespachoAsync(t.Despacho(t.PRIN, t.PV2, null, t.K.Linea(t.K.P1, 5m)));

        var sinConfirmar = await t.RecibirAsync(borrador.Value.PublicId, null, t.Recibida(borrador.Value.PublicId, 5m));
        t.Codigo(sinConfirmar).Should().Be("Inventory.Transfer.NotInTransit");

        var (despacho, _) = await t.TrasladoAsync(t.PRIN, t.PV2, t.K.Linea(t.K.P1, 5m));
        var anulado = await t.Anular().Handle(new Application.Inventory.Documents.VoidInventoryDocumentCommand(despacho, DocumentClassGroup.Transfers, "se canceló"), default);
        anulado.IsSuccess.Should().BeTrue(anulado.IsFailure ? $"{anulado.Error.Code}: {anulado.Error.Message}" : string.Empty);
        var r = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 5m));
        t.Codigo(r).Should().Be("Inventory.Transfer.NotInTransit");
    }

    [Fact]
    public async Task No_se_recibe_antes_del_despacho()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);

        var r = await t.RecibirAsync(despacho, t.K.C.Reloj.HoyLocal.AddDays(-1), t.Recibida(despacho, 10m));

        t.Codigo(r).Should().Be("Inventory.Transfer.ReceiptBeforeDispatch");
    }

    [Fact]
    public async Task Un_despacho_de_un_mes_cerrado_se_recibe_en_el_periodo_abierto()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var agosto = new DateOnly(2026, 8, 20);
        var (entrada, e) = await t.K.AjusteAsync(t.K.Borrador("AJP", fecha: new DateOnly(2026, 8, 1), lineas: [t.K.Linea(t.K.P1, 20m, 1_000m)]));
        e.IsSuccess.Should().BeTrue();
        var guardado = await t.GuardarDespachoAsync(t.Despacho(t.PRIN, t.PV2, agosto, t.K.Linea(t.K.P1, 10m)));
        var d = await t.DespacharAsync(guardado.Value.PublicId);
        d.IsSuccess.Should().BeTrue(d.IsFailure ? $"{d.Error.Code}: {d.Error.Message}" : string.Empty);
        t.Db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 1, 1), LastClosedDate = new DateOnly(2026, 8, 31), StartedAt = DateTime.UtcNow });
        await t.Db.SaveChangesAsync();

        var r = await t.RecibirAsync(guardado.Value.PublicId, null, t.Recibida(guardado.Value.PublicId, 10m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.OperationDate.Should().Be(t.K.C.Reloj.HoyLocal);
        _ = entrada;
    }

    [Fact]
    public async Task Un_producto_bloqueado_con_existencia_en_transito_se_recibe()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        t.K.C.Producto(t.K.P1).Status = ProductStatus.Blocked;
        await t.Db.SaveChangesAsync();

        var r = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 10m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        t.Fisico(t.K.P1, t.PV2).Should().Be(10m);
    }

    [Fact]
    public async Task Exige_el_destino_en_el_alcance()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);

        t.SoloEn(t.PRIN);
        var fuera = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 10m));
        t.Codigo(fuera).Should().Be("Inventory.Document.NotFound", "quien sólo ve el origen no recibe");

        t.SoloEn(t.PV2);
        var dentro = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 10m));
        dentro.IsSuccess.Should().BeTrue(dentro.IsFailure ? $"{dentro.Error.Code}: {dentro.Error.Message}" : string.Empty);
    }
}
