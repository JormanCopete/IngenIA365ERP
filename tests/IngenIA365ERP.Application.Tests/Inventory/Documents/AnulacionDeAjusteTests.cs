using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T242 (FR-006; contracts/api.md §9.5; data-model §3.1 «anulación»): anular un ajuste crea el <c>Voiding</c> con su
/// propia fecha y las referencias en los dos sentidos, revierte cada hecho con <c>ReversesEntryId</c> al costo del original y
/// deja la existencia de antes; si la entrada anulada ya entró al promedio y éste cambió, una línea <c>VoidDifference</c> y un
/// <c>AjusteDeCostoReconocido</c> sobre el documento afectado; <c>DocumentoAnulado</c> lleva el contenido del original con los
/// signos contrarios; una segunda anulación responde <c>AlreadyVoided</c>; anular una entrada ya consumida con el negativo
/// prohibido responde <c>Inventory.Stock.Insufficient</c> con <c>data.suggestion</c>.
/// </summary>
public class AnulacionDeAjusteTests
{
    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "(éxito)";

    private static Task<Result<VoidResultDto>> AnularAsync(KardexDePrueba k, Guid documento, string motivo = "Error de digitación") =>
        k.Anular().Handle(new VoidInventoryDocumentCommand(documento, DocumentClassGroup.Adjustments, motivo), default);

    [Fact]
    public async Task Anular_revierte_cada_hecho_al_costo_del_original_con_su_propia_fecha_y_deja_la_existencia_de_antes()
    {
        var k = await KardexDePrueba.CrearAsync();
        // La entrada va antes de la salida: una salida fechada antes de un movimiento ya registrado es retroactiva (US3, T285).
        var (_, entrada) = await k.AjusteAsync(k.Borrador("AJP", fecha: new DateOnly(2026, 9, 10), lineas: [k.Linea(k.P1, 10m, 1000m)]));
        entrada.IsSuccess.Should().BeTrue();
        var (salida, _) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), fecha: new DateOnly(2026, 9, 20), lineas: [k.Linea(k.P1, 4m)]));

        var r = await AnularAsync(k, salida);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.OperationDate.Should().Be(Catalog.CatalogoDePrueba.Hoy, "la anulación lleva su propia fecha, nunca la del original");
        r.Value.CostAdjustments.Should().BeEmpty("revertir una salida entra al costo con que salió, sin diferencia");

        var original = await k.C.Db.InventoryDocuments.SingleAsync(d => d.PublicId == salida);
        var anulacion = await k.C.Db.InventoryDocuments.SingleAsync(d => d.PublicId == r.Value.VoidingDocumentPublicId);
        original.Status.Should().Be(DocumentStatus.Voided);
        original.VoidedByDocumentId.Should().Be(anulacion.Id);
        anulacion.VoidsDocumentId.Should().Be(original.Id);

        var delOriginal = await k.C.Db.KardexEntries.SingleAsync(e => e.DocumentId == original.Id);
        var reversion = await k.C.Db.KardexEntries.SingleAsync(e => e.DocumentId == anulacion.Id);
        reversion.ReversesEntryId.Should().Be(delOriginal.Id);
        reversion.Kind.Should().Be(KardexEntryKind.Entry);
        reversion.QuantityBase.Should().Be(4m);
        reversion.UnitCost.Should().Be(delOriginal.UnitCost, "al costo del original");
        reversion.OperationDate.Should().Be(Catalog.CatalogoDePrueba.Hoy);

        (await k.C.Db.StockBalances.SingleAsync()).Physical.Should().Be(10m, "la existencia de antes");
        var costo = await k.C.Db.CostStates.SingleAsync();
        costo.Quantity.Should().Be(10m);
        costo.Value.Should().Be(10000m);
    }

    [Fact]
    public async Task Anular_una_entrada_ya_promediada_deja_la_diferencia_como_VoidDifference_y_su_ajuste_de_costo()
    {
        var k = await KardexDePrueba.CrearAsync();
        var primera = await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.EntradaAsync(k.P1, 10m, 1300m);

        var r = await AnularAsync(k, primera);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var anulacion = await k.C.Db.InventoryDocuments.SingleAsync(d => d.PublicId == r.Value.VoidingDocumentPublicId);
        var lineas = await k.C.Db.KardexEntries.Where(e => e.DocumentId == anulacion.Id).OrderBy(e => e.Id).ToListAsync();
        lineas.Should().HaveCount(2);
        lineas[0].Kind.Should().Be(KardexEntryKind.Exit);
        lineas[0].UnitCost.Should().Be(1000m, "sale al costo con que entró");
        lineas[0].TotalCost.Should().Be(-10000m);
        lineas[1].Kind.Should().Be(KardexEntryKind.CostAdjustment);
        lineas[1].Reason.Should().Be(KardexReason.VoidDifference);
        lineas[1].TotalCost.Should().Be(-1500m, "10 × 1.000 − 10 × 1.150");
        var entradaOriginal = await k.C.Db.KardexEntries.SingleAsync(e => e.DocumentId == k.C.Db.InventoryDocuments.Single(d => d.PublicId == primera).Id);
        lineas[1].AffectsEntryId.Should().Be(entradaOriginal.Id);

        var costo = await k.C.Db.CostStates.SingleAsync();
        costo.Quantity.Should().Be(10m);
        costo.Value.Should().Be(11500m);
        costo.AverageCost.Should().Be(1150m, "anular al costo de entrada no mueve el promedio");

        var ajuste = r.Value.CostAdjustments.Should().ContainSingle().Subject;
        ajuste.Product.Code.Should().Be("P1");
        ajuste.Difference.Should().Be(-1500m);

        var mensaje = await k.C.Db.IntegrationMessages.SingleAsync(m => m.Type == AjusteDeCostoReconocidoV1.Type);
        mensaje.OriginPublicId.Should().Be(anulacion.PublicId);
        mensaje.OriginEventKey.Should().Be($"Confirmation:{primera:N}", "una parte por documento afectado");
        mensaje.RelatedPublicId.Should().Be(primera);
        var contenido = JsonDocument.Parse(mensaje.PayloadJson).RootElement;
        contenido.GetProperty("reason").GetString().Should().Be("VoidDifference");
        contenido.GetProperty("lines")[0].GetProperty("inventoryAmount").GetDecimal().Should().Be(-1500m);
    }

    [Fact]
    public async Task DocumentoAnulado_lleva_el_contenido_del_original_con_los_signos_contrarios()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);
        var (salida, _) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 4m)]));

        var r = await AnularAsync(k, salida, "Se contó mal");

        var anulado = await k.C.Db.IntegrationMessages.SingleAsync(m => m.Type == DocumentoAnuladoV1.Type);
        var original = await k.C.Db.IntegrationMessages.SingleAsync(m => m.OriginPublicId == salida);
        anulado.OriginPublicId.Should().Be(r.Value.VoidingDocumentPublicId);
        anulado.RelatedPublicId.Should().Be(salida);
        anulado.Kind.Should().Be(original.Kind, "hereda el Kind del original");
        var raiz = JsonDocument.Parse(anulado.PayloadJson).RootElement;
        raiz.GetProperty("reason").GetString().Should().Be("Se contó mal");
        raiz.GetProperty("voidedDocumentTypeCode").GetString().Should().Be("AJN");
        var anuladoContenido = raiz.GetProperty("voidedContents").EnumerateArray().Single();
        anuladoContenido.GetProperty("messageId").GetGuid().Should().Be(original.PublicId);
        anuladoContenido.GetProperty("type").GetString().Should().Be(AjusteInventarioAprobadoV1.Type);
        var linea = anuladoContenido.GetProperty("content").GetProperty("lines")[0];
        linea.GetProperty("quantityBase").GetDecimal().Should().Be(-4m);
        linea.GetProperty("cost").GetDecimal().Should().Be(-4000m);
        linea.GetProperty("documentLines")[0].GetInt32().Should().Be(1, "los números de línea no se invierten");
    }

    [Fact]
    public async Task Anular_dos_veces_responde_ya_anulado()
    {
        var k = await KardexDePrueba.CrearAsync();
        var entrada = await k.EntradaAsync(k.P1, 10m, 1000m);
        (await AnularAsync(k, entrada)).IsSuccess.Should().BeTrue();

        var r = await AnularAsync(k, entrada, "otra vez");

        Codigo(r).Should().Be("Inventory.Document.AlreadyVoided");
    }

    [Fact]
    public async Task Anular_una_entrada_ya_consumida_con_el_negativo_prohibido_propone_un_ajuste_negativo()
    {
        var k = await KardexDePrueba.CrearAsync();
        var entrada = await k.EntradaAsync(k.P1, 10m, 1000m);
        (await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 8m)]))).Confirmacion.IsSuccess.Should().BeTrue();

        var r = await AnularAsync(k, entrada);

        Codigo(r).Should().Be("Inventory.Stock.Insufficient");
        var datos = r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data;
        datos.GetType().GetProperty("suggestion")!.GetValue(datos).Should().Be("NegativeAdjustment");
        datos.GetType().GetProperty("available")!.GetValue(datos).Should().Be(2m);
        (await k.C.Db.InventoryDocuments.SingleAsync(d => d.PublicId == entrada)).Status.Should().Be(DocumentStatus.Confirmed);
    }

    [Fact]
    public async Task Sin_permiso_de_costos_la_anulacion_no_informa_las_diferencias()
    {
        var k = await KardexDePrueba.CrearAsync();
        var primera = await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.EntradaAsync(k.P1, 10m, 1300m);
        k.Permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, Arg.Any<CancellationToken>()).Returns(false);

        var r = await AnularAsync(k, primera);

        r.IsSuccess.Should().BeTrue();
        r.Value.CostAdjustments.Should().BeNull();
    }
}
