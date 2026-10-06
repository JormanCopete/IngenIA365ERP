using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Payroll;

namespace IngenIA365ERP.API.IntegrationTests.Lending;

/// <summary>
/// Colección «Cartera e2e» (feature 013, T0014; decisiones-transversales §2.25): un host propio, con sus contenedores,
/// para las e2e de Cartera. Lo que activa Cartera corre en una cooperativa aislada por prueba
/// (<see cref="CarteraE2E.CooperativaAisladaAsync"/>): la activación es irreversible y cambia lo que ven Comercial y Nómina.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class CarteraCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Cartera e2e";
}

/// <summary>
/// Ayudantes de las e2e de Cartera (feature 013, T0014): la clave de idempotencia, la cooperativa aislada (mismo camino
/// HTTP que <c>ContabilidadE2E</c> e <c>InventarioE2E</c>) y las pasadas a mano de los trabajos de fondo, que la fixture deja
/// apagados. El ayudante del <c>ProcesadorDeCartera</c> y <c>ActivarCarteraAsync</c> los agrega la fase Fundacional B.
/// </summary>
public static class CarteraE2E
{
    public const string CabeceraDeClave = "Idempotency-Key";

    /// <summary>Pone una <c>Idempotency-Key</c> nueva; la prueba de repetición pone la misma clave a mano.</summary>
    public static HttpRequestMessage ConClave(HttpRequestMessage peticion)
    {
        peticion.Headers.Remove(CabeceraDeClave);
        peticion.Headers.Add(CabeceraDeClave, Guid.NewGuid().ToString());
        return peticion;
    }

    // ------------------------------------------------------ cooperativa aislada --

    public sealed class CooperativaAislada
    {
        public required string TokenAdmin { get; init; }
        public required Guid TenantPublicId { get; init; }
        public required Guid SucursalPrincipal { get; init; }
        public int? PrimerEjercicioContable { get; init; }
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<CooperativaAislada>> Aisladas = [];
    private static readonly object Cerrojo = new();
    private static int _siguienteNit;

    /// <summary>Una cooperativa por nombre y fixture; la segunda llamada con el mismo nombre devuelve la misma.</summary>
    public static Task<CooperativaAislada> CooperativaAisladaAsync(CentralIdentityApiFixture fx, string nombre, int? primerEjercicioContable = null)
    {
        lock (Cerrojo)
        {
            if (!Aisladas.TryGetValue((fx, nombre), out var tarea))
            {
                tarea = CooperativaAisladaDeVerdadAsync(fx, nombre, primerEjercicioContable);
                Aisladas[(fx, nombre)] = tarea;
            }
            return tarea;
        }
    }

    /// <summary>El token del maestro, compartido con Inventario: el limitador de login admite 10 por minuto por IP.</summary>
    public static Task<string> TokenMaestroAsync(CentralIdentityApiFixture fx, HttpClient http) => InventarioE2E.TokenMaestroAsync(fx, http);

    private static async Task<CooperativaAislada> CooperativaAisladaDeVerdadAsync(CentralIdentityApiFixture fx, string nombre, int? primerEjercicioContable)
    {
        using var http = fx.CreateClient();
        var tokenMaestro = await TokenMaestroAsync(fx, http);
        var correoAdmin = $"admin.{nombre}@coop.cartera.test";
        var alta = await EnviarAsync(http, tokenMaestro, HttpMethod.Post, "/api/saas/tenants/with-admin", new
        {
            name = $"Coop. Cartera {nombre}",
            schemaName = $"tenant_car_{nombre}",
            subdomain = $"tenant_car_{nombre}",
            nit = (900_700_000 + Interlocked.Increment(ref _siguienteNit)).ToString(),
            legalName = $"Cooperativa Cartera {nombre} E2E",
            contactEmail = $"contacto.{nombre}@coop.cartera.test",
            planType = "Basic",
            maxUsers = 20,
            storageLimitMb = 1024,
            firstAdminEmail = correoAdmin,
        });
        alta.StatusCode.Should().Be(HttpStatusCode.OK, $"alta de la cooperativa {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var tenantPublicId = (await LeerAsync(alta)).GetProperty("tenantPublicId").GetGuid();
        var admin = await AceptarInvitacionAsync(fx, http, correoAdmin, $"Car-{nombre}-2026!");

        var sucursal = await EnviarAsync(http, admin, HttpMethod.Post, "/api/core/branches", new { name = "Principal", shortName = "PPAL" });
        sucursal.IsSuccessStatusCode.Should().BeTrue($"sucursal: «{await sucursal.Content.ReadAsStringAsync()}»");
        var sucursales = await GetAsync(http, admin, "/api/core/branches?PageNumber=1&PageSize=10");
        var principal = sucursales.GetProperty("items").EnumerateArray().First().GetProperty("publicId").GetGuid();

        if (primerEjercicioContable is { } ejercicio)
        {
            var inicio = await EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/setup/initialize", new
            {
                catalogCode = "PUC-SOLIDARIO", movementLevel = 5, niifGroup = 2, firstFiscalYear = ejercicio, mainBranchPublicId = principal, fourEyes = false,
            });
            inicio.IsSuccessStatusCode.Should().BeTrue($"iniciar la contabilidad: «{await inicio.Content.ReadAsStringAsync()}»");
        }

        return new CooperativaAislada
        {
            TokenAdmin = admin, TenantPublicId = tenantPublicId, SucursalPrincipal = principal, PrimerEjercicioContable = primerEjercicioContable,
        };
    }

    private static async Task<string> AceptarInvitacionAsync(CentralIdentityApiFixture fx, HttpClient http, string correo, string clave)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull($"la invitación de {correo} tiene que haber salido por correo");
        var token = System.Text.RegularExpressions.Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        token.Success.Should().BeTrue("el correo de invitación trae token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = clave } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aceptar la invitación de {correo}: «{await resp.Content.ReadAsStringAsync()}»");
        var acceso = (await LeerAsync(resp)).GetProperty("accessToken").GetString();
        acceso.Should().NotBeNullOrWhiteSpace();
        return acceso!;
    }

    // -------------------------------------------------- trabajos de fondo a mano --

    /// <summary>Pasadas del despachador de la bandeja para una cooperativa (la fixture lo deja apagado).</summary>
    public static Task DespacharAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, int pasadas = 3) =>
        fx.Despachador.PasadasAsync(tenantPublicId, pasadas);

    public static Task CorrerTareasProgramadasAsync(CentralIdentityApiFixture fx, Guid tenantPublicId) =>
        fx.CorrerTareasProgramadasAsync(tenantPublicId);

    public static Task<int> ReenviarAuditoriaAsync(CentralIdentityApiFixture fx, Guid tenantPublicId) =>
        fx.ReenviarAuditoriaAsync(tenantPublicId);

    // ------------------------------------------------------------------- http --

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo) =>
        NominaE2E.EnviarAsync(http, token, metodo, url, cuerpo);

    public static Task<JsonElement> LeerAsync(HttpResponseMessage resp) => NominaE2E.LeerAsync(resp);

    public static Task<JsonElement> GetAsync(HttpClient http, string token, string url) => NominaE2E.GetAsync(http, token, url);

    public static Task<string> CodigoDeErrorAsync(HttpResponseMessage resp) => NominaE2E.CodigoDeErrorAsync(resp);
}

/// <summary>Prueba humo de la colección (T0014): el host arranca y una cooperativa aislada opera con su token.</summary>
[Collection(CarteraCollection.Nombre)]
public sealed class CarteraE2EHumoTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Una_cooperativa_aislada_arranca_y_responde_con_su_token()
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, "humo");
        using var http = fx.CreateClient();
        var politica = await CarteraE2E.GetAsync(http, coop.TokenAdmin, "/api/auth/session-policy");
        politica.ValueKind.Should().Be(JsonValueKind.Object);
        coop.TenantPublicId.Should().NotBeEmpty();
    }
}
