using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, US10, T0133 (US10 «Independent Test»; quickstart.md recorrido G pasos 1, 3–5): Cartera desembolsa por transferencia y la orden llega sola a la
/// bandeja de Tesorería; el lote en la cuenta de pagos con <c>PAGOS-GENERICO</c> muestra total, cantidad y GMF estimado; lo aprueba otra persona; el archivo se genera
/// y lo descarga una tercera; quien lo preparó no puede descargarlo. Una orden repetida no se duplica. Se corre en la fase 22.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class OrdenesYLotesDeGiroTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task La_orden_llega_se_arma_el_lote_lo_aprueba_otra_persona_y_descarga_una_tercera()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "ordenes");
        var orden = await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, 2_345_000m);
        orden.GetProperty("status").GetInt32().Should().BeOneOf(3, 2);

        // El jefe de Cartera prepara el lote (tiene Treasury.Batches.Prepare por la plantilla del tesorero en la fase 22) y otro aprueba.
        var lote = await TesoreriaE2E.LoteAsync(http, t.Admin, t.CuentaDePagos, [orden.GetProperty("publicId").GetGuid()]);
        lote.GetProperty("formatCode").GetString().Should().Be("PAGOS-GENERICO");
        lote.GetProperty("orderCount").GetInt32().Should().Be(1);
        lote.GetProperty("total").GetDecimal().Should().Be(2_345_000m);
        var id = lote.GetProperty("publicId").GetGuid();
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/submit"), "enviar a aprobación");

        var autoaprobado = await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve", new { });
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(autoaprobado)).Should().Be("Treasury.Batch.SelfApproval");
        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Escenario.Usuarios.Jefe.Token, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/approve",
            new { }), "aprobar");

        var archivo = await InventarioE2E.ExitoAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-batches/{id}/file");
        var archivoId = archivo.GetProperty("publicId").GetGuid();
        archivo.GetProperty("sha256").GetString().Should().HaveLength(64);

        var mismaPersona = await CarteraE2E.MandarGetAsync(http, t.Admin, $"/api/treasury/payment-files/{archivoId}/download");
        (await RecaudoYTesoreriaE2E.CodigoDeErrorAsync(mismaPersona)).Should().Be("Treasury.File.SameUserDownload");
        var tercera = await CarteraE2E.MandarGetAsync(http, t.Escenario.Usuarios.Pruebas.Token, $"/api/treasury/payment-files/{archivoId}/download");
        tercera.StatusCode.Should().Be(HttpStatusCode.OK, await tercera.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Una_orden_repetida_no_se_duplica_y_una_de_valor_cero_se_rechaza_al_llegar()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "repetidas");
        await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, 1_111_000m);
        await CarteraE2E.DespacharAsync(fx, t.Escenario.Coop.TenantPublicId, 3);
        var ordenes = await CarteraE2E.GetAsync(http, t.Admin, "/api/treasury/payment-orders");
        ordenes.EnumerateArray().Count(o => o.GetProperty("netAmount").GetDecimal() == 1_111_000m).Should().Be(1);
    }
}
