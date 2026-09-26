using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;

namespace IngenIA365ERP.API.IntegrationTests.Inventory;

/// <summary>
/// Las pruebas e2e del módulo comercial (feature 012: inventario, compras, ventas, punto de venta,
/// facturación electrónica y la plataforma que los sostiene) comparten UN host. La colección es
/// otra que «Nomina e2e» y «Contabilidad e2e» para que xUnit corra las suites en paralelo sobre
/// contenedores distintos (decisiones-transversales §2.18).
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class InventarioCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Inventario e2e";
}

/// <summary>
/// Ayudantes de las e2e del comercio (T008). Todo pasa por la API con tokens reales; nada se
/// escribe a la base por debajo. Lo que cambia el estado de toda una cooperativa (activar el
/// inventario, cerrar un período, cargar saldos iniciales) corre en una
/// <see cref="CooperativaAisladaAsync">cooperativa aislada</see> del mismo host, en el molde de
/// <see cref="Accounting.ContabilidadE2E.CooperativaAisladaAsync"/>.
/// </summary>
public static class InventarioE2E
{
    /// <summary>Cabecera de idempotencia de las operaciones de pantalla (contracts/api.md §2.3).</summary>
    public const string CabeceraDeClave = "Idempotency-Key";

    /// <summary>
    /// Pone <c>Idempotency-Key</c> con un UUID nuevo y devuelve la misma petición, para encadenar:
    /// <c>await http.SendAsync(InventarioE2E.ConClave(peticion))</c>. Una prueba de repetición que
    /// necesite la MISMA clave la pone a mano.
    /// </summary>
    public static HttpRequestMessage ConClave(HttpRequestMessage peticion)
    {
        peticion.Headers.Remove(CabeceraDeClave);
        peticion.Headers.Add(CabeceraDeClave, Guid.NewGuid().ToString());
        return peticion;
    }

    // ------------------------------------------------------ cooperativa aislada --

    /// <summary>Una cooperativa propia dentro del mismo host, con su administrador y su sucursal «Principal».</summary>
    public sealed class CooperativaAislada
    {
        public required string TokenAdmin { get; init; }
        public required Guid TenantPublicId { get; init; }
        public required Guid SucursalPrincipal { get; init; }
        /// <summary>El primer ejercicio contable, si se pidió iniciar la contabilidad; nulo si no.</summary>
        public int? PrimerEjercicioContable { get; init; }
    }

    private static readonly Dictionary<(CentralIdentityApiFixture, string), Task<CooperativaAislada>> Aisladas = [];
    private static readonly object Cerrojo = new();

    /// <summary>
    /// Alta de una cooperativa con su administrador y la sucursal «Principal» —el mismo camino HTTP
    /// que <see cref="Accounting.ContabilidadE2E.CooperativaAisladaAsync"/>—. Si se indica
    /// <paramref name="primerEjercicioContable"/>, además inicia la contabilidad (PUC solidario,
    /// nivel 5, ese ejercicio abierto), que las pruebas de contabilización por mensajes necesitan.
    /// Una por nombre y fixture: dos pruebas que piden el mismo nombre comparten la cooperativa.
    /// </summary>
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

    private static readonly Dictionary<CentralIdentityApiFixture, (string Token, DateTime Renovar)> TokensDelMaestro = [];
    private static readonly SemaphoreSlim CerrojoDelMaestro = new(1, 1);

    /// <summary>
    /// El access del maestro, uno por fixture y por diez minutos (vive quince). La colección crea una
    /// cooperativa por caso y cada alta iniciaba sesión: pasadas diez en un minuto, el limitador de
    /// <c>POST /api/auth/login</c> (10/min por IP) respondía 429 y la colección caía entera.
    /// </summary>
    public static async Task<string> TokenMaestroAsync(CentralIdentityApiFixture fx, HttpClient http)
    {
        await CerrojoDelMaestro.WaitAsync();
        try
        {
            if (TokensDelMaestro.TryGetValue(fx, out var vigente) && DateTime.UtcNow < vigente.Renovar) return vigente.Token;
            var token = await fx.IniciarSesionMaestroAsync(http);
            TokensDelMaestro[fx] = (token, DateTime.UtcNow.AddMinutes(10));
            return token;
        }
        finally
        {
            CerrojoDelMaestro.Release();
        }
    }

    private static async Task<CooperativaAislada> CooperativaAisladaDeVerdadAsync(CentralIdentityApiFixture fx, string nombre, int? primerEjercicioContable)
    {
        using var http = fx.CreateClient();
        var tokenMaestro = await TokenMaestroAsync(fx, http);
        var correoAdmin = $"admin.{nombre}@coop.inventario.test";
        var alta = await EnviarAsync(http, tokenMaestro, HttpMethod.Post, "/api/saas/tenants/with-admin", new
        {
            name = $"Coop. Inventario {nombre}",
            schemaName = $"tenant_inv_{nombre}",
            subdomain = $"tenant_inv_{nombre}",
            nit = (900_600_000 + Math.Abs(nombre.Sum(c => c) % 100_000)).ToString(),
            legalName = $"Cooperativa Inventario {nombre} E2E",
            contactEmail = $"contacto.{nombre}@coop.inventario.test",
            planType = "Basic",
            maxUsers = 20,
            storageLimitMb = 1024,
            firstAdminEmail = correoAdmin,
        });
        alta.StatusCode.Should().Be(HttpStatusCode.OK, $"alta de la cooperativa {nombre}: «{await alta.Content.ReadAsStringAsync()}»");
        var tenantPublicId = (await LeerAsync(alta)).GetProperty("tenantPublicId").GetGuid();
        var admin = await AceptarInvitacionAsync(fx, http, correoAdmin, $"Inv-{nombre}-2026!");

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

    // ------------------------------------------------------------------- http --

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo) =>
        NominaE2E.EnviarAsync(http, token, metodo, url, cuerpo);

    public static Task<JsonElement> LeerAsync(HttpResponseMessage resp) => NominaE2E.LeerAsync(resp);

    public static Task<JsonElement> GetAsync(HttpClient http, string token, string url) => NominaE2E.GetAsync(http, token, url);

    public static Task<string> CodigoDeErrorAsync(HttpResponseMessage resp) => NominaE2E.CodigoDeErrorAsync(resp);
}
