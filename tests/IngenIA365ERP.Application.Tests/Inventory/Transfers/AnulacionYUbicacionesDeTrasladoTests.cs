using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Inventory.Transfers;

/// <summary>
/// Feature 012, T362 (US10-3, US10-4; contracts/api.md §10, §11): un despacho no recibido se anula y la mercancía vuelve del tránsito al
/// origen al costo del despacho, con <c>DocumentoAnulado</c>; con recepción, <c>Inventory.Document.HasDependents</c>; una recepción
/// no se anula (<c>Inventory.Transfer.UseReverseTransfer</c>). El movimiento entre ubicaciones es de un paso, no cambia el estado de
/// costo ni emite mensajes, deja dos filas de kardex de igual valor y signo contrario en la misma bodega y rechaza la misma
/// ubicación.
/// </summary>
public class AnulacionYUbicacionesDeTrasladoTests
{
    private static Task<Result<VoidResultDto>> AnularAsync(TrasladosDePrueba t, Guid documento, DocumentClassGroup grupo = DocumentClassGroup.Transfers) =>
        t.Anular().Handle(new VoidInventoryDocumentCommand(documento, grupo, "se canceló el viaje"), default);

    [Fact]
    public async Task Anular_un_despacho_no_recibido_devuelve_del_transito_al_origen()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);
        await t.K.EntradaAsync(t.K.P1, 10m, 1_300m);
        var (despacho, d) = await t.TrasladoAsync(t.PRIN, t.PV2, t.K.Linea(t.K.P1, 10m));
        d.IsSuccess.Should().BeTrue();
        var costoAntes = await t.Db.CostStates.AsNoTracking().SingleAsync(c => c.ProductId == t.K.ProductoId(t.K.P1));

        var r = await AnularAsync(t, despacho);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        t.Documento(despacho).Status.Should().Be(DocumentStatus.Voided);
        t.Fisico(t.K.P1, t.PRIN).Should().Be(20m);
        t.Fisico(t.K.P1, t.TR01).Should().Be(0m);
        var costo = await t.Db.CostStates.AsNoTracking().SingleAsync(c => c.ProductId == t.K.ProductoId(t.K.P1));
        (costo.Quantity, costo.Value, costo.AverageCost).Should().Be((costoAntes.Quantity, costoAntes.Value, costoAntes.AverageCost),
            "vuelve al costo del despacho, sin diferencia contra el promedio");
        var anulacion = t.Documento(r.Value.VoidingDocumentPublicId);
        (await t.Db.KardexEntries.AsNoTracking().AnyAsync(k => k.DocumentId == anulacion.Id && k.Reason == KardexReason.VoidDifference))
            .Should().BeFalse();
        var mensaje = await t.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == r.Value.VoidingDocumentPublicId);
        mensaje.Type.Should().Be(DocumentoAnuladoV1.Type);
    }

    [Fact]
    public async Task Con_recepcion_el_despacho_tiene_dependientes()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        (await t.RecibirAsync(despacho, null, t.Recibida(despacho, 10m))).IsSuccess.Should().BeTrue();

        var r = await AnularAsync(t, despacho);

        t.Codigo(r).Should().Be("Inventory.Document.HasDependents");
    }

    [Fact]
    public async Task Una_recepcion_no_se_anula_se_corrige_con_un_traslado_contrario()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        var despacho = await t.DespachoConfirmadoAsync(10m);
        var recepcion = await t.RecibirAsync(despacho, null, t.Recibida(despacho, 10m));

        var r = await AnularAsync(t, recepcion.Value.ReceiptPublicId);

        t.Codigo(r).Should().Be("Inventory.Transfer.UseReverseTransfer");

        // El traslado contrario: de PV2 a PRIN, con su propio tránsito (el de Florida).
        var (contrario, c) = await t.TrasladoAsync(t.PV2, t.PRIN, t.K.Linea(t.K.P1, 10m));
        c.IsSuccess.Should().BeTrue(c.IsFailure ? $"{c.Error.Code}: {c.Error.Message}" : string.Empty);
        t.Documento(contrario).TransitWarehouseId.Should().Be(t.TR02.Id);
    }

    [Fact]
    public async Task El_movimiento_entre_ubicaciones_es_de_un_paso_neutro_y_sin_mensajes()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);
        await t.K.EntradaAsync(t.K.P1, 10m, 1_300m);
        var antes = await t.Db.CostStates.AsNoTracking().SingleAsync(c => c.ProductId == t.K.ProductoId(t.K.P1));

        var (documento, r) = await MoverAsync(t, 5m, t.A01.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.PostingMode.Should().BeNull("no emite mensajes a Contabilidad");
        var despues = await t.Db.CostStates.AsNoTracking().SingleAsync(c => c.ProductId == t.K.ProductoId(t.K.P1));
        (despues.Quantity, despues.Value, despues.AverageCost, despues.LastUnitCost)
            .Should().Be((antes.Quantity, antes.Value, antes.AverageCost, antes.LastUnitCost));

        var id = t.Documento(documento).Id;
        var kardex = await t.Db.KardexEntries.AsNoTracking().Where(k => k.DocumentId == id).OrderBy(k => k.Id).ToListAsync();
        kardex.Should().HaveCount(2);
        kardex.Should().OnlyContain(k => k.WarehouseId == t.PRIN.Id);
        kardex[0].TotalCost.Should().Be(-kardex[1].TotalCost);
        kardex[1].LocationId.Should().Be(t.A01.Id);
        (await t.Db.IntegrationMessages.AsNoTracking().AnyAsync(m => m.OriginPublicId == documento)).Should().BeFalse();

        var detalles = await t.Db.StockDetails.AsNoTracking().Where(s => s.WarehouseId == t.PRIN.Id).ToListAsync();
        detalles.Single(s => s.LocationId == t.A01.Id).Quantity.Should().Be(5m);
        detalles.Where(s => s.LocationId != t.A01.Id).Sum(s => s.Quantity).Should().Be(15m);
        t.Fisico(t.K.P1, t.PRIN).Should().Be(20m);
    }

    [Fact]
    public async Task El_movimiento_a_la_misma_ubicacion_o_sin_destino_se_rechaza()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);
        var general = await t.Db.WarehouseLocations.AsNoTracking().SingleAsync(l => l.WarehouseId == t.PRIN.Id && l.IsDefault);

        var (_, misma) = await MoverAsync(t, 1m, general.PublicId);
        t.Codigo(misma).Should().Be("Inventory.Location.Same");

        var (_, sinDestino) = await MoverAsync(t, 1m, null);
        t.Codigo(sinDestino).Should().Be("Inventory.Document.FieldRequired");
    }

    [Fact]
    public async Task Anular_un_movimiento_entre_ubicaciones_lo_devuelve()
    {
        var t = await TrasladosDePrueba.CrearAsync();
        await t.K.EntradaAsync(t.K.P1, 10m, 1_000m);
        var (documento, r) = await MoverAsync(t, 4m, t.A01.PublicId);
        r.IsSuccess.Should().BeTrue();

        var anulada = await AnularAsync(t, documento, DocumentClassGroup.Adjustments);

        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? $"{anulada.Error.Code}: {anulada.Error.Message}" : string.Empty);
        var detalles = await t.Db.StockDetails.AsNoTracking().Where(s => s.WarehouseId == t.PRIN.Id).ToListAsync();
        detalles.Single(s => s.LocationId == t.A01.Id).Quantity.Should().Be(0m);
        t.Fisico(t.K.P1, t.PRIN).Should().Be(10m);
    }

    private static async Task<(Guid Documento, Result<ConfirmationResultDto> Resultado)> MoverAsync(TrasladosDePrueba t, decimal cantidad, Guid? hacia)
    {
        var borrador = new SaveInventoryDraftRequest(t.Tipo("MUB"), null, t.PRIN.PublicId, null, null, null, null, null, null, null, null, null, null,
            [new SaveInventoryDraftLine(null, t.K.P1, t.K.Unidad(t.K.P1), cantidad, ToLocationPublicId: hacia)]);
        var guardado = await t.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, borrador), default);
        if (guardado.IsFailure) throw new InvalidOperationException($"{guardado.Error.Code}: {guardado.Error.Message}");
        return (guardado.Value.PublicId, await t.Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(guardado.Value.PublicId, DocumentClassGroup.Adjustments), default));
    }
}
