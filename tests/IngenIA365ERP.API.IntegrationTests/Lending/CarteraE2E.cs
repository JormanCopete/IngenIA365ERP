using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    // ------------------------------------------------------ activación de prueba --

    /// <summary>Lo que deja <see cref="ActivarCarteraAsync"/>: la fecha de activación, la activación y la carga (vacía) activada.</summary>
    public sealed record CarteraActivadaE2E(DateOnly ActivationDate, Guid ActivationPublicId, Guid MigrationLoadPublicId, Guid SessionItemPublicId);

    /// <summary>Opciones de <see cref="ActivarCarteraAsync"/>; por defecto registra los dobles <c>TST</c> y <c>TES</c>.</summary>
    public sealed record OpcionesDeActivacion(bool RegistrarDobles = true, string Motivo = "Activación de Cartera para las e2e");

    private static readonly Dictionary<(CentralIdentityApiFixture, Guid), Task<CarteraActivadaE2E>> Activadas = [];

    /// <summary>
    /// Activa Cartera en una cooperativa aislada <b>sin cartera que migrar</b> (feature 013, T0235, C107), por las rutas reales: el único
    /// camino con que las e2e de las fases 4 a 12 obtienen una cooperativa activada, sin <c>Skip</c>. Registra por
    /// <c>/api/core/integration-participants</c> los dobles <c>TST</c> (Recaudo, recaudador del contrato) y <c>TES</c> (destino
    /// <c>Treasury</c>, que consume <c>TesoreriaDePrueba</c>), los dos con <c>Class = Test</c>; inicia la contabilidad con el ejercicio de hoy
    /// si la cooperativa no la tenía (como <c>CooperativaAisladaAsync(…, primerEjercicioContable)</c>); crea la carga con corte = hoy − 1, la
    /// aplica vacía, siembra el acta de activación y activa con fecha = hoy por <c>POST /api/lending/activation</c>.
    ///
    /// <para>
    /// <b>El acta se siembra</b> con el <c>ApplicationDbContext</c> de la cooperativa —un órgano, una sesión cerrada y un punto <c>Decision</c>
    /// aprobado de sujeto <c>ActivationDifferences</c> sobre la carga— mientras no existan las rutas de órganos y sesiones; US17 (T0311) la
    /// pasa a HTTP. «Hoy» es el del reloj de la suite (<c>IDateTimeService</c> del host). La segunda llamada con la misma cooperativa devuelve
    /// la misma activación.
    /// </para>
    /// </summary>
    public static Task<CarteraActivadaE2E> ActivarCarteraAsync(CentralIdentityApiFixture fx, CooperativaAislada coop, OpcionesDeActivacion? opciones = null)
    {
        lock (Cerrojo)
        {
            if (!Activadas.TryGetValue((fx, coop.TenantPublicId), out var tarea))
            {
                tarea = ActivarCarteraDeVerdadAsync(fx, coop, opciones ?? new OpcionesDeActivacion());
                Activadas[(fx, coop.TenantPublicId)] = tarea;
            }
            return tarea;
        }
    }

    /// <summary>Hoy en el reloj de la suite (el del host).</summary>
    public static DateOnly Hoy(CentralIdentityApiFixture fx) =>
        fx.Factory.Services.GetRequiredService<IngenIA365ERP.Application.Common.Interfaces.IDateTimeService>().HoyLocal;

    private static async Task<CarteraActivadaE2E> ActivarCarteraDeVerdadAsync(CentralIdentityApiFixture fx, CooperativaAislada coop, OpcionesDeActivacion opciones)
    {
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var hoy = Hoy(fx);
        var corte = hoy.AddDays(-1);

        if (coop.PrimerEjercicioContable is null)
        {
            var inicio = await ConClaveAsync(http, admin, HttpMethod.Post, "/api/accounting/setup/initialize", new
            {
                catalogCode = "PUC-SOLIDARIO", movementLevel = 5, niifGroup = 2, firstFiscalYear = hoy.Year, mainBranchPublicId = coop.SucursalPrincipal, fourEyes = false,
            });
            if (!inicio.IsSuccessStatusCode)
                (await CodigoDeErrorAsync(inicio)).Should().Be("Accounting.Setup.AlreadyInitialized", "la activación exige la contabilidad iniciada");
        }

        if (opciones.RegistrarDobles)
        {
            await RegistrarDobleAsync(http, admin, new
            {
                code = "TST", @class = "Test", role = "ObligationsCollector", destination = (string?)null, isEnabled = true, isTestDouble = true,
                adoptions = new[] { new { contract = "ProveedorDeObligaciones", version = 1 } }, validFrom = corte,
                reason = "Recaudador de prueba para las e2e de Cartera",
            });
            await RegistrarDobleAsync(http, admin, new
            {
                code = "TES", @class = "Test", role = "MessageDestination", destination = "Treasury", isEnabled = true, isTestDouble = true,
                adoptions = new[] { new { contract = "OrdenDeGiroEmitida", version = 1 }, new { contract = "AnulacionDeOrdenDeGiro", version = 1 } },
                validFrom = corte, reason = "Tesorería de prueba para las e2e de Cartera",
            });
        }

        var alta = await ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/migration/loads", new { cutoffDate = corte, notes = "Sin cartera que migrar (e2e)" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"crear la carga: «{await alta.Content.ReadAsStringAsync()}»");
        var carga = (await LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var aplicar = await ConClaveAsync(http, admin, HttpMethod.Post, $"/api/lending/migration/loads/{carga}/apply", new { reason = "Carga vacía (e2e)" });
        aplicar.IsSuccessStatusCode.Should().BeTrue($"aplicar la carga: «{await aplicar.Content.ReadAsStringAsync()}»");

        var punto = await SembrarActaDeActivacionAsync(fx, coop.TenantPublicId, carga, corte);

        var activar = await ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/activation", new
        {
            migrationLoadPublicId = carga, activationDate = hoy, sessionItemPublicId = punto, acknowledgeWarnings = true, reason = opciones.Motivo,
        });
        activar.StatusCode.Should().Be(HttpStatusCode.Created, $"activar Cartera: «{await activar.Content.ReadAsStringAsync()}»");
        var activacion = await LeerAsync(activar);
        return new CarteraActivadaE2E(DateOnly.Parse(activacion.GetProperty("activationDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            activacion.GetProperty("activationPublicId").GetGuid(), carga, punto);
    }

    /// <summary>Registra un doble si no hay ya uno vigente con ese código (la cooperativa puede traerlo de una prueba anterior).</summary>
    private static async Task RegistrarDobleAsync(HttpClient http, string token, object cuerpo)
    {
        var codigo = (string)cuerpo.GetType().GetProperty("code")!.GetValue(cuerpo)!;
        var registro = await GetAsync(http, token, "/api/core/integration-participants?class=Test");
        if (registro.EnumerateArray().Any(p => p.GetProperty("code").GetString() == codigo && p.GetProperty("validTo").ValueKind == JsonValueKind.Null))
            return;
        var resp = await ConClaveAsync(http, token, HttpMethod.Post, "/api/core/integration-participants", cuerpo);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"registrar el doble {codigo}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>
    /// El acta de activación sembrada en la base de la cooperativa (hasta US17, T0311): órgano, sesión cerrada y punto <c>Decision</c>
    /// aprobado de sujeto <c>ActivationDifferences</c> sobre la carga. Devuelve el <c>PublicId</c> del punto.
    /// </summary>
    public static async Task<Guid> SembrarActaDeActivacionAsync(CentralIdentityApiFixture fx, Guid tenantPublicId, Guid cargaPublicId, DateOnly fecha)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var entrada = (await servicios.GetRequiredService<IngenIA365ERP.Application.Common.Interfaces.ITenantDirectory>().ListActiveAsync(CancellationToken.None))
            .Single(t => t.PublicId == tenantPublicId);
        await using var ambito = servicios.GetRequiredService<IngenIA365ERP.Application.Common.Interfaces.ITenantDbContextFactory>()
            .Abrir(entrada.DatabaseName!, entrada.ConnectionString);
        var db = (IngenIA365ERP.Persistence.DbContext.ApplicationDbContext)ambito.Db;

        var usuario = await db.Users.Select(u => (int?)u.Id).FirstOrDefaultAsync();
        var organo = await db.GoverningBodies.FirstOrDefaultAsync(o => o.Code == "CONSEJOE2E");
        if (organo is null)
        {
            organo = new GoverningBody
            {
                Code = "CONSEJOE2E", Name = "Consejo de administración (e2e)", Kind = GoverningBodyKind.BoardOfDirectors, DecidesCredit = true,
                MinutesPrefix = "CAE2E-", CreatedBy = "e2e",
            };
            db.GoverningBodies.Add(organo);
            await db.SaveChangesAsync();
        }

        var sesion = new Session
        {
            GoverningBodyId = organo.Id, Number = $"CAE2E-{cargaPublicId.ToString("N")[..8]}", Kind = SessionKind.Extraordinary,
            Mode = SessionMode.InPerson, HeldOn = fecha, Status = SessionStatus.Closed, ClosedAt = DateTime.UtcNow, ClosedByUserId = usuario,
            QuorumMet = true, Notes = "Acta sembrada por CarteraE2E.ActivarCarteraAsync (C107) hasta que US17 publique las rutas de sesiones.",
            CreatedBy = "e2e",
        };
        db.LendingSessions.Add(sesion);
        await db.SaveChangesAsync();

        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority,
            SubjectType = LendingDecisionSubjects.ActivationDifferences, SubjectPublicId = cargaPublicId,
            Title = "Activación de Cartera sin cartera previa", Outcome = CreditDecisionOutcome.Approve, VotesFor = 1, VotesAgainst = 0, Abstentions = 0,
            CreatedBy = "e2e",
        };
        db.SessionItems.Add(punto);
        await db.SaveChangesAsync();
        return punto.PublicId;
    }

    /// <summary>Una petición con el token y una <c>Idempotency-Key</c> nueva (o la dada).</summary>
    public static Task<HttpResponseMessage> ConClaveAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo, Guid? clave = null) =>
        InventarioE2E.MandarAsync(http, token, metodo, url, cuerpo, clave);

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
