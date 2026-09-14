using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using static IngenIA365ERP.API.IntegrationTests.Payroll.NominaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Core;

/// <summary>
/// Feature 008 — alta de persona en un paso desde los módulos, recorrida por HTTP sobre la
/// cooperativa compartida de nómina: el compuesto es atómico, las banderas derivadas no se pisan,
/// el reingreso es una ficha nueva, una eliminada se restaura y no se duplica, y sin permiso la
/// API responde igual que a una ruta inexistente.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class AltaDePersonaEnUnPasoTests(CentralIdentityApiFixture fx)
{
    private static int _documento = 800_000_000;
    private static string Documento() => Interlocked.Increment(ref _documento).ToString();

    private static object Persona(string documento, string nombre = "Ana") => new
    {
        idType = "C", taxId = documento, firstName = nombre, lastName = "UnPaso", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test",
    };

    private static object Laboral() => new
    {
        baseSalary = 2_600_000m, contractType = 1, hireDate = new DateTime(2026, 9, 15), payrollBankAccountType = 1,
    };

    private static object Afiliacion() => new { joinDate = new DateOnly(2026, 9, 15), contributionRate = 5m };

    // ------------------------------------------------------------------------- US1 --

    [Fact]
    public async Task Empleado_con_persona_nueva_en_un_solo_paso()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var documento = Documento();
        var alta = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/payroll/employees/with-person",
            new { person = Persona(documento), employee = Laboral() });

        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var resultado = await LeerAsync(alta);
        var personaId = resultado.GetProperty("personPublicId").GetGuid();
        var empleadoId = resultado.GetProperty("employeePublicId").GetGuid();

        var persona = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{personaId}");
        persona.GetProperty("isEmployee").GetBoolean().Should().BeTrue("la bandera la encendió el registro de la ficha");
        persona.GetProperty("isAssociate").GetBoolean().Should().BeFalse();
        persona.GetProperty("taxId").GetString().Should().Be(documento);

        var porPersona = await GetAsync(http, ctx.TokenAdmin, $"/api/payroll/employees/by-person/{personaId}");
        porPersona.GetProperty("publicId").GetGuid().Should().Be(empleadoId);
        porPersona.GetProperty("personPublicId").GetGuid().Should().Be(personaId);

        // FR-014: UNA operación auditada, la del compuesto, con el request completo; ningún
        // CreatePersonCommand aparte para ese documento. Hay dos rastros: el de la operación
        // (AuditBehavior: el request entero, sin EntityId) y el de cada entidad escrita
        // (Principio VII: Create/Person, Create/Employee…, con el PublicId como EntityId); aquí se
        // mira el primero. La auditoría se escribe en lotes (FlushIntervalSeconds = 5): se espera a que caiga.
        var compuesto = await EsperarEventoAsync(http, ctx.TokenAdmin, "RegisterEmployeeWithPerson", documento);
        compuesto.Should().BeTrue("el alta compuesta es UNA operación de auditoría con el documento en el request");
        var aparte = await EventoAsync(http, ctx.TokenAdmin, "Create", "Person", documento);
        aparte.Should().BeNull($"la persona no se audita como operación aparte: va dentro del compuesto; se encontró: {aparte}");
    }

    [Fact]
    public async Task Documento_duplicado_no_registra_nada()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var documento = Documento();
        var existente = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/people", Persona(documento, "Carlos"));
        existente.StatusCode.Should().Be(HttpStatusCode.Created, await existente.Content.ReadAsStringAsync());
        var empleadosAntes = await TotalAsync(http, ctx.TokenAdmin, "/api/payroll/employees?PageNumber=1&PageSize=1");

        var alta = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/payroll/employees/with-person",
            new { person = Persona(documento), employee = Laboral() });

        alta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var error = await LeerAsync(alta);
        error.GetProperty("code").GetString().Should().Be("Person.TaxIdDuplicate");
        error.GetProperty("message").GetString().Should().Contain("Carlos UnPaso");
        (await TotalAsync(http, ctx.TokenAdmin, "/api/payroll/employees?PageNumber=1&PageSize=1")).Should().Be(empleadosAntes);
        var buscada = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/search?q={documento}");
        buscada.GetArrayLength().Should().Be(1, "sigue habiendo una sola persona con ese documento");
    }

    [Fact]
    public async Task Documento_de_una_eliminada_no_crea_segunda_fila_y_el_admin_la_restaura()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var documento = Documento();
        var creada = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/people", Persona(documento, "Diana"));
        var personaId = (await LeerAsync(creada)).GetGuid();
        var borrada = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Delete, $"/api/core/people/{personaId}", null);
        borrada.StatusCode.Should().Be(HttpStatusCode.NoContent, await borrada.Content.ReadAsStringAsync());

        // Crear otra con el mismo documento: 422 con código propio, no un 500 del índice único.
        var alta = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/payroll/employees/with-person",
            new { person = Persona(documento), employee = Laboral() });
        alta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await alta.Content.ReadAsStringAsync());
        (await LeerAsync(alta)).GetProperty("code").GetString().Should().Be("Person.TaxIdDeleted");

        // La búsqueda normal no la ve; la búsqueda por documento sí, y dice que está eliminada.
        (await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/search?q={documento}")).GetArrayLength().Should().Be(0);
        var dueno = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/by-document?taxId={documento}");
        dueno.GetProperty("publicId").GetGuid().Should().Be(personaId);
        dueno.GetProperty("isDeleted").GetBoolean().Should().BeTrue();

        // Sólo lectura no puede restaurar: 404 indistinguible. El administrador sí: misma fila.
        var sinPermiso = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"/api/core/people/{personaId}/restore", null);
        sinPermiso.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LeerAsync(sinPermiso)).GetProperty("code").GetString().Should().Be("Generic.NotFound");

        var restaurada = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, $"/api/core/people/{personaId}/restore", null);
        restaurada.StatusCode.Should().Be(HttpStatusCode.NoContent, await restaurada.Content.ReadAsStringAsync());
        var persona = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{personaId}");
        persona.GetProperty("publicId").GetGuid().Should().Be(personaId);
        persona.GetProperty("isEmployee").GetBoolean().Should().BeFalse();

        var otraVez = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, $"/api/core/people/{personaId}/restore", null);
        otraVez.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await LeerAsync(otraVez)).GetProperty("code").GetString().Should().Be("Person.NotDeleted");
    }

    // ------------------------------------------------------------------------- US3 --

    [Fact]
    public async Task Las_banderas_no_se_pisan_y_terminar_apaga_solo_empleado()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var documento = Documento();

        // Asociada primero, luego empleada, luego editada desde Personas.
        var asociado = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/associates/with-person",
            new { person = Persona(documento, "Elena"), associate = Afiliacion() });
        asociado.StatusCode.Should().Be(HttpStatusCode.Created, await asociado.Content.ReadAsStringAsync());
        var personaId = (await LeerAsync(asociado)).GetProperty("personPublicId").GetGuid();

        var empleado = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/payroll/employees",
            new { personPublicId = personaId, baseSalary = 2_600_000m, contractType = 1, hireDate = new DateTime(2026, 9, 15) });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, await empleado.Content.ReadAsStringAsync());
        var empleadoId = (await LeerAsync(empleado)).GetGuid();

        // El PUT manda banderas derivadas «viejas» (isEmployee=false): el servidor las ignora.
        var edicion = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Put, $"/api/core/people/{personaId}", new
        {
            idType = "C", taxId = documento, firstName = "Elena", lastName = "UnPaso", email = $"elena.nueva.{documento}@coop.nomina.test",
            isEmployee = false, isAssociate = false, isCustomer = true,
        });
        edicion.StatusCode.Should().Be(HttpStatusCode.NoContent, await edicion.Content.ReadAsStringAsync());

        var persona = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{personaId}");
        persona.GetProperty("isAssociate").GetBoolean().Should().BeTrue("editar la persona no toca las derivadas");
        persona.GetProperty("isEmployee").GetBoolean().Should().BeTrue();
        persona.GetProperty("isCustomer").GetBoolean().Should().BeTrue("las simples sí se editan");
        persona.GetProperty("email").GetString().Should().Contain("elena.nueva");

        // Terminar apaga sólo «Empleado». El motivo es texto libre (hasta 120; antes la columna
        // era el código de 4 caracteres de SOLIDO y «Renuncia» daba 500).
        var fin = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, $"/api/payroll/employees/{empleadoId}/terminate",
            new { terminationDate = new DateTime(2026, 10, 31), terminationCause = "Renuncia voluntaria" });
        fin.IsSuccessStatusCode.Should().BeTrue(await fin.Content.ReadAsStringAsync());
        persona = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{personaId}");
        persona.GetProperty("isEmployee").GetBoolean().Should().BeFalse();
        persona.GetProperty("isAssociate").GetBoolean().Should().BeTrue();

        // Sin ficha viva, by-person es 404 (la pantalla pasa a modo registro).
        var sinViva = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Get, $"/api/payroll/employees/by-person/{personaId}", null);
        sinViva.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Reingreso = ficha nueva; by-person devuelve la viva, no la retirada.
        var reingreso = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/payroll/employees",
            new { personPublicId = personaId, baseSalary = 2_900_000m, contractType = 1, hireDate = new DateTime(2027, 1, 15) });
        reingreso.StatusCode.Should().Be(HttpStatusCode.Created, await reingreso.Content.ReadAsStringAsync());
        var nuevaFicha = (await LeerAsync(reingreso)).GetGuid();
        nuevaFicha.Should().NotBe(empleadoId);
        var viva = await GetAsync(http, ctx.TokenAdmin, $"/api/payroll/employees/by-person/{personaId}");
        viva.GetProperty("publicId").GetGuid().Should().Be(nuevaFicha);
        (await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{personaId}")).GetProperty("isEmployee").GetBoolean().Should().BeTrue();
    }

    // ------------------------------------------------------------------------- US5 --

    [Fact]
    public async Task Sin_permiso_la_respuesta_es_la_de_una_ruta_inexistente()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();

        var registro = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, "/api/payroll/employees",
            new { personPublicId = Guid.NewGuid(), baseSalary = 1m, contractType = 1, hireDate = DateTime.Today });
        var inexistente = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, "/api/payroll/no-existe", new { });
        var compuesto = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, "/api/payroll/employees/with-person",
            new { person = Persona(Documento()), employee = Laboral() });

        registro.StatusCode.Should().Be(HttpStatusCode.NotFound);
        compuesto.StatusCode.Should().Be(HttpStatusCode.NotFound);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await LeerAsync(registro)).GetProperty("code").GetString().Should().Be("Generic.NotFound");
        (await LeerAsync(compuesto)).GetProperty("code").GetString().Should().Be("Generic.NotFound");

        // Pero la lectura la conserva (FR-010).
        var lectura = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, "/api/core/people?PageNumber=1&PageSize=5", null);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK, await lectura.Content.ReadAsStringAsync());
        var empleados = await EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, "/api/payroll/employees?PageNumber=1&PageSize=5", null);
        empleados.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Mis_permisos_dicen_lo_que_cada_rol_puede()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();

        var admin = await GetAsync(http, ctx.TokenAdmin, "/api/admin/permissions/mine");
        var lectura = await GetAsync(http, ctx.TokenSoloLectura, "/api/admin/permissions/mine");

        admin.GetProperty("isGlobalMasterAdmin").GetBoolean().Should().BeFalse();
        var deAdmin = admin.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        deAdmin.Should().Contain(["Core.People.Create", "Core.People.Delete", "Payroll.Employees.Terminate", "Core.Associates.Create"]);

        var deLectura = lectura.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        deLectura.Should().Contain(["Core.People.View", "Payroll.Employees.View", "Core.Associates.View"]);
        deLectura.Should().NotContain(["Core.People.Create", "Payroll.Employees.Create"]);
    }

    [Fact]
    public async Task La_busqueda_filtra_por_rol_y_trae_las_banderas()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var documento = Documento();
        var alta = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/associates/with-person",
            new { person = Persona(documento, "Fabio"), associate = Afiliacion() });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());

        var comoAsociado = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/search?q={documento}&rol=associate");
        var comoEmpleado = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/search?q={documento}&rol=employee");

        comoAsociado.GetArrayLength().Should().Be(1);
        var fila = comoAsociado[0];
        fila.GetProperty("isAssociate").GetBoolean().Should().BeTrue();
        fila.GetProperty("isEmployee").GetBoolean().Should().BeFalse();
        fila.TryGetProperty("isSalesperson", out _).Should().BeTrue("la fila trae las ocho banderas");
        comoEmpleado.GetArrayLength().Should().Be(0);
    }

    // ---------------------------------------------------------------------- ayudas --

    /// <summary>¿Hay un evento de auditoría de esa acción cuyo request contenga el texto (el documento)?</summary>
    private static async Task<bool> HayEventoAsync(HttpClient http, string token, string accion, string? entidad, string texto) =>
        await EventoAsync(http, token, accion, entidad, texto) is not null;

    private static async Task<string?> EventoAsync(HttpClient http, string token, string accion, string? entidad, string texto)
    {
        var url = $"/api/audit/logs?Action={Uri.EscapeDataString(accion)}&PageSize=200";
        if (entidad is not null) url += $"&EntityType={Uri.EscapeDataString(entidad)}";
        var pagina = await GetAsync(http, token, url);
        // Sólo las operaciones: AuditBehavior no pone EntityId; el rastro por entidad lleva el PublicId.
        var evento = pagina.GetProperty("items").EnumerateArray().FirstOrDefault(e =>
            e.TryGetProperty("entityId", out var entidadId) && entidadId.ValueKind == JsonValueKind.Null
            && e.TryGetProperty("newValuesJson", out var nuevo) && nuevo.ValueKind == JsonValueKind.String
            && nuevo.GetString()!.Contains(texto, StringComparison.Ordinal));
        return evento.ValueKind == JsonValueKind.Object ? evento.GetRawText() : null;
    }

    /// <summary>La auditoría se vacía a Mongo cada pocos segundos; se pregunta hasta que llegue o venza el plazo.</summary>
    private static async Task<bool> EsperarEventoAsync(HttpClient http, string token, string accion, string texto)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        do
        {
            if (await HayEventoAsync(http, token, accion, null, texto)) return true;
            await Task.Delay(500);
        } while (DateTime.UtcNow < limite);
        return false;
    }

    private static async Task<long> TotalAsync(HttpClient http, string token, string url)
    {
        var pagina = await GetAsync(http, token, url);
        return pagina.TryGetProperty("totalCount", out var total) ? total.GetInt64() : pagina.GetArrayLength();
    }
}
