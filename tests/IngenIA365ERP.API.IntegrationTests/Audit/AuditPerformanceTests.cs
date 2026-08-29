using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace IngenIA365ERP.API.IntegrationTests.Audit;

/// <summary>
/// T085 / SC-004 — con 50.000 eventos, una consulta de un mes responde p95
/// &lt; 5 s.
///
/// <para>
/// <b>No corre en el run estándar</b>: hace falta <c>RUN_PERF_TESTS=1</c>,
/// siguiendo el precedente de <c>LoginThroughputFact</c>. El gate va ANTES de
/// sembrar — antes se pagaban los 50.000 eventos para después fallar.
/// </para>
///
/// <para>
/// <b>Qué estaba roto.</b> Decía «RED esperado hasta T091» y T091 llevaba meses
/// cerrado. Lo viejo era la prueba, y en cinco cosas a la vez: pegaba con un
/// cliente ANÓNIMO a un endpoint autenticado; consultaba abril de 2026 mientras
/// sembraba con la hora actual; sembraba sin cooperativa, y el endpoint exige
/// una (<c>Session.TenantNotSelected</c>); encolaba por <c>IAuditService</c>,
/// cuyo <c>FlushAsync</c> vacía un lote por llamada, así que de 50.000 llegaban
/// unos cientos; y descartaba en silencio toda muestra que no fuera 200, de modo
/// que el fallo final culpaba a una tarea terminada en vez de decir que no había
/// medido nada.
/// </para>
///
/// <para>
/// Ahora siembra <b>directo contra la base de auditoría de la cooperativa</b>
/// con <c>InsertMany</c> —determinista, sin cola de por medio— y comprueba que
/// el dataset está antes de cronometrar. El nombre de la base lo compone
/// <see cref="AuditDatabaseNames"/>, el mismo sitio que usa producción: si la
/// prueba lo escribiera a mano, mediría sobre una base que nadie lee.
/// </para>
/// </summary>
[Trait("category", "perf")]
public class AuditPerformanceTests(CentralIdentityApiFixture fx)
    : IClassFixture<CentralIdentityApiFixture>
{
    private const int EventCount = 50_000;
    private static readonly TimeSpan P95Budget = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Consultar_un_mes_sobre_50k_eventos_responde_bajo_5s_p95()
    {
        if (Environment.GetEnvironmentVariable("RUN_PERF_TESTS") != "1") return;

        using var http = fx.CreateClient();

        // Una cooperativa con su admin: el endpoint exige cooperativa activa, y
        // el maestro no puede seleccionar una de la que no es miembro.
        var masterToken = await fx.IniciarSesionMaestroAsync(http);
        var cooperativa = await RegistrarCooperativaAsync(http, masterToken);
        var tokenAdmin = await AceptarInvitacionAsync(
            http, "auditor@coop.perf.test", "Auditor-Perf-2026!");

        // «Al menos», no «exactamente»: el alta de la cooperativa y la
        // aceptación de la invitación dejan sus propios eventos de auditoría en
        // esta misma colección. Exigir el número exacto haría fallar la prueba
        // por hacer bien su preparación.
        var sembrados = SembrarEnMongo(cooperativa, EventCount);
        Assert.True(sembrados >= EventCount,
            $"quedaron {sembrados:N0} documentos y se sembraron {EventCount:N0}");

        // La ventana se calcula desde ahora. Un rango escrito a mano sólo
        // coincidiría el mes en que se escribió la prueba — y eso es justo lo
        // que hacía que no midiera nada.
        var ahora = DateTime.UtcNow;
        var url = "/api/audit/logs?"
            + $"from={ahora.AddDays(-15):yyyy-MM-dd}&to={ahora.AddDays(15):yyyy-MM-dd}&pageSize=50";

        var muestras = new List<TimeSpan>(20);
        var total = 0;

        for (var i = 0; i < 20; i++)
        {
            var reloj = Stopwatch.StartNew();
            var resp = await ConTokenAsync(http, url, tokenAdmin);
            reloj.Stop();

            if (resp.StatusCode != HttpStatusCode.OK)
            {
                // El cuerpo, no sólo el número: un 401 seco no distingue entre
                // token ausente, permiso ausente y cooperativa sin seleccionar.
                Assert.Fail($"GET {url} respondió {(int)resp.StatusCode}: "
                    + await resp.Content.ReadAsStringAsync());
            }

            total = (await LeerJsonAsync(resp)).GetProperty("totalCount").GetInt32();
            muestras.Add(reloj.Elapsed);
        }

        // Sin esto la prueba mediría la latencia de devolver una página vacía.
        Assert.True(total >= EventCount,
            $"la consulta ve {total:N0} eventos de {EventCount:N0}: medir p95 sobre esto no dice nada");

        var ordenadas = muestras.OrderBy(t => t).ToList();
        var p95 = ordenadas[Math.Max(0, (int)Math.Ceiling(ordenadas.Count * 0.95) - 1)];

        Assert.True(p95 < P95Budget,
            $"SC-004 exige p95 < 5s — observado {p95.TotalMilliseconds:N0} ms sobre {total:N0} eventos");
    }

    /// <summary>
    /// Inserta el dataset en la base de auditoría de la cooperativa, por lotes.
    /// Devuelve cuántos documentos quedaron.
    ///
    /// <para>
    /// Va directo al motor y no por <c>IAuditService</c> a propósito: ese camino
    /// encola y vacía un lote por llamada, de forma desatendida, así que quien
    /// siembra no puede saber cuándo terminó. Para una prueba de rendimiento eso
    /// es peor que lento: es no saber sobre qué se está midiendo.
    /// </para>
    /// </summary>
    private int SembrarEnMongo(Guid cooperativa, int cuantos)
    {
        var config = fx.Factory.Services.GetRequiredService<IConfiguration>();
        var cadena = config["MongoDb:ConnectionString"]!;
        var prefijo = config["MongoDb:DatabaseName"]!;

        var tenantId = cooperativa.ToString("N");
        var coleccion = new MongoClient(cadena)
            .GetDatabase(AuditDatabaseNames.Para(prefijo, tenantId))
            .GetCollection<AuditLog>(AuditDatabaseNames.Coleccion);

        var ahora = DateTime.UtcNow;
        var lote = new List<AuditLog>(1000);

        for (var i = 0; i < cuantos; i++)
        {
            lote.Add(new AuditLog
            {
                TenantId = tenantId,
                UserId = "system:perf",
                UserName = "perf",
                Action = i % 3 == 0 ? "Created" : i % 3 == 1 ? "Updated" : "Viewed",
                EntityType = "PerformanceProbe",
                EntityId = $"probe-{i}",
                Module = "Audit.PerfTest",
                Endpoint = "/api/seed",
                HttpMethod = "POST",
                HttpStatusCode = 200,
                DurationMs = 1,
                // Repartidos hacia atrás en dos semanas: un dataset con todos los
                // eventos en el mismo instante no ejercita el índice por fecha.
                Timestamp = ahora.AddMinutes(-(i % 20_160)),
            });

            if (lote.Count == 1000)
            {
                coleccion.InsertMany(lote);
                lote.Clear();
            }
        }

        if (lote.Count > 0) coleccion.InsertMany(lote);

        return (int)coleccion.CountDocuments(FilterDefinition<AuditLog>.Empty);
    }

    // === Ayudantes (mismo patrón que Security_TenantAdminScope) ===

    private static async Task<string> LoginAsync(HttpClient http, string email, string password)
    {
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var token = (await LeerJsonAsync(resp)).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    private async Task<Guid> RegistrarCooperativaAsync(HttpClient http, string masterToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/saas/tenants/with-admin")
        {
            Content = JsonContent.Create(new
            {
                name = "Coop. Rendimiento Auditoria",
                schemaName = "tenant_auditoria_perf",
                subdomain = "tenant_auditoria_perf",
                nit = "900999111",
                legalName = "Coop. Rendimiento Auditoria SAS",
                contactEmail = "contacto@coop.perf.test",
                planType = "Basic",
                maxUsers = 50,
                storageLimitMb = 5120,
                firstAdminEmail = "auditor@coop.perf.test",
            }),
        };
        req.Headers.Authorization = new("Bearer", masterToken);
        var resp = await http.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await LeerJsonAsync(resp)).GetProperty("tenantPublicId").GetGuid();
    }

    private async Task<string> AceptarInvitacionAsync(HttpClient http, string correo, string password)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        Assert.NotNull(mensaje);
        var token = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        Assert.True(token.Success, "El correo de invitación no contiene token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new
        {
            token = token.Groups[1].Value,
            registration = new { password },
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var acceso = (await LeerJsonAsync(resp)).GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(acceso));
        return acceso!;
    }

    private static async Task<HttpResponseMessage> ConTokenAsync(
        HttpClient http, string url, string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new("Bearer", token);
        return await http.SendAsync(req);
    }

    private static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(raw, Json);
    }
}
