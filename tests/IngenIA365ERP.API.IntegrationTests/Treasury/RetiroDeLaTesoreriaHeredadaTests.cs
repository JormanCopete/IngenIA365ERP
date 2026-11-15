using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Receipts;

namespace IngenIA365ERP.API.IntegrationTests.Treasury;

/// <summary>
/// Feature 015, fase 46, T0471 (data-model §24; FR-161): por HTTP, las rutas de la Tesorería heredada responden 404 sin alias.
///
/// <para>Consolidación de migraciones (2026-11-15): las dos pruebas de la migración destructiva <c>RetiroDeLaTesoreriaHeredada</c>
/// (la guarda con filas y la irreversibilidad), y <c>SacarElRetiroAsync</c>, que las de la 012, la 013 y la 014 usaban para recorrer el
/// historial hacia atrás, se retiraron: la migración quedó absorbida por <c>EsquemaBase</c>, ya pasó por todas las bases que existían y
/// una base nueva nace sin las tres tablas. Siguen en git.</para>
/// </summary>
[Collection(RecaudoYTesoreriaCollection.Nombre)]
public class RetiroDeLaTesoreriaHeredadaTests(CentralIdentityApiFixture fx)
{
    internal const string Retiro = "RetiroDeLaTesoreriaHeredada";

    [Fact]
    public async Task Por_HTTP_las_rutas_heredadas_responden_404_sin_alias()
    {
        var coop = await RecaudoYTesoreriaE2E.CooperativaAisladaAsync(fx, "tes_retiro_http");
        using var http = fx.CreateClient();
        foreach (var ruta in new[] { "/api/treasury/checks", "/api/treasury/concepts", "/api/treasury/invoices", "/api/treasury/cash-flow?year=2026",
                     "/api/treasury/legacy-export/tables" })
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", coop.TokenAdmin);
            var resp = await http.SendAsync(pedido);
            resp.StatusCode.Should().Be(HttpStatusCode.NotFound, $"{ruta} se retiró sin alias (FR-161)");
        }
    }
}
