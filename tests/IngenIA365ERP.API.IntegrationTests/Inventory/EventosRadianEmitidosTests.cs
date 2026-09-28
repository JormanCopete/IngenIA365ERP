using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.ElectronicInvoicing;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// T775 (feature 012, I5; US13-4, SC-015, T42; api.md §14.8, §24.7; dian.md §4.3), en el motor de <c>DB_PROVIDER</c>, sobre la cooperativa
/// de facturación electrónica de I4 configurada con el <c>CanalSimulado</c> («radiani5»): una factura del proveedor a crédito con su
/// recepción confirmada levanta la alerta <c>Compras.EventosRadianFaltantes</c>; <c>POST …/radian-events/emit</c> con el 030 y el 032
/// responde 202 con los dos en <c>Pending</c>; tras el procesador los dos quedan <c>Emitted</c> con su documento electrónico, validados
/// por el canal, y la revisión siguiente deja la alerta atendida. (nuevo)
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class EventosRadianEmitidosTests(CentralIdentityApiFixture fx)
{
    private const string Compras = "/api/inventory/purchases";

    [Fact]
    public async Task El_acuse_y_el_recibo_del_bien_se_emiten_por_el_canal_y_atienden_la_alerta()
    {
        var fe = await EscenarioDeFacturacionElectronica.PrepararAsync(fx, "radiani5");
        var esc = fe.Inv;
        using var http = fx.CreateClient();
        var t = fe.Admin;
        // El canal simulado valida a una contraparte cuyo documento termina en 9.
        var proveedor = await EscenarioDeFacturacionElectronica.PersonaAsync(http, t, "ProveedorRadian", 9);
        var emision = esc.Corte.AddDays(2);

        var compra = await InventarioE2E.ExitoAsync(http, t, HttpMethod.Post, $"{Compras}/direct", new
        {
            receipt = new
            {
                documentTypePublicId = esc.Tipos["REC"], warehousePublicId = esc.Bodega("PRIN"), supplierPersonPublicId = proveedor,
                lines = new[] { new { productPublicId = esc.P("P4").Id, unitPublicId = esc.P("P4").Unidad, quantity = 5m, unitPrice = 3_000m } },
            },
            invoice = new
            {
                documentTypePublicId = esc.Tipos["FCP"],
                supplier = new
                {
                    prefix = "FEP", number = "88001", cufe = new string('a', 96), issueDate = emision.ToString("yyyy-MM-dd"),
                    dueDate = emision.AddDays(30).ToString("yyyy-MM-dd"), paymentForm = "Credit", isElectronic = true,
                },
            },
        });
        var factura = compra.GetProperty("supplierInvoice").GetProperty("publicId").GetGuid();

        await fx.CorrerTareaAsync(fe.Coop.TenantPublicId, Application.Inventory.Purchasing.TareaDeEventosRadian.NombreDeLaTarea);
        (await AlertasAsync(http, t, "Pending")).Should().BeGreaterThan(0, "más de Compras.DiasAlertaEventosRadian días sin 030 ni 032");

        var emitir = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Compras}/supplier-invoices/{factura}/radian-events/emit",
            new { eventCodes = new[] { "Receipt030", "GoodsReceived032" } });
        emitir.StatusCode.Should().Be(HttpStatusCode.Accepted, await emitir.Content.ReadAsStringAsync());
        var pedidos = (await InventarioE2E.LeerAsync(emitir)).GetProperty("events").EnumerateArray().ToList();
        pedidos.Should().HaveCount(2).And.OnlyContain(e => e.GetProperty("electronicDocumentPublicId").GetGuid() != Guid.Empty);

        // El intento de después del commit ya pudo emitirlos; el procesador retoma lo que falte (el 032 espera al 030).
        for (var i = 0; i < 3; i++) await EscenarioDeFacturacionElectronica.ProcesarAsync(fx, fe.Coop.TenantPublicId);

        var eventos = (await InventarioE2E.GetAsync(http, t, $"{Compras}/supplier-invoices/{factura}/radian-events")).EnumerateArray().ToList();
        eventos.Should().HaveCount(2);
        eventos.Should().OnlyContain(e => e.GetProperty("status").ToString() == "2", $"los dos Emitted: {string.Join(" | ", eventos)}");
        foreach (var e in eventos)
        {
            var electronico = e.GetProperty("electronicDocumentPublicId").GetGuid();
            pedidos.Select(p => p.GetProperty("electronicDocumentPublicId").GetGuid()).Should().Contain(electronico);
            var detalle = await EscenarioDeFacturacionElectronica.DetalleAsync(http, t, electronico);
            EscenarioDeFacturacionElectronica.Estado(detalle).Should().BeOneOf(2, 3); // Validated o ValidatedWithNotices
        }

        await fx.CorrerTareaAsync(fe.Coop.TenantPublicId, Application.Inventory.Purchasing.TareaDeEventosRadian.NombreDeLaTarea);
        (await AlertasAsync(http, t, "Pending")).Should().Be(0, "con los dos eventos emitidos la alerta se atiende sola");
        (await AlertasAsync(http, t, "Attended")).Should().BeGreaterThan(0);

        // Emitir otra vez lo ya emitido no es posible.
        var repetido = await InventarioE2E.MandarAsync(http, t, HttpMethod.Post, $"{Compras}/supplier-invoices/{factura}/radian-events/emit",
            new { eventCodes = new[] { "Receipt030" } });
        await InventarioE2E.FallaAsync(repetido, "Inventory.RadianEvent.AlreadyRegistered");
    }

    private static async Task<int> AlertasAsync(HttpClient http, string t, string estado) =>
        (await InventarioE2E.GetAsync(http, t, $"/api/inventory/alerts?typeCode=Compras.EventosRadianFaltantes&status={estado}&pageSize=50"))
            .GetProperty("items").GetArrayLength();
}
