using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Purchasing;

/// <summary>
/// Feature 012, T328 (contracts/api.md §14.5; E9): la nota del proveedor contra su factura confirmada del mismo proveedor; la
/// crédito no pasa de lo que queda; impuestos y retenciones con la foto de la factura en proporción y sin volver a probar la base
/// mínima; «FacturaProveedorRegistrada» con su signo y, con <c>affectsCost</c>, el ajuste de costo; no mueve existencia; misma
/// unicidad que la factura; rige el límite de monto de <c>Purchases.Confirm</c>.
/// </summary>
public class NotaDeProveedorTests
{
    /// <summary>Una nota contra la primera línea de la factura, por valor (o por la cantidad de la factura con su precio).</summary>
    public static async Task<SaveInventoryDraftRequest> NotaAsync(ComprasDePrueba c, InventoryDocumentDto factura, string numero, string clase,
        decimal? valor = null, bool afectaCosto = false, Person? proveedor = null)
    {
        var linea = (await c.LineasAsync(factura.PublicId)).First();
        return new SaveInventoryDraftRequest(c.Tipo("NCP"), null, null, null, null, null, null, "Descuento comercial", null, null, null, null, null,
            [new SaveInventoryDraftLine(null, Guid.Empty, Guid.Empty, 0m, InvoiceLinePublicId: linea.PublicId, Amount: valor, AffectsCost: afectaCosto ? true : null)],
            SupplierPersonPublicId: (proveedor ?? c.ProveedorA).PublicId,
            Supplier: ComprasDePrueba.Documento(numero, prefijo: "NC"),
            SupplierInvoicePublicId: factura.PublicId,
            NoteKind: clase);
    }

    private static async Task<(ComprasDePrueba C, InventoryDocumentDto Recepcion, InventoryDocumentDto Factura)> FacturaDeAceiteAsync()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P3, 10m, 1_000m)]));
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento()));
        return (c, recepcion, factura);
    }

    [Fact]
    public async Task Va_contra_una_factura_confirmada_del_mismo_proveedor()
    {
        var (c, _, factura) = await FacturaDeAceiteAsync();
        (await c.GuardarAsync(await NotaAsync(c, factura, "1", "Credit", 1_000m, proveedor: c.ProveedorB)))
            .Error.Code.Should().Be("Inventory.SupplierNote.InvoiceFromOtherSupplier");

        var flete = await c.ServicioAsync();
        var borrador = (await c.GuardarAsync(c.Factura(ComprasDePrueba.Documento("B1"), lineas: [c.Linea(flete, 1m, 1_000m)]))).Value;
        (await c.GuardarAsync(await NotaAsync(c, borrador, "2", "Credit", 100m)))
            .Error.Code.Should().Be("Inventory.SupplierNote.InvoiceNotConfirmed");

        (await c.GuardarAsync(await NotaAsync(c, factura, "3", "Credit", 1_000m))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_nota_credito_no_pasa_de_lo_que_queda_de_la_factura()
    {
        var (c, _, factura) = await FacturaDeAceiteAsync();
        await c.ConfirmadoAsync(await NotaAsync(c, factura, "1", "Credit", 8_000m));

        var guardada = await c.GuardarAsync(await NotaAsync(c, factura, "2", "Credit", 3_000m));
        guardada.Value.Warnings.Should().Contain(w => w.Code == "Inventory.SupplierNote.ExceedsInvoice");
        var r = await c.ConfirmarAsync(guardada.Value.PublicId);
        r.Error.Code.Should().Be("Inventory.SupplierNote.ExceedsInvoice");
        // La factura es 11.900 y la primera nota 9.520 (8.000 + su IVA): quedan 2.380.
        JsonSerializer.Serialize(CatalogoDePrueba.Datos(r.Error)).Should().Contain("\"remaining\":2380");
    }

    [Fact]
    public async Task Impuestos_con_la_foto_de_la_factura_en_proporcion()
    {
        var (c, _, factura) = await FacturaDeAceiteAsync();
        var nota = await c.ConfirmadoAsync(await NotaAsync(c, factura, "1", "Credit", 5_000m));

        var doc = c.Documento(nota.PublicId);
        doc.Subtotal.Should().Be(5_000m);
        doc.TaxTotal.Should().Be(950m);
        var foto = await c.C.Db.DocumentTaxLines.Where(t => t.DocumentId == doc.Id).ToListAsync();
        foto.Should().ContainSingle(t => t.TaxRateCode == "IVA19" && t.Base == 5_000m && t.Amount == 950m && t.Treatment == TaxTreatment.Deductible);
    }

    [Fact]
    public async Task La_retencion_de_la_factura_se_aplica_sin_volver_a_probar_la_base_minima()
    {
        var c = await ComprasDePrueba.CrearAsync();
        var recepcion = await c.ConfirmadoAsync(c.Recepcion(lineas: [c.Linea(c.P1, 1m, 2_000_000m)]));
        var factura = await c.ConfirmadoAsync(await c.FacturaContraAsync(recepcion, ComprasDePrueba.Documento()));
        c.Documento(factura.PublicId).WithholdingTotal.Should().Be(50_000m);

        var nota = await c.ConfirmadoAsync(await NotaAsync(c, factura, "1", "Credit", 100_000m));
        c.Documento(nota.PublicId).WithholdingTotal.Should().Be(2_500m, "100.000 está bajo el mínimo, pero la nota usa la foto de la factura (E9)");
    }

    [Fact]
    public async Task La_credito_viaja_con_importes_negativos_y_no_mueve_existencia()
    {
        var (c, _, factura) = await FacturaDeAceiteAsync();
        var existenciaAntes = (await c.C.Db.StockBalances.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P3))).Physical;
        var nota = await c.ConfirmadoAsync(await NotaAsync(c, factura, "1", "Credit", 5_000m));

        c.MensajesDe(nota.PublicId).Should().Equal("FacturaProveedorRegistrada");
        var m = JsonNode.Parse(c.ContenidoDe(nota.PublicId, "FacturaProveedorRegistrada"))!;
        m["supplierDocument"]!["kind"]!.GetValue<string>().Should().Be("CreditNote");
        m["totals"]!["subtotal"]!.GetValue<decimal>().Should().Be(-5_000m);
        m["totals"]!["total"]!.GetValue<decimal>().Should().Be(-5_950m);
        m["lines"]![0]!["netAmount"]!.GetValue<decimal>().Should().Be(-5_000m);
        (await c.C.Db.StockBalances.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P3))).Physical.Should().Be(existenciaAntes);
        c.Documento(nota.PublicId).PostingMode.Should().Be(c.Documento(factura.PublicId).PostingMode, "es un relacionado: copia el modo de la factura");
    }

    [Fact]
    public async Task La_debito_suma_y_con_affectsCost_ajusta_el_costo_de_la_recepcion()
    {
        var (c, recepcion, factura) = await FacturaDeAceiteAsync();
        var nota = await c.ConfirmadoAsync(await NotaAsync(c, factura, "1", "Debit", 1_000m, afectaCosto: true));

        c.MensajesDe(nota.PublicId).Should().Equal("FacturaProveedorRegistrada", "AjusteDeCostoReconocido");
        var m = JsonNode.Parse(c.ContenidoDe(nota.PublicId, "FacturaProveedorRegistrada"))!;
        m["supplierDocument"]!["kind"]!.GetValue<string>().Should().Be("DebitNote");
        m["totals"]!["subtotal"]!.GetValue<decimal>().Should().Be(1_000m);
        var ajuste = JsonNode.Parse(c.ContenidoDe(nota.PublicId, "AjusteDeCostoReconocido"))!;
        ajuste["affectedDocument"]!["publicId"]!.GetValue<Guid>().Should().Be(recepcion.PublicId);
        ajuste["lines"]![0]!["inventoryAmount"]!.GetValue<decimal>().Should().Be(1_000m);
        (await c.C.Db.CostStates.SingleAsync(s => s.ProductId == c.K.ProductoId(c.P3))).Value.Should().Be(11_000m);
    }

    [Fact]
    public async Task Misma_unicidad_que_la_factura()
    {
        var (c, _, factura) = await FacturaDeAceiteAsync();
        await c.ConfirmadoAsync(await NotaAsync(c, factura, "1", "Credit", 100m));
        (await c.GuardarAsync(await NotaAsync(c, factura, "1", "Credit", 100m))).Error.Code.Should().Be("Inventory.SupplierInvoice.Duplicate");
    }

    [Fact]
    public async Task Rige_el_limite_de_monto_de_Purchases_Confirm_con_el_total_de_la_nota()
    {
        var (c, _, factura) = await FacturaDeAceiteAsync();
        c.K.Motor.ClearReceivedCalls();
        await c.ConfirmadoAsync(await NotaAsync(c, factura, "1", "Credit", 1_000m));
        await c.K.Motor.Received().EvaluarAsync(ApprovalSubjects.DocumentConfirmation, Arg.Any<Guid>(), Arg.Any<DateOnly>(), 1_190m,
            "Inventory.Purchases.Confirm", Arg.Any<CancellationToken>());
    }
}
