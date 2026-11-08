using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// Feature 020 (T184; R17; FR-038): una versión de conector no se publica con una URL insegura ni con una dirección interna, en un host sin
/// <c>Conectores:PermitirRedesPrivadas</c> (el de la colección). Tampoco con una credencial fuera de la autenticación.
/// </summary>
[Collection(TransporteCollection.Nombre)]
public class ConectoresSeguridadTests(ApiConProveedorFalsoFixture fx)
{
    [Theory]
    [InlineData("http://proveedor.example.com", "Connector.InsecureUrl")]
    [InlineData("https://usuario:clave@proveedor.example.com", "Connector.InsecureUrl")]
    [InlineData("https://10.1.2.3", "Connector.PrivateAddress")]
    [InlineData("https://169.254.169.254", "Connector.PrivateAddress")]
    [InlineData("https://localhost:8443", "Connector.PrivateAddress")]
    public async Task Una_url_insegura_o_interna_no_se_publica(string url, string codigo)
    {
        using var http = fx.CreateClient();
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        var alta = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/connectors",
            new { code = $"SEG{Math.Abs(url.GetHashCode()) % 100000}", name = "Seguridad" });
        alta.StatusCode.Should().Be(HttpStatusCode.OK, await alta.Content.ReadAsStringAsync());
        var id = (await InventarioE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var r = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Post, $"/api/saas/connectors/{id}/versions",
            new { baseUrl = url, definition = TransporteE2E.DefinicionDeReferencia });

        r.IsSuccessStatusCode.Should().BeFalse();
        (await TransporteE2E.CodigoAsync(r)).Should().Be(codigo);
    }

    [Fact]
    public async Task Una_credencial_en_el_cuerpo_de_la_operacion_no_se_publica()
    {
        using var http = fx.CreateClient();
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        var alta = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/connectors", new { code = "SEGCRED", name = "Credencial fuera" });
        var id = (await InventarioE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();
        var definicion = TransporteE2E.DefinicionDeReferencia.Replace("{{envio.id}}", "{{cred.password}}", StringComparison.Ordinal);

        var r = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Post, $"/api/saas/connectors/{id}/versions",
            new { baseUrl = "https://proveedor.example.com", definition = definicion });

        r.IsSuccessStatusCode.Should().BeFalse();
        (await r.Content.ReadAsStringAsync()).Should().Contain("Connector.CredentialOutsideAuth");
    }

    [Fact]
    public async Task Una_cooperativa_no_ve_las_rutas_del_maestro()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "trnsinmodulo");
        using var http = fx.CreateClient();

        var r = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Get, "/api/saas/connectors", null);

        r.IsSuccessStatusCode.Should().BeFalse();
    }
}
