using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Enums.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T023 (feature 012; decisiones-transversales T37, T38; FR-008, SC-012; quickstart §3.13 punto 7): la auditoría
/// de los módulos encadenados se escribe en <c>COR_AuditOutbox</c> en la misma transacción que el cambio,
/// <c>AuditOutboxForwarder</c> la sella y la lleva a Mongo, y <c>POST /api/audit/integrity/verify</c> descubre
/// lo que alguien con la credencial de Mongo le haga: alterar, borrar, intercalar, o cambiar un ancla.
///
/// <para>
/// Escrita en la fase 2; se ejecuta tras la migración <c>PlataformaParaInventario</c> (T186), que crea
/// <c>COR_AuditOutbox</c>, <c>COR_AuditChainHeads</c>, <c>COR_AuditAnchors</c> y <c>COR_BackgroundLeases</c>, cuando
/// exista la ruta de parámetros (T067–T071) y cuando <c>DomainPermissionCatalogSeeder</c> siembre
/// <c>AuditLog.VerifyIntegrity</c> (fase 3). La fase 11 (US12, T420) lo <b>amplió</b> con la auditoría completa por HTTP
/// (US12-4, <see cref="La_auditoria_completa_registra_origen_motivo_rechazos_y_navegacion"/>).
/// </para>
///
/// <para>
/// El reenviador está apagado en las pruebas: cada caso lo conduce con <c>fx.ReenviarAuditoriaAsync</c>. La
/// credencial de Mongo del host de pruebas es la del contenedor (administrativa), que es la que permite
/// simular el ataque; la de la API en los clústeres sólo inserta.
/// </para>
/// </summary>
[Collection(InventarioCollection.Nombre)]
public class IntegridadDeAuditoriaTests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/inventory/parameters/INV/Existencias.StockNegativoPermitido/versions";
    private const string Verificar = "/api/audit/integrity/verify";
    private const string BaseDeAuditoria = "IngenIA365ERP_Audit_Test";

    // ------------------------------------------------------------------ casos --

    [Fact]
    public async Task Las_altas_quedan_en_el_outbox_y_el_reenvio_las_sella_en_Mongo_dejando_la_fila_delgada()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "integridad");
        using var http = fx.CreateClient();
        await AltasAsync(http, coop.TokenAdmin, "2026-10-01", "2026-11-01", "2026-12-01");

        var antes = await OutboxAsync(coop.TenantPublicId);
        antes.Where(e => e.Module == "Parameters").Should().HaveCountGreaterThanOrEqualTo(3,
            "cada alta deja al menos el evento del comando en la misma transacción");
        antes.Should().OnlyContain(e => !e.Forwarded && e.PayloadJson != null && e.Seq == null);

        var enviados = await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);

        enviados.Should().Be(antes.Count);
        var despues = await OutboxAsync(coop.TenantPublicId);
        despues.Should().OnlyContain(e => e.Forwarded && e.ForwardedAt != null && e.PayloadJson == null && e.Hash!.Length == 64,
            "tras reenviar queda la fila delgada: nunca DELETE, sólo se vacía la carga");
        despues.Select(e => e.Seq!.Value).OrderBy(s => s).Should().Equal(Enumerable.Range(1, despues.Count).Select(i => (long)i),
            "la cadena no tiene huecos");

        var flujo = AuditoriaEncadenada.Flujo(coop.TenantPublicId);
        var documentos = await Coleccion(coop.TenantPublicId)
            .Find(Builders<BsonDocument>.Filter.Eq("chain.stream", flujo)).ToListAsync();
        documentos.Should().HaveCount(despues.Count);
        foreach (var fila in despues)
        {
            var doc = documentos.Single(d => d["_id"].AsString == fila.EventId.ToString("D"));
            var cadena = doc["chain"].AsBsonDocument;
            cadena["stream"].AsString.Should().Be(flujo);
            cadena["seq"].ToInt64().Should().Be(fila.Seq);
            cadena["prevHash"].AsString.Should().Be(fila.PrevHash);
            cadena["hash"].AsString.Should().Be(fila.Hash);
            cadena["alg"].AsString.Should().Be("SHA-256");
            cadena["v"].ToInt32().Should().Be(1);
        }

        (await AnclasAsync(coop.TenantPublicId)).Should().Contain(a => a.Kind == AuditAnchorKind.Genesis && a.Seq == 0,
            "al activar la cadena queda el ancla génesis");
        (await fx.ReenviarAuditoriaAsync(coop.TenantPublicId)).Should().Be(0, "una segunda pasada no reenvía nada");
    }

    [Fact]
    public async Task La_verificacion_de_una_cadena_limpia_no_informa_nada_y_queda_auditada()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "integridadlimpia");
        using var http = fx.CreateClient();
        await AltasAsync(http, coop.TokenAdmin, "2026-10-01", "2026-11-01");
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        var filas = await OutboxAsync(coop.TenantPublicId);

        var r = await VerificarAsync(http, coop.TokenAdmin);

        r.GetProperty("stream").GetString().Should().Be(AuditoriaEncadenada.Flujo(coop.TenantPublicId), "sin stream, el de 10 años");
        r.GetProperty("fromSeq").GetInt64().Should().Be(1);
        r.GetProperty("toSeq").GetInt64().Should().Be(filas.Count);
        r.GetProperty("checked").GetInt32().Should().Be(filas.Count);
        r.GetProperty("anchorsChecked").GetInt32().Should().BeGreaterThanOrEqualTo(1, "al menos la génesis");
        r.GetProperty("incidents").GetArrayLength().Should().Be(0);

        (await EsperarEventoAsync(coop.TenantPublicId, "AuditLog.IntegrityVerified")).Should().NotBeNull(
            "la verificación misma queda en la auditoría con su resultado");
    }

    [Fact]
    public async Task Alterar_borrar_e_intercalar_en_Mongo_se_informan_por_posicion()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "integridadatacada");
        using var http = fx.CreateClient();
        await AltasAsync(http, coop.TokenAdmin, "2026-10-01", "2026-11-01", "2026-12-01");
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        var filas = (await OutboxAsync(coop.TenantPublicId)).OrderBy(f => f.Seq).ToList();
        filas.Should().HaveCountGreaterThanOrEqualTo(3);
        var coleccion = Coleccion(coop.TenantPublicId);
        var porId = (FiltroDe(filas[0]), FiltroDe(filas[1]), FiltroDe(filas[2]));

        // seq 1: un campo cambiado.
        await coleccion.UpdateOneAsync(porId.Item1, Builders<BsonDocument>.Update.Set("userName", "otra persona"));
        // seq 2: borrado.
        await coleccion.DeleteOneAsync(porId.Item2);
        // seq 3: otro documento con la misma posición (copia con otro _id).
        var copia = (await coleccion.Find(porId.Item3).SingleAsync()).DeepClone().AsBsonDocument;
        copia["_id"] = Guid.NewGuid().ToString("D");
        copia["action"] = "Inventado";
        await coleccion.InsertOneAsync(copia);

        var incidentes = (await VerificarAsync(http, coop.TokenAdmin)).GetProperty("incidents").EnumerateArray()
            .Select(i => (Kind: i.GetProperty("kind").GetString(), Seq: i.GetProperty("seq").GetInt64()))
            .ToList();

        incidentes.Should().Contain(("Altered", filas[0].Seq!.Value));
        incidentes.Should().Contain(("Deleted", filas[1].Seq!.Value), "un salto de secuencia es un evento eliminado");
        incidentes.Should().Contain(("Interleaved", filas[2].Seq!.Value));
        incidentes.Should().NotContain(i => i.Kind == "Altered" && i.Seq == filas[2].Seq, "el original de la posición 3 sigue intacto");
    }

    [Fact]
    public async Task Un_ancla_con_otro_HMAC_es_AnchorInvalid()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "integridadancla");
        using var http = fx.CreateClient();
        await AltasAsync(http, coop.TokenAdmin, "2026-10-01");
        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);

        await ConLaBaseAsync(coop.TenantPublicId, db => db.AuditAnchors
            .Where(a => a.Kind == AuditAnchorKind.Genesis)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Hmac, Convert.ToBase64String(new byte[32]))));

        var incidentes = (await VerificarAsync(http, coop.TokenAdmin)).GetProperty("incidents").EnumerateArray()
            .Select(i => (Kind: i.GetProperty("kind").GetString(), Seq: i.GetProperty("seq").GetInt64()))
            .ToList();

        incidentes.Should().Contain(("AnchorInvalid", 0L));
    }

    [Fact]
    public async Task Un_rango_de_mas_de_diez_anios_responde_Validation_Invalid()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "integridad");
        using var http = fx.CreateClient();

        var resp = await InventarioE2E.EnviarAsync(http, coop.TokenAdmin, HttpMethod.Post, Verificar,
            new { from = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc), to = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc) });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest, await resp.Content.ReadAsStringAsync());
        (await InventarioE2E.CodigoDeErrorAsync(resp)).Should().Be("Validation.Invalid");
    }

    [Fact]
    public async Task Un_comando_rechazado_deja_su_evento_Rejected_aunque_la_transaccion_se_revierta()
    {
        var coop = await InventarioE2E.CooperativaAisladaAsync(fx, "integridadrechazo");
        using var http = fx.CreateClient();

        var resp = await PostAsync(http, coop.TokenAdmin, new
        {
            scopeKind = "None", value = "tal vez", validFrom = "2026-10-01", reason = "Valor no admitido (T023)",
        });
        (await InventarioE2E.CodigoDeErrorAsync(resp)).Should().Be("Parameters.ValueNotAllowed");

        var filas = await OutboxAsync(coop.TenantPublicId);
        var rechazo = filas.Should().ContainSingle(f => f.PayloadJson != null && f.PayloadJson.Contains("\"Rejected\""),
            "el rechazo se escribió por otro contexto y sobrevivió al rollback").Subject;
        rechazo.PayloadJson.Should().Contain("Parameters.ValueNotAllowed");
        (await ConLaBaseAsync(coop.TenantPublicId, db => db.OperationKeys.IgnoreQueryFilters().CountAsync()))
            .Should().Be(0, "la transacción del comando se revirtió: su clave no quedó");

        await fx.ReenviarAuditoriaAsync(coop.TenantPublicId);
        var evento = await EsperarEventoAsync(coop.TenantPublicId, "Rejected");
        evento.Should().NotBeNull();
        evento!["metadata"]["ErrorCode"].AsString.Should().Be("Parameters.ValueNotAllowed");
        evento["module"].AsString.Should().Be("Parameters");
    }

    /// <summary>
    /// T420 (US12-4, SC-012; quickstart §3.13 paso 6), en el escenario aislado «auditoria» con los usuarios del ensayo: crear,
    /// confirmar, aprobar, rechazar y anular un documento, cambiar un parámetro, exportar un informe e ingresar a una opción dejan
    /// eventos con actor, IP, canal (<c>web</c> por <c>X-Canal</c>) y motivo donde se exige; un rechazo queda <c>Rejected</c> con
    /// su código; la clave de idempotencia va en la metadata; el ingreso queda con <c>Module = Navigation</c>; <c>auditor</c>
    /// verifica la cadena sin incidentes y <c>lectura</c> (sin <c>AuditLog.VerifyIntegrity</c>) recibe 404.
    /// </summary>
    [Fact]
    public async Task La_auditoria_completa_registra_origen_motivo_rechazos_y_navegacion()
    {
        var esc = await EscenarioDeInventario.PrepararAsync(fx, "auditoria");
        var u = await UsuariosDelEnsayo.CrearAsync(fx, esc);
        using var http = fx.CreateClient();
        http.DefaultRequestHeaders.Add("X-Canal", "web");
        // El host de pruebas no tiene conexión real: la IP del visitante llega como la pone Cloudflare (IpAddressAccessor).
        http.DefaultRequestHeaders.Add("CF-Connecting-IP", "203.0.113.7");
        var desde = DateTime.UtcNow.AddMinutes(-1);

        // Crear y confirmar; anular con motivo.
        var entrada = await esc.AjusteAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 5, 100m)]);
        await InventarioE2E.ConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", entrada);
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, $"/api/inventory/adjustments/{entrada}/void", new { reason = "Anulado para la auditoría" });
        await esc.AjusteConfirmadoAsync(http, esc.Admin, "AJP", "PRIN", [new("P1", 20, 100m)]);

        // Un rechazo del servidor: la salida que no cabe (antes de la política, que la mandaría a aprobación).
        var imposible = await esc.AjusteAsync(http, esc.Admin, "AJN", "PRIN", [new("P1", 9_999)], causa: "MERMA");
        await InventarioE2E.FallaAsync(await InventarioE2E.PedirConfirmarAsync(http, esc.Admin, "/api/inventory/adjustments", imposible), "Inventory.Stock.Insufficient");

        // Aprobar y rechazar (política de un nivel sobre los ajustes negativos).
        await InventarioE2E.PoliticaAsync(http, esc.Admin, esc.Tipos["AJN"], esc.Corte.AddDays(1), (0m, "Inventory.Adjustments.Approve"));
        var aprobar = await esc.AjusteAsync(http, u.BodegaA.Token, "AJN", "PRIN", [new("P1", 1)], causa: "MERMA");
        var s1 = (await InventarioE2E.ConfirmarAsync(http, u.BodegaA.Token, "/api/inventory/adjustments", aprobar)).GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, s1, aprobar: true)).StatusCode.Should().Be(HttpStatusCode.OK);
        var rechazar = await esc.AjusteAsync(http, u.BodegaA.Token, "AJN", "PRIN", [new("P1", 1)], causa: "MERMA");
        var s2 = (await InventarioE2E.ConfirmarAsync(http, u.BodegaA.Token, "/api/inventory/adjustments", rechazar)).GetProperty("approval").GetProperty("requestPublicId").GetGuid();
        (await InventarioE2E.DecidirAsync(http, esc.Admin, u.Aprobador.Token, s2, aprobar: false, motivo: "Merma sin soporte")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Parámetro, exportación e ingreso a una opción.
        await InventarioE2E.ExitoAsync(http, esc.Admin, HttpMethod.Post, Ruta, new
        {
            scopeKind = "None", value = "false", validFrom = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1).ToString("yyyy-MM-dd"),
            reason = "Parámetro para la auditoría",
        });
        var hoy = InventarioE2E.HoyEnColombia.ToString("yyyy-MM-dd");
        (await InventarioE2E.MandarAsync(http, esc.Admin, HttpMethod.Get,
            $"/api/reports/inventory/kardex?format=xlsx&product={esc.P("P1").Id}&warehouse={esc.Bodega("PRIN")}&from={hoy}&to={hoy}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await InventarioE2E.EnviarAsync(http, esc.Admin, HttpMethod.Post, "/api/audit/access", new { route = "/inventario/kardex", title = "Kardex" }))
            .IsSuccessStatusCode.Should().BeTrue();

        await fx.ReenviarAuditoriaAsync(esc.Coop.TenantPublicId);
        await fx.Factory.Services.GetRequiredService<IAuditService>().FlushAsync();
        var hasta = DateTime.UtcNow.AddMinutes(1);
        var eventos = new List<JsonElement>();
        for (var pagina = 1; pagina <= 10; pagina++)
        {
            var lote = await InventarioE2E.GetAsync(http, esc.Admin,
                $"/api/audit/logs?modules=Inventory,Approvals,Parameters,Navigation&from={desde:O}&to={hasta:O}&page={pagina}&pageSize=100");
            var items = lote.GetProperty("items").EnumerateArray().ToList();
            eventos.AddRange(items);
            if (items.Count < 100) break;
        }
        static string? Texto(JsonElement e, string campo) => e.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var filas = eventos.Select(e => new EventoDeAuditoria(Texto(e, "module"), Texto(e, "action"), Texto(e, "channel"), Texto(e, "ipAddress"),
            Texto(e, "userName"), Texto(e, "operationKey"), Texto(e, "reason"), Texto(e, "result"), Texto(e, "errorCode"))).ToList();

        filas.Should().Contain(e => e.Module == "Inventory" && e.Channel == "web" && e.Ip == "203.0.113.7" && e.UserName != null,
            "cada operación lleva actor, IP y canal");
        filas.Should().Contain(e => e.Module == "Inventory" && e.Action == "ConfirmInventoryDocument" && e.OperationKey != null,
            "la clave de idempotencia de la confirmación va en la metadata");
        filas.Should().Contain(e => e.Reason == "Anulado para la auditoría");
        filas.Should().Contain(e => e.Reason == "Merma sin soporte");
        filas.Should().Contain(e => e.Reason == "Parámetro para la auditoría");
        filas.Should().Contain(e => e.Result == "Rejected" && e.ErrorCode == "Inventory.Stock.Insufficient");
        filas.Should().Contain(e => e.Action == "Inventory.Report.Exported");
        filas.Should().Contain(e => e.Module == "Navigation");
        filas.Should().Contain(e => e.Module == "Approvals");

        var verificacion = await InventarioE2E.EnviarAsync(http, u.Auditor.Token, HttpMethod.Post, Verificar,
            new { from = DateTime.UtcNow.AddHours(-2), to = DateTime.UtcNow.AddHours(1) });
        verificacion.StatusCode.Should().Be(HttpStatusCode.OK, await verificacion.Content.ReadAsStringAsync());
        (await InventarioE2E.LeerAsync(verificacion)).GetProperty("incidents").GetArrayLength().Should().Be(0);
        (await InventarioE2E.EnviarAsync(http, u.Lectura.Token, HttpMethod.Post, Verificar, new { from = DateTime.UtcNow.AddHours(-2), to = DateTime.UtcNow }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------- ayudantes --

    private sealed record EventoDeAuditoria(string? Module, string? Action, string? Channel, string? Ip, string? UserName, string? OperationKey,
        string? Reason, string? Result, string? ErrorCode);

    private static async Task AltasAsync(HttpClient http, string token, params string[] vigencias)
    {
        var valor = true;
        foreach (var desde in vigencias)
        {
            var resp = await PostAsync(http, token, new
            {
                scopeKind = "None", value = valor ? "true" : "false", validFrom = desde, reason = $"Vigencia desde {desde} (T023)",
            });
            resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
            valor = !valor;
        }
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient http, string token, object cuerpo)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, Ruta) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http.SendAsync(InventarioE2E.ConClave(req));
    }

    private static async Task<JsonElement> VerificarAsync(HttpClient http, string token)
    {
        var resp = await InventarioE2E.EnviarAsync(http, token, HttpMethod.Post, Verificar,
            new { from = DateTime.UtcNow.AddHours(-2), to = DateTime.UtcNow.AddHours(1) });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return await InventarioE2E.LeerAsync(resp);
    }

    private async Task<T> ConLaBaseAsync<T>(Guid tenantPublicId, Func<IApplicationDbContext, Task<T>> trabajo)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == tenantPublicId);
        await using var ambito = servicios.GetRequiredService<ITenantDbContextFactory>().Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        return await trabajo(ambito.Db);
    }

    private Task<List<AuditOutboxEntry>> OutboxAsync(Guid tenantPublicId) =>
        ConLaBaseAsync(tenantPublicId, db => db.AuditOutbox.AsNoTracking().OrderBy(e => e.Id).ToListAsync());

    private Task<List<AuditAnchor>> AnclasAsync(Guid tenantPublicId) =>
        ConLaBaseAsync(tenantPublicId, db => db.AuditAnchors.AsNoTracking().ToListAsync());

    private IMongoCollection<BsonDocument> Coleccion(Guid tenantPublicId) =>
        fx.Factory.Services.GetRequiredService<IMongoClient>()
            .GetDatabase(AuditDatabaseNames.Para(BaseDeAuditoria, tenantPublicId.ToString("N")))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);

    private static FilterDefinition<BsonDocument> FiltroDe(AuditOutboxEntry fila) =>
        Builders<BsonDocument>.Filter.Eq("_id", fila.EventId.ToString("D"));

    /// <summary>Lo que va directo por <c>MongoAuditService</c> sale en tandas: se vacía la cola y se espera.</summary>
    private async Task<BsonDocument?> EsperarEventoAsync(Guid tenantPublicId, string accion)
    {
        var auditoria = fx.Factory.Services.GetRequiredService<IAuditService>();
        var filtro = Builders<BsonDocument>.Filter.Eq("action", accion);
        var limite = DateTime.UtcNow.AddSeconds(20);
        do
        {
            await auditoria.FlushAsync();
            var doc = await Coleccion(tenantPublicId).Find(filtro).FirstOrDefaultAsync();
            if (doc is not null) return doc;
            await Task.Delay(500);
        } while (DateTime.UtcNow < limite);
        return null;
    }
}
