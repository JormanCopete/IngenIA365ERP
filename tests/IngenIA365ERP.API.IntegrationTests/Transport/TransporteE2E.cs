using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Payroll;

namespace IngenIA365ERP.API.IntegrationTests.Transport;

/// <summary>
/// La colección de las e2e de Transporte y conectores (feature 020): su propio host y contenedores, en paralelo con las demás colecciones.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class TransporteCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Transporte e2e";
}

/// <summary>Ayudantes de las e2e de Transporte: todo por la API con tokens reales, sobre cooperativas aisladas del mismo host.</summary>
public static class TransporteE2E
{
    public const string CodigoDelModulo = "TRN";

    /// <summary>El maestro enciende (o apaga) Transporte en la cooperativa y corre el aprovisionamiento como lo haría el programador.</summary>
    public static async Task HabilitarAsync(CentralIdentityApiFixture fx, HttpClient http, Guid cooperativa, bool habilitado = true)
    {
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        var r = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Put, $"/api/saas/tenants/{cooperativa}/modules/{CodigoDelModulo}",
            new { enabled = habilitado, reason = "Prueba e2e de Transporte" });
        r.StatusCode.Should().Be(HttpStatusCode.NoContent, $"habilitar Transporte: «{await r.Content.ReadAsStringAsync()}»");
        await fx.CorrerTareaAsync(cooperativa, "modulos.aprovisionar");
    }

    /// <summary>El maestro crea un conector y publica su versión 1; devuelve su <c>PublicId</c>.</summary>
    public static async Task<Guid> ConectorAsync(CentralIdentityApiFixture fx, HttpClient http, string codigo, string urlBase, string definicion)
    {
        var maestro = await InventarioE2E.TokenMaestroAsync(fx, http);
        var alta = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Post, "/api/saas/connectors", new { code = codigo, name = $"Conector {codigo}" });
        alta.StatusCode.Should().Be(HttpStatusCode.OK, $"crear el conector: «{await alta.Content.ReadAsStringAsync()}»");
        var id = (await InventarioE2E.LeerAsync(alta)).GetProperty("publicId").GetGuid();
        var version = await InventarioE2E.EnviarAsync(http, maestro, HttpMethod.Post, $"/api/saas/connectors/{id}/versions",
            new { baseUrl = urlBase, definition = definicion, note = "e2e" });
        version.StatusCode.Should().Be(HttpStatusCode.OK, $"publicar la versión: «{await version.Content.ReadAsStringAsync()}»");
        return id;
    }

    /// <summary>Una definición válida con inicio de sesión por token, genérica.</summary>
    public const string DefinicionDeReferencia = """
        {
          "auth": { "kind": "LoginToken", "method": "POST", "path": "/v1/login",
            "body": "{\"username\":\"{{cred.username}}\",\"password\":\"{{cred.password}}\"}",
            "tokenPath": "$.token", "expiresInSeconds": 3600, "header": "Authorization", "prefix": "Bearer " },
          "operation": { "method": "POST", "path": "/v1/asignar",
            "body": "{\"movil\":\"{{vehiculo.radioMovil}}\",\"valor\":{{pago.valor:int}},\"externalID\":\"{{envio.id}}\"}" },
          "credentialFields": [ { "name": "username", "label": "Usuario", "secret": false }, { "name": "password", "label": "Clave", "secret": true } ],
          "responses": [ { "status": 200, "outcome": "Done" }, { "status": 401, "outcome": "ReAuth" }, { "status": 500, "outcome": "Retry" } ]
        }
        """;

    public static async Task<string> CodigoAsync(HttpResponseMessage resp)
    {
        var texto = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(texto);
        return doc.RootElement.TryGetProperty("code", out var c) ? c.GetString() ?? string.Empty : texto;
    }

    public static Task<JsonElement> GetAsync(HttpClient http, string token, string url) => NominaE2E.GetAsync(http, token, url);
}
