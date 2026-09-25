using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Audit.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// T021 (feature 012; decisiones-transversales T13, T14; FR-016, SC-002; contracts/api.md §2.3): una
/// operación de pantalla se ejecuta una sola vez por <c>Idempotency-Key</c>, sobre la ruta de plataforma
/// <c>POST /api/inventory/parameters/INV/Existencias.StockNegativoPermitido/versions</c>
/// (<c>AddParameterVersionCommand</c>), en PostgreSQL y SQL Server.
///
/// <para>
/// Escrita en la fase 2; se ejecuta tras la migración <c>PlataformaParaInventario</c> (T186), que crea
/// <c>COR_OperationKeys</c> y <c>COR_ParameterVersions</c>, y cuando la ruta de parámetros exista (T067–T071).
/// Lo que InMemory no puede ver —que un fallo revierta la fila y que el duplicado concurrente espere en el
/// índice único— sólo se prueba aquí.
/// </para>
///
/// <para>
/// Cada caso usa su propia cooperativa aislada: todos escriben vigencias de la misma clave, y una vigencia
/// que empieza después de otra de otro caso sería <c>Parameters.Overlaps</c> según el orden de ejecución.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class IdempotenciaDeOperacionesTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/inventory/parameters/INV/Existencias.StockNegativoPermitido/versions";
    private const string Historia = "/api/inventory/parameters/INV/Existencias.StockNegativoPermitido/history?scopeKind=None";
    private const string BaseDeAuditoria = "IngenIA365ERP_Audit_Test";

    private static object Cuerpo(string valor, string validFrom = "2026-10-01") =>
        new { scopeKind = "None", value = valor, validFrom, reason = "Prueba de idempotencia (T021)" };

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, object cuerpo, Guid? clave)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, Ruta) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (clave is { } k) req.Headers.Add(InventarioE2E.CabeceraDeClave, k.ToString());
        return await http.SendAsync(req);
    }

    private static bool Repetida(HttpResponseMessage resp) =>
        resp.Headers.TryGetValues("Idempotent-Replayed", out var v) && v.Contains("true");

    private static async Task<int> VigenciasAsync(HttpClient http, string token, string validFrom)
    {
        var historia = await InventarioE2E.GetAsync(http, token, Historia);
        return historia.EnumerateArray().Count(v => v.GetProperty("validFrom").GetString() == validFrom);
    }

    private IMongoCollection<BsonDocument> Auditoria(Guid tenantPublicId) =>
        fx.Factory.Services.GetRequiredService<IMongoClient>()
            .GetDatabase(AuditDatabaseNames.Para(BaseDeAuditoria, tenantPublicId.ToString("N")))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);

    private async Task<long> EventosDeRepeticionAsync(Guid tenantPublicId, Guid clave)
    {
        var auditoria = fx.Factory.Services.GetRequiredService<IAuditService>();
        var filtro = Builders<BsonDocument>.Filter.Eq("action", "Operation.Replayed")
            & Builders<BsonDocument>.Filter.Regex("metadata.OperationKey", new BsonRegularExpression(clave.ToString()));
        var limite = DateTime.UtcNow.AddSeconds(20);
        long cuantos;
        do
        {
            await auditoria.FlushAsync();
            cuantos = await Auditoria(tenantPublicId).CountDocumentsAsync(filtro);
            if (cuantos >= 2) return cuantos;
            await Task.Delay(500);
        } while (DateTime.UtcNow < limite);
        return cuantos;
    }

    [Fact]
    public async Task La_misma_clave_tres_veces_deja_una_sola_vigencia_y_la_misma_respuesta()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "idemrepite");
        using var http = fx.CreateClient();
        var clave = Guid.NewGuid();
        var cuerpo = Cuerpo("true");

        var primera = await EnviarAsync(http, coop.TokenAdmin, cuerpo, clave);
        var segunda = await EnviarAsync(http, coop.TokenAdmin, cuerpo, clave);
        var tercera = await EnviarAsync(http, coop.TokenAdmin, cuerpo, clave);

        primera.StatusCode.Should().Be(HttpStatusCode.Created, await primera.Content.ReadAsStringAsync());
        Repetida(primera).Should().BeFalse();
        var cuerpoPrimera = await primera.Content.ReadAsStringAsync();
        foreach (var repetida in new[] { segunda, tercera })
        {
            repetida.StatusCode.Should().Be(HttpStatusCode.Created, "la repetición responde el mismo estado");
            Repetida(repetida).Should().BeTrue("la repetición lleva Idempotent-Replayed: true");
            JsonDocument.Parse(await repetida.Content.ReadAsStringAsync()).RootElement.GetRawText()
                .Should().Be(JsonDocument.Parse(cuerpoPrimera).RootElement.GetRawText(), "y el mismo cuerpo");
        }

        (await VigenciasAsync(http, coop.TokenAdmin, "2026-10-01")).Should().Be(1, "un solo efecto");
        (await EventosDeRepeticionAsync(coop.TenantPublicId, clave)).Should().Be(2, "cada repetición deja Operation.Replayed");
    }

    [Fact]
    public async Task La_misma_clave_con_otro_cuerpo_es_Operation_KeyReused()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "idemotro");
        using var http = fx.CreateClient();
        var clave = Guid.NewGuid();

        var primera = await EnviarAsync(http, coop.TokenAdmin, Cuerpo("true"), clave);
        primera.StatusCode.Should().Be(HttpStatusCode.Created, await primera.Content.ReadAsStringAsync());

        var otra = await EnviarAsync(http, coop.TokenAdmin, Cuerpo("false"), clave);

        otra.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var sobre = await InventarioE2E.LeerAsync(otra);
        sobre.GetProperty("code").GetString().Should().Be("Operation.KeyReused");
        sobre.GetProperty("data").GetProperty("operation").GetString().Should().Be("AddParameterVersionCommand");
        sobre.GetProperty("data").GetProperty("firstUsedAt").GetDateTime().Should().BeBefore(DateTime.UtcNow.AddMinutes(1));
        (await VigenciasAsync(http, coop.TokenAdmin, "2026-10-01")).Should().Be(1);
    }

    [Fact]
    public async Task Sin_cabecera_responde_400_Operation_KeyRequired()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "idemsin");
        using var http = fx.CreateClient();

        var sin = await EnviarAsync(http, coop.TokenAdmin, Cuerpo("true"), clave: null);

        sin.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InventarioE2E.CodigoDeErrorAsync(sin)).Should().Be("Operation.KeyRequired");
        (await VigenciasAsync(http, coop.TokenAdmin, "2026-10-01")).Should().Be(0);
    }

    [Fact]
    public async Task Un_intento_que_falla_no_deja_la_clave_y_reintentar_con_ella_corrige()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "idemfalla");
        using var http = fx.CreateClient();
        var clave = Guid.NewGuid();

        var fallida = await EnviarAsync(http, coop.TokenAdmin, Cuerpo("quizas"), clave);
        fallida.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await InventarioE2E.CodigoDeErrorAsync(fallida)).Should().Be("Parameters.ValueNotAllowed");

        // La transacción se revirtió con la fila de COR_OperationKeys: la misma clave con el cuerpo
        // corregido es una operación nueva, no Operation.KeyReused.
        var corregida = await EnviarAsync(http, coop.TokenAdmin, Cuerpo("true"), clave);

        corregida.StatusCode.Should().Be(HttpStatusCode.Created, await corregida.Content.ReadAsStringAsync());
        Repetida(corregida).Should().BeFalse();
        (await VigenciasAsync(http, coop.TokenAdmin, "2026-10-01")).Should().Be(1);
    }

    [Fact]
    public async Task Dos_peticiones_simultaneas_con_la_misma_clave_dejan_un_solo_efecto()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "idemconc");
        using var http = fx.CreateClient();
        var clave = Guid.NewGuid();
        var cuerpo = Cuerpo("true");

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => EnviarAsync(http, coop.TokenAdmin, cuerpo, clave)));

        respuestas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created,
            "el duplicado espera en el índice único y después recibe la respuesta guardada");
        respuestas.Count(r => !Repetida(r)).Should().Be(1, "una sola ejecutó; las demás son repeticiones");
        (await VigenciasAsync(http, coop.TokenAdmin, "2026-10-01")).Should().Be(1);
    }
}
