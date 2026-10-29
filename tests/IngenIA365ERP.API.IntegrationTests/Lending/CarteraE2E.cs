using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Inventory;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
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
/// apagados, la activación de prueba con su acta por HTTP y las personas y usuarios que piden las e2e de gobierno.
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

        /// <summary>El correo del administrador (<c>CompanyAdmin</c>): con él se busca su usuario para asociarle su persona (T0311).</summary>
        public string? CorreoAdmin { get; init; }
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
            // Los usuarios del ensayo de Cartera (quickstart §2.7, UsuariosDeCartera) son diecinueve más el administrador.
            maxUsers = 40,
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
            CorreoAdmin = correoAdmin,
        };
    }

    /// <summary>Acepta la invitación que salió por correo para <paramref name="correo"/> y devuelve el access de la sesión.</summary>
    public static async Task<string> AceptarInvitacionAsync(CentralIdentityApiFixture fx, HttpClient http, string correo, string clave)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull($"la invitación de {correo} tiene que haber salido por correo");
        var token = System.Text.RegularExpressions.Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        token.Success.Should().BeTrue("el correo de invitación trae token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, documentNumber = DocumentoDePrueba.Numero(correo), registration = new { password = clave } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aceptar la invitación de {correo}: «{await resp.Content.ReadAsStringAsync()}»");
        var acceso = (await LeerAsync(resp)).GetProperty("accessToken").GetString();
        acceso.Should().NotBeNullOrWhiteSpace();
        return acceso!;
    }

    // ------------------------------------------------------ activación de prueba --

    /// <summary>Lo que deja <see cref="ActivarCarteraAsync"/>: la fecha de activación, la activación y la carga (vacía) activada.</summary>
    public sealed record CarteraActivadaE2E(DateOnly ActivationDate, Guid ActivationPublicId, Guid MigrationLoadPublicId, Guid SessionItemPublicId);

    /// <summary>
    /// Opciones de <see cref="ActivarCarteraAsync"/>; por defecto registra los dobles <c>TST</c> y <c>TES</c> y fija
    /// <c>Contabilidad.FaltaDeRegla = Advertir</c> (<see cref="AdvertirSinReglaContableAsync"/>).
    /// </summary>
    /// <remarks>Feature 015, T0299: <c>RegistrarDobles</c> pasó a <c>RegistrarRecaudoYTesoreria</c>: activa Recaudo y Tesorería reales, no los dobles.</remarks>
    public sealed record OpcionesDeActivacion(bool RegistrarRecaudoYTesoreria = true, string Motivo = "Activación de Cartera para las e2e", bool AdvertirSinRegla = true);

    private static readonly Dictionary<(CentralIdentityApiFixture, Guid), Task<CarteraActivadaE2E>> Activadas = [];

    /// <summary>
    /// Activa Cartera en una cooperativa aislada <b>sin cartera que migrar</b> (feature 013, T0235, C107), por las rutas reales: el único
    /// camino con que las e2e de las fases 4 a 12 obtienen una cooperativa activada, sin <c>Skip</c>. Registra por
    /// su activación a Recaudo y Tesorería reales (<see cref="RecaudoYTesoreriaRealesAsync"/>; hasta la feature 015 eran los dobles <c>TST</c> y
    /// <c>TES</c>); inicia la contabilidad con el ejercicio de hoy
    /// si la cooperativa no la tenía (como <c>CooperativaAisladaAsync(…, primerEjercicioContable)</c>); crea la carga con corte = hoy − 1, la
    /// aplica vacía, levanta el acta de activación y activa con fecha = hoy por <c>POST /api/lending/activation</c>.
    ///
    /// <para>
    /// <b>El acta se levanta por HTTP</b> desde US17 (T0311; <see cref="ActaDeActivacionAsync"/>): órgano, sesión, asistencia, punto
    /// <c>Decision</c> de sujeto <c>ActivationDifferences</c> sobre la carga, votos y cierre por las rutas de órganos y sesiones; hasta la fase 3
    /// se sembraba con el <c>ApplicationDbContext</c>. «Hoy» es el del reloj de la suite (<c>IDateTimeService</c> del host). La segunda llamada
    /// con la misma cooperativa devuelve la misma activación.
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

        if (opciones.RegistrarRecaudoYTesoreria) await RecaudoYTesoreriaRealesAsync(fx, http, coop, corte);
        if (opciones.AdvertirSinRegla) await AdvertirSinReglaContableAsync(http, admin, corte);
        await RequisitosDeActivacionAsync(fx, coop, corte);

        var alta =await ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/migration/loads", new { cutoffDate = corte, notes = "Sin cartera que migrar (e2e)" });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, $"crear la carga: «{await alta.Content.ReadAsStringAsync()}»");
        var carga = (await LeerAsync(alta)).GetProperty("publicId").GetGuid();

        var aplicar = await ConClaveAsync(http, admin, HttpMethod.Post, $"/api/lending/migration/loads/{carga}/apply", new { reason = "Carga vacía (e2e)" });
        aplicar.IsSuccessStatusCode.Should().BeTrue($"aplicar la carga: «{await aplicar.Content.ReadAsStringAsync()}»");

        var punto = await ActaDeActivacionAsync(http, coop, carga, hoy);

        var activar = await ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/activation", new
        {
            migrationLoadPublicId = carga, activationDate = hoy, sessionItemPublicId = punto, acknowledgeWarnings = true, reason = opciones.Motivo,
        });
        activar.StatusCode.Should().Be(HttpStatusCode.Created, $"activar Cartera: «{await activar.Content.ReadAsStringAsync()}»");
        var activacion = await LeerAsync(activar);
        return new CarteraActivadaE2E(DateOnly.Parse(activacion.GetProperty("activationDate").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            activacion.GetProperty("activationPublicId").GetGuid(), carga, punto);
    }

    /// <summary>
    /// Feature 015, T0299 (FR-224): Recaudo y Tesorería <b>reales</b> activados y registrados por su propia activación, vigentes desde <paramref name="desde"/>
    /// (corte de su puesta en marcha = la víspera), en lugar de los dobles <c>TST</c> y <c>TES</c> que se registraban a mano. Inicia la contabilidad si hace falta
    /// (la activación de Cartera la exige de todos modos). Idempotente: un módulo activo no se vuelve a activar.
    /// </summary>
    public static Task RecaudoYTesoreriaRealesAsync(CentralIdentityApiFixture fx, HttpClient http, CooperativaAislada coop, DateOnly desde) =>
        Receipts.RecaudoYTesoreriaE2E.RegistrarYActivarAsync(http, coop, fx, corte: desde.AddDays(-1), iniciarContabilidad: true);

    /// <summary>
    /// Decisión del dueño del 2026-10-18: los requisitos de la activación se exigen en todos los ambientes (hasta esa fecha, fuera de producción el
    /// Anexo 1, la usura de las líneas activas, los roles de Cartera, las oficinas vinculadas y la regla del recaudo por nómina quedaban
    /// <c>Waived</c>). Deja a la cooperativa cumpliéndolos como una real, por las rutas: una pasada de la réplica de valores nacionales
    /// (<see cref="NationalValues.ValoresNacionalesE2E.ReplicarAsync"/>: trae el Anexo 1 que siembra la central del host; no registra valores en la
    /// central, porque hay escenarios que registran su propia usura —la usura de las líneas la registra quien las crea, con
    /// <see cref="LineasE2E.PrepararAsync"/>—), los roles de las trece plantillas (<c>POST /api/lending/activation/suggested-roles</c>, idempotente)
    /// y, si ya hay líneas activas, la regla <c>RecaudoPorAplicar</c> de la clase <c>Payroll</c> en la matriz de Cartera, vigente desde el primer
    /// día del mes de <paramref name="desde"/> (sin líneas no se exige; así una cooperativa que se activa antes de crear líneas no estrena una regla).
    /// Las oficinas de una carga las vincula quien la arma (<see cref="MigracionE2E.OficinaAsync"/>). Idempotente. No Recaudo ni Tesorería:
    /// <see cref="RecaudoYTesoreriaRealesAsync"/>.
    /// </summary>
    public static async Task RequisitosDeActivacionAsync(CentralIdentityApiFixture fx, CooperativaAislada coop, DateOnly desde)
    {
        await NationalValues.ValoresNacionalesE2E.ReplicarAsync(fx, coop.TenantPublicId);
        using var http = fx.CreateClient();
        var admin = coop.TokenAdmin;
        var roles = await ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/activation/suggested-roles", new { });
        roles.IsSuccessStatusCode.Should().BeTrue($"crear los roles de Cartera: «{await roles.Content.ReadAsStringAsync()}»");

        var hayLineasActivas = (await GetAsync(http, admin, "/api/lending/credit-lines")).EnumerateArray()
            .Any(l => l.GetProperty("status").GetInt32() == (int)CreditLineStatus.Active);
        if (!hayLineasActivas) return;
        const string Reglas = "/api/accounting/lending/rules";
        if ((await GetAsync(http, admin, $"{Reglas}?role=RecaudoPorAplicar&collectorClass=Payroll&pageSize=1")).GetProperty("totalCount").GetInt32() > 0) return;
        const string Cuenta = "24101098";
        await Accounting.ContabilidadE2E.CrearCuentaAsync(http, admin, Cuenta, "Recaudo por nómina para Cartera (e2e)", "241010", ["CNT", "CAR"]);
        var regla = await ConClaveAsync(http, admin, HttpMethod.Post, Reglas, new
        {
            operation = "*", role = "RecaudoPorAplicar", dimensions = new { collectorClass = "Payroll" },
            accountPublicId = await Accounting.ContabilidadE2E.CuentaAsync(http, admin, Cuenta), validFrom = new DateOnly(desde.Year, desde.Month, 1),
            notes = "Requisito de la activación (e2e)", reason = "La activación exige la regla del recaudo por nómina en todos los ambientes",
        });
        regla.StatusCode.Should().Be(HttpStatusCode.Created, $"la regla del recaudo por nómina: «{await regla.Content.ReadAsStringAsync()}»");
    }

    /// <summary>
    /// <c>Contabilidad.FaltaDeRegla = Advertir</c> desde <paramref name="desde"/>. El defecto es <c>Bloquear</c> (decisión del dueño del 2026-10-14) y
    /// estas cooperativas inician la contabilidad sin cargar la matriz de Cartera: sin esto, todo desembolso respondería
    /// <c>Lending.Prevalidation.NotPostable</c>. Las pruebas que recorren la matriz fijan <c>Bloquear</c> con su propia vigencia
    /// (<c>ContabilizacionDeCarteraPorMensajesTests</c>). Si ya hay una vigencia desde esa fecha, no hace nada.
    /// </summary>
    public static async Task AdvertirSinReglaContableAsync(HttpClient http, string admin, DateOnly desde)
    {
        var resp = await ConClaveAsync(http, admin, HttpMethod.Post, "/api/lending/parameters/Contabilidad.FaltaDeRegla/versions", new
        {
            scopeKind = 0, value = "Advertir", validFrom = desde, reason = "Las e2e operan sin la matriz contable de Cartera completa",
        });
        if (resp.IsSuccessStatusCode) return;
        (await CodigoDeErrorAsync(resp)).Should().Contain("Overlap", $"fijar Advertir: «{await resp.Content.ReadAsStringAsync()}»");
    }

    /// <summary>
    /// El acta de activación por las rutas de US17 (T0311, C61, C107): la persona del administrador (la secretaría que registra los votos
    /// y cierra, que debe tenerla por la segregación), tres miembros del maestro, el órgano con sus reglas desde hoy —el consejo, o un órgano
    /// «Otro» si la cooperativa ya tiene un consejo activo—, la sesión de hoy con los tres presentes, el punto <c>Decision</c> de sujeto
    /// <c>ActivationDifferences</c> sobre la carga, tres votos a favor registrados por la secretaría y el cierre con la huella que se revisó.
    /// Devuelve el <c>PublicId</c> del punto, que <c>ActivateLendingCommand</c> valida con <c>PuntoDeActaCerrado</c>.
    /// </summary>
    public static async Task<Guid> ActaDeActivacionAsync(HttpClient http, CooperativaAislada coop, Guid cargaPublicId, DateOnly hoy)
    {
        var admin = coop.TokenAdmin;
        await PersonaDelAdministradorAsync(http, coop);

        var miembros = new List<Guid>();
        for (var i = 1; i <= 3; i++) miembros.Add(await PersonaAsync(http, admin, $"Consejero{i}", "Activacion"));

        var organos = (await GetAsync(http, admin, "/api/lending/governing-bodies")).EnumerateArray().ToList();
        var hayConsejo = organos.Any(o => o.GetProperty("kind").GetInt32() == (int)GoverningBodyKind.BoardOfDirectors && o.GetProperty("isActive").GetBoolean());
        var codigo = $"ACT{cargaPublicId.ToString("N")[..6].ToUpperInvariant()}";
        var organo = await ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/governing-bodies", new
        {
            code = codigo, name = $"Órgano de activación {codigo} (e2e)", kind = hayConsejo ? "Other" : "BoardOfDirectors", decidesCredit = true,
            minutesPrefix = "ACT-",
            rules = new
            {
                quorum = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
                ordinaryMajority = new { numerator = 1, denominator = 2, comparison = "MoreThan" },
                relatedPartyFraction = hayConsejo ? null : new { numerator = 4, denominator = 5, comparison = "AtLeast" },
                validFrom = hoy, reason = "Reglas del órgano de activación (e2e)", source = "Estatutos de la cooperativa de prueba",
            },
        });
        var organoId = organo.GetProperty("publicId").GetGuid();

        var miembrosDelOrgano = new List<Guid>();
        foreach (var persona in miembros)
        {
            var conMiembro = await ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/governing-bodies/{organoId}/members", new
            {
                personPublicId = persona, position = "Member", votes = true, validFrom = hoy, appointmentNotes = "Nombramiento de prueba (e2e)",
            });
            miembrosDelOrgano.Add(MiembroDe(conMiembro));
        }

        var sesion = (await ExitoAsync(http, admin, HttpMethod.Post, "/api/lending/sessions", new
        {
            governingBodyPublicId = organoId, kind = "Extraordinary", mode = "InPerson", heldOn = hoy, notes = "Acta de activación (e2e)",
        })).GetProperty("publicId").GetGuid();
        await ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/attendance", new
        {
            attendees = miembrosDelOrgano.Select(m => new { memberPublicId = m, present = true }).ToList(),
        });
        var punto = (await ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/items", new
        {
            subject = LendingDecisionSubjects.ActivationDifferences, subjectPublicId = cargaPublicId, kind = "Decision",
        })).GetProperty("itemPublicId").GetGuid();
        foreach (var persona in miembros)
            await ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/items/{punto}/votes", new
            {
                personPublicId = persona, vote = "For", method = "RecordedBySecretary",
            });

        var huella = (await GetAsync(http, admin, $"/api/lending/sessions/{sesion}")).GetProperty("currentContentSha256").GetString();
        var cerrada = await ExitoAsync(http, admin, HttpMethod.Post, $"/api/lending/sessions/{sesion}/close", new { expectedContentSha256 = huella });
        cerrada.GetProperty("status").GetInt32().Should().Be((int)SessionStatus.Closed, $"el acta de activación se cierra: {cerrada}");
        return punto;
    }

    /// <summary>El <c>memberPublicId</c> del <c>GoverningBodyMemberDto</c> que devuelve el nombramiento (<c>POST /{id}/members</c>).</summary>
    public static Guid MiembroDe(JsonElement nombramiento) => nombramiento.GetProperty("memberPublicId").GetGuid();

    // ------------------------------------------------------------------ personas y usuarios --

    private static int _siguienteDocumento;

    /// <summary>
    /// Una persona del maestro por <c>POST /api/core/people</c>. El documento termina en 0 a propósito: el canal <c>SIMULADO</c> responde por
    /// el último dígito del documento (1 dato inválido, 5 canal no disponible) y aquí se quiere el envío normal.
    /// </summary>
    public static async Task<Guid> PersonaAsync(HttpClient http, string token, string nombre, string apellido = "Cartera", string? movil = null)
    {
        var documento = (8_800_000_000L + Interlocked.Increment(ref _siguienteDocumento) * 10L).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var resp = await EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = apellido, mobile = movil,
            email = $"{nombre.ToLowerInvariant()}.{documento}@persona.cartera.test",
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"persona {nombre}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetGuid();
    }

    /// <summary>El <c>PublicId</c> del usuario con ese correo en la cooperativa.</summary>
    public static async Task<Guid> UsuarioPorCorreoAsync(HttpClient http, string tokenAdmin, string correo)
    {
        var usuarios = await GetAsync(http, tokenAdmin, $"/api/admin/users?search={Uri.EscapeDataString(correo)}&pageSize=50");
        return usuarios.GetProperty("items").EnumerateArray()
            .Single(u => string.Equals(u.GetProperty("email").GetString(), correo, StringComparison.OrdinalIgnoreCase)).GetProperty("publicId").GetGuid();
    }

    /// <summary>Asocia el usuario a su persona del maestro (<c>SEC_Users.PersonId</c>) por <c>PUT /api/admin/users/{id}</c> con <c>personPublicId</c>.</summary>
    public static async Task AsociarPersonaAsync(HttpClient http, string tokenAdmin, Guid usuario, string correo, Guid persona)
    {
        var resp = await EnviarAsync(http, tokenAdmin, HttpMethod.Put, $"/api/admin/users/{usuario}", new { email = correo, personPublicId = persona });
        resp.IsSuccessStatusCode.Should().BeTrue($"asociar la persona de {correo}: «{await resp.Content.ReadAsStringAsync()}»");
    }

    private static readonly Dictionary<Guid, Task<Guid>> PersonasDelAdministrador = [];

    /// <summary>La persona del administrador de la cooperativa (una por cooperativa): sin ella no puede cerrar un acta (segregación por persona).</summary>
    public static Task<Guid> PersonaDelAdministradorAsync(HttpClient http, CooperativaAislada coop)
    {
        lock (Cerrojo)
        {
            if (!PersonasDelAdministrador.TryGetValue(coop.TenantPublicId, out var tarea))
            {
                tarea = AsociarAlAdministradorAsync(http, coop);
                PersonasDelAdministrador[coop.TenantPublicId] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Guid> AsociarAlAdministradorAsync(HttpClient http, CooperativaAislada coop)
    {
        var correo = coop.CorreoAdmin ?? throw new InvalidOperationException("La cooperativa aislada no trae el correo de su administrador.");
        var persona = await PersonaAsync(http, coop.TokenAdmin, "Administrador", "Cooperativa");
        await AsociarPersonaAsync(http, coop.TokenAdmin, await UsuarioPorCorreoAsync(http, coop.TokenAdmin, correo), correo, persona);
        return persona;
    }

    /// <summary>Un <c>GET</c> con el token, sin juzgar la respuesta (el PDF, los 200 que no son JSON).</summary>
    public static Task<HttpResponseMessage> MandarGetAsync(HttpClient http, string token, string url) =>
        InventarioE2E.MandarAsync(http, token, HttpMethod.Get, url);

    /// <summary>Manda y exige un éxito; devuelve el cuerpo.</summary>
    public static Task<JsonElement> ExitoAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo = null) =>
        InventarioE2E.ExitoAsync(http, token, metodo, url, cuerpo);

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
