using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Lending;
using IngenIA365ERP.API.IntegrationTests.Treasury;

namespace IngenIA365ERP.API.IntegrationTests.Receipts;

/// <summary>
/// Feature 015, T0165 (parte de Tesorería; FR-203; SC-025): un beneficiario en lista vinculante no entra a un lote aprobado: su orden queda pendiente con
/// <c>BindingListMatch</c> y la alerta <c>Tesoreria.ListaVinculante</c> sale de inmediato. El canal de listas es el simulado (documento terminado en 9 = coincidencia
/// exacta). La parte de Recaudo (el recibo del pagador en lista) la suma la fase 15. Se corre en la fase 22.
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public sealed class ListaVinculanteBloqueaTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task El_beneficiario_en_lista_vinculante_no_entra_a_un_lote_aprobado()
    {
        using var http = fx.CreateClient();
        var t = await TesoreriaE2E.PreparadaAsync(fx, http, "listas");
        var orden = await TesoreriaE2E.OrdenDeDesembolsoAsync(fx, http, t, 1_900_000m);
        var ordenId = orden.GetProperty("publicId").GetGuid();
        await DocumentoEnListaAsync(http, t.Admin, orden.GetProperty("beneficiaryPersonPublicId").GetGuid());

        TesoreriaE2E.EsExito(await TesoreriaE2E.MandarAsync(http, t.Admin, HttpMethod.Post, $"/api/treasury/payment-orders/{ordenId}/revalidate"), "revalidar");
        var revalidada = await CarteraE2E.GetAsync(http, t.Admin, $"/api/treasury/payment-orders/{ordenId}");
        revalidada.GetProperty("status").GetInt32().Should().Be(2, "pendiente");
        revalidada.GetProperty("pendingReasons").EnumerateArray().Select(r => r.GetString()).Should().Contain("BindingListMatch");

        var lote = await InventarioE2E.MandarAsync(http, t.Admin, HttpMethod.Post, "/api/treasury/payment-batches", new
        {
            bankAccountPublicId = t.CuentaDePagos, form = 1, payDate = DateOnly.FromDateTime(DateTime.Today), orderPublicIds = new[] { ordenId },
        });
        var texto = await lote.Content.ReadAsStringAsync();
        if (lote.IsSuccessStatusCode) JsonNode.Parse(texto)!["orderCount"]!.GetValue<int>().Should().Be(0, "cero órdenes del beneficiario en lista en el lote (SC-025)");
        var alertas = await CarteraE2E.GetAsync(http, t.Admin, "/api/alerts?type=Tesoreria.ListaVinculante");
        alertas.ToString().Should().Contain("Tesoreria.ListaVinculante");
    }

    /// <summary>Cambia el documento de la persona para que el canal simulado la encuentre en lista (termina en 9).</summary>
    private static async Task DocumentoEnListaAsync(HttpClient http, string admin, Guid persona)
    {
        var actual = JsonNode.Parse((await CarteraE2E.GetAsync(http, admin, $"/api/core/people/{persona}")).GetRawText())!.AsObject();
        var documento = actual["taxId"]?.GetValue<string>() ?? "900100";
        actual["taxId"] = documento[..^1] + "9";
        TesoreriaE2E.EsExito(await InventarioE2E.MandarAsync(http, admin, HttpMethod.Put, $"/api/core/people/{persona}", actual), "cambiar el documento");
    }
}
