using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Las cuatro pruebas e2e de nómina comparten UN host y UNA cooperativa: levantar tres
/// contenedores y migrar dos bases por clase costaría minutos por archivo sin probar nada
/// distinto. La colección hace que xUnit las corra en serie sobre la misma fixture.
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class NominaCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Nomina e2e";
}

/// <summary>
/// Bootstrap de la cooperativa de pruebas de nómina y ayudantes HTTP. Todo pasa por la
/// API con el token de un administrador de cooperativa real (invitación aceptada), como lo
/// haría una persona; nada se escribe a la base por debajo.
/// </summary>
public static class NominaE2E
{
    public const string CorreoAdmin = "nomina.admin@coop.nomina.test";
    public const string ClaveAdmin = "Nomina-Admin-2026!";
    public const string CorreoSoloLectura = "nomina.lectura@coop.nomina.test";
    public const string ClaveSoloLectura = "Nomina-Lectura-2026!";
    public const string CuentaDebito = "51050501";
    public const string CuentaCredito = "25050501";

    public sealed class Contexto
    {
        public required string TokenAdmin { get; init; }
        public required string TokenSoloLectura { get; init; }
        public required Guid TenantPublicId { get; init; }
    }

    private static readonly Dictionary<CentralIdentityApiFixture, Task<Contexto>> Contextos = [];
    private static readonly object Cerrojo = new();
    private static int _documento = 700_000_000;

    /// <summary>Una sola vez por fixture: cooperativa, administrador, usuario de sólo lectura, contabilidad mínima y cuentas por concepto.</summary>
    public static Task<Contexto> PrepararAsync(CentralIdentityApiFixture fx)
    {
        lock (Cerrojo)
        {
            if (!Contextos.TryGetValue(fx, out var tarea))
            {
                tarea = PrepararDeVerdadAsync(fx);
                Contextos[fx] = tarea;
            }
            return tarea;
        }
    }

    private static async Task<Contexto> PrepararDeVerdadAsync(CentralIdentityApiFixture fx)
    {
        using var http = fx.CreateClient();
        var tokenMaestro = await fx.IniciarSesionMaestroAsync(http);

        // --- cooperativa + administrador ---
        var alta = await EnviarAsync(http, tokenMaestro, HttpMethod.Post, "/api/saas/tenants/with-admin", new
        {
            name = "Coop. Nomina E2E",
            schemaName = "tenant_nomina_e2e",
            subdomain = "tenant_nomina_e2e",
            nit = "900777333",
            legalName = "Cooperativa Nomina E2E",
            contactEmail = "contacto@coop.nomina.test",
            planType = "Basic",
            maxUsers = 50,
            storageLimitMb = 5120,
            firstAdminEmail = CorreoAdmin,
        });
        alta.StatusCode.Should().Be(HttpStatusCode.OK, $"alta de la cooperativa: «{await alta.Content.ReadAsStringAsync()}»");
        var tenantPublicId = (await LeerAsync(alta)).GetProperty("tenantPublicId").GetGuid();
        var tokenAdmin = await AceptarInvitacionAsync(fx, http, CorreoAdmin, ClaveAdmin);

        // --- usuario de sólo lectura (rol por defecto de una invitación de la cooperativa) ---
        var invitacion = await EnviarAsync(http, tokenAdmin, HttpMethod.Post, $"/api/tenants/{tenantPublicId}/invitations", new { email = CorreoSoloLectura });
        invitacion.StatusCode.Should().Be(HttpStatusCode.OK, $"invitación de sólo lectura: «{await invitacion.Content.ReadAsStringAsync()}»");
        var tokenSoloLectura = await AceptarInvitacionAsync(fx, http, CorreoSoloLectura, ClaveSoloLectura);

        // --- lo que la semilla no deja: sucursal, centro de costo, contabilidad iniciada y dos auxiliares ---
        // Feature 009: la contabilidad se inicia una vez por cooperativa (catálogo PUC solidario, movimiento en el
        // nivel 5 con ocho dígitos, ejercicio 2026 con sus doce períodos abiertos) y las auxiliares cuelgan de la
        // subcuenta del catálogo con sus reglas; ambas habilitadas para Nómina.
        await CrearAsync(http, tokenAdmin, "/api/core/branches", new { name = "Principal", shortName = "PPAL" });
        await CrearAsync(http, tokenAdmin, "/api/core/cost-centers", new { name = "Administración", payrollType = 1, period = 1, payrollPeriodicity = 30 });
        var sucursales = await LeerAsync(await EnviarAsync(http, tokenAdmin, HttpMethod.Get, "/api/core/branches?PageNumber=1&PageSize=10", null));
        var sucursalPublicId = sucursales.GetProperty("items").EnumerateArray().First().GetProperty("publicId").GetGuid();
        await CrearAsync(http, tokenAdmin, "/api/accounting/setup/initialize", new
        {
            catalogCode = "PUC-SOLIDARIO", movementLevel = 5, level5Length = 8, level6Length = 0, niifGroup = 2, firstFiscalYear = 2026,
            mainBranchPublicId = sucursalPublicId, fourEyes = false,
        });
        await CrearAuxiliarAsync(http, tokenAdmin, CuentaDebito, "Sueldos y salarios", "510505");
        await CrearAuxiliarAsync(http, tokenAdmin, CuentaCredito, "Salarios por pagar", "250505");

        // --- cuentas contables para todos los conceptos de la semilla ---
        var conceptos = await LeerAsync(await EnviarAsync(http, tokenAdmin, HttpMethod.Get, "/api/payroll/concept-definitions", null));
        conceptos.GetArrayLength().Should().BeGreaterThan(30, "la semilla deja los conceptos estándar en la cooperativa nueva");
        foreach (var c in conceptos.EnumerateArray())
        {
            var code = c.GetProperty("code").GetString()!;
            var resp = await EnviarAsync(http, tokenAdmin, HttpMethod.Put, $"/api/payroll/concept-definitions/{code}/accounts",
                new { rows = new[] { new { costCenterPublicId = (Guid?)null, debitAccountCode = CuentaDebito, creditAccountCode = CuentaCredito } } });
            resp.IsSuccessStatusCode.Should().BeTrue($"cuentas de {code}: «{await resp.Content.ReadAsStringAsync()}»");
        }

        // --- la cooperativa permite que quien calcula apruebe, con segunda confirmación ---
        var parametros = await LeerAsync(await EnviarAsync(http, tokenAdmin, HttpMethod.Get, "/api/admin/parametros?modulo=PAY", null));
        var politica = parametros.EnumerateArray().FirstOrDefault(p => p.GetProperty("clave").GetString() == "Payroll.AllowSameUserApproval");
        politica.ValueKind.Should().Be(JsonValueKind.Object, "la semilla deja la política Payroll.AllowSameUserApproval");
        var cambio = await EnviarAsync(http, tokenAdmin, HttpMethod.Put, $"/api/admin/parametros/{politica.GetProperty("publicId").GetGuid()}", new { valor = "true" });
        cambio.IsSuccessStatusCode.Should().BeTrue($"política de aprobación: «{await cambio.Content.ReadAsStringAsync()}»");

        return new Contexto { TokenAdmin = tokenAdmin, TokenSoloLectura = tokenSoloLectura, TenantPublicId = tenantPublicId };
    }

    private static async Task<string> AceptarInvitacionAsync(CentralIdentityApiFixture fx, HttpClient http, string correo, string clave)
    {
        var mensaje = fx.Emails.Sent.LastOrDefault(m => m.To == correo);
        mensaje.Should().NotBeNull($"la invitación de {correo} tiene que haber salido por correo");
        var token = Regex.Match(mensaje!.BodyHtml, @"token=([A-Za-z0-9_\-]+)");
        token.Success.Should().BeTrue("el correo de invitación trae token=");

        var resp = await http.PostAsJsonAsync("/api/invitations/accept", new { token = token.Groups[1].Value, registration = new { password = clave } });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aceptar la invitación de {correo}: «{await resp.Content.ReadAsStringAsync()}»");
        var acceso = (await LeerAsync(resp)).GetProperty("accessToken").GetString();
        acceso.Should().NotBeNullOrWhiteSpace();
        return acceso!;
    }

    /// <summary>Una auxiliar de movimiento bajo la subcuenta del catálogo, habilitada para Contabilidad y Nómina (feature 009).</summary>
    private static async Task CrearAuxiliarAsync(HttpClient http, string token, string codigo, string nombre, string codigoPadre)
    {
        var busqueda = await LeerAsync(await EnviarAsync(http, token, HttpMethod.Get, $"/api/accounting/accounts/search?q={codigoPadre}&onlyMovement=false", null));
        var padre = busqueda.EnumerateArray().FirstOrDefault(c => c.GetProperty("code").GetString() == codigoPadre);
        padre.ValueKind.Should().Be(JsonValueKind.Object, $"la subcuenta {codigoPadre} viene del catálogo PUC solidario");
        await CrearAsync(http, token, "/api/accounting/accounts", new
        {
            code = codigo, name = nombre, parentPublicId = padre.GetProperty("publicId").GetGuid(),
            enabledModules = new[] { "CNT", "NOM" }, requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false,
        });
    }

    private static async Task CrearAsync(HttpClient http, string token, string url, object cuerpo)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, url, cuerpo);
        // Idempotente entre corridas sobre la misma base: lo que ya existe no es un fallo.
        if (resp.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity) return;
        resp.IsSuccessStatusCode.Should().BeTrue($"POST {url}: {(int)resp.StatusCode} «{await resp.Content.ReadAsStringAsync()}»");
    }

    // ------------------------------------------------------------------ datos --

    public static async Task<(Guid EmpleadoId, string Documento)> CrearEmpleadoAsync(HttpClient http, string token, string nombre, decimal salario, DateTime ingreso, bool conCorreo = true)
    {
        var documento = Interlocked.Increment(ref _documento).ToString();
        var persona = await EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Prueba",
            email = conCorreo ? $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test" : null, isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await LeerAsync(persona)).GetGuid();

        var empleado = await EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = salario, contractType = 1, hireDate = ingreso,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await empleado.Content.ReadAsStringAsync()}»");
        return ((await LeerAsync(empleado)).GetGuid(), documento);
    }

    public static async Task<Guid> CrearPeriodoAsync(HttpClient http, string token, DateTime desde, DateTime hasta)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/pay-periods", new
        {
            startDate = desde, endDate = hasta, description = $"Nómina {desde:MMMM yyyy}", statusMessage = string.Empty,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"período: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetGuid();
    }

    public static async Task<Guid> RegistrarNovedadAsync(HttpClient http, string token, Guid periodoId, object novedad)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/novelties", novedad);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"novedad: «{await resp.Content.ReadAsStringAsync()}»");
        return (await LeerAsync(resp)).GetProperty("publicId").GetGuid();
    }

    public static async Task<JsonElement> CalcularAsync(HttpClient http, string token, Guid periodoId)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/runs", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"calcular: «{await resp.Content.ReadAsStringAsync()}»");
        return await LeerAsync(resp);
    }

    /// <summary>
    /// Las excepciones que la aprobación necesita: una por cada bloqueo de cada empleado de la
    /// corrida. Los empleados de prueba no tienen EPS ni fondo, y cada período del plan incluye a
    /// todos los vigentes, no sólo al que creó la prueba.
    /// </summary>
    public static async Task<object[]> ExcepcionesParaAsync(HttpClient http, string token, Guid runId)
    {
        var empleados = await GetAsync(http, token, $"/api/payroll/runs/{runId}/employees");
        return empleados.EnumerateArray()
            .SelectMany(e => e.GetProperty("flags").EnumerateArray().Select(f => new
            {
                employeePublicId = e.GetProperty("employeePublicId").GetGuid(),
                flag = f.GetString()!,
                reason = "Empleado de prueba sin afiliaciones",
            }))
            .Cast<object>()
            .ToArray();
    }

    /// <summary>Aprueba autorizando cada bloqueo con motivo y con la segunda confirmación de segregación.</summary>
    public static async Task<JsonElement> AprobarAsync(HttpClient http, string token, Guid runId)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Post, $"/api/payroll/runs/{runId}/approve", new
        {
            confirm = true,
            exceptions = await ExcepcionesParaAsync(http, token, runId),
            confirmEmpty = false,
            confirmWithoutSegregation = true,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"aprobar: «{await resp.Content.ReadAsStringAsync()}»");
        return await LeerAsync(resp);
    }

    /// <summary>Un período del mes indicado con un empleado y una novedad, calculado y aprobado. Lo usan pagos, comprobantes y reversión.</summary>
    public static async Task<(Guid PeriodoId, Guid EmpleadoId, Guid RunId, string Documento)> CicloAprobadoAsync(HttpClient http, string token, int mes, string nombre)
    {
        var (empleadoId, documento) = await CrearEmpleadoAsync(http, token, nombre, 2_500_000m, new DateTime(2025, 1, 15));
        var desde = new DateTime(2026, mes, 1);
        var periodoId = await CrearPeriodoAsync(http, token, desde, desde.AddMonths(1).AddDays(-1));
        await RegistrarNovedadAsync(http, token, periodoId, new { employeePublicId = empleadoId, conceptCode = "HEX_NOCTURNA", quantity = 4 });
        var calculo = await CalcularAsync(http, token, periodoId);
        var runId = calculo.GetProperty("runPublicId").GetGuid();
        await AprobarAsync(http, token, runId);
        return (periodoId, empleadoId, runId, documento);
    }

    // ------------------------------------------------------------------- http --

    public static async Task<HttpResponseMessage> EnviarAsync(HttpClient http, string token, HttpMethod metodo, string url, object? cuerpo)
    {
        using var req = new HttpRequestMessage(metodo, url);
        if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await http.SendAsync(req);
    }

    public static async Task<JsonElement> LeerAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(raw).RootElement.Clone();
    }

    public static async Task<JsonElement> GetAsync(HttpClient http, string token, string url)
    {
        var resp = await EnviarAsync(http, token, HttpMethod.Get, url, null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"GET {url}: «{await resp.Content.ReadAsStringAsync()}»");
        return await LeerAsync(resp);
    }

    /// <summary>El código del envelope de error (<c>{code, message, traceId}</c>), o el cuerpo entero si no lo trae.</summary>
    public static async Task<string> CodigoDeErrorAsync(HttpResponseMessage resp)
    {
        var raw = await resp.Content.ReadAsStringAsync();
        try
        {
            var json = JsonDocument.Parse(raw).RootElement;
            return json.TryGetProperty("code", out var code) ? code.GetString() ?? raw : raw;
        }
        catch (JsonException) { return raw; }
    }
}
