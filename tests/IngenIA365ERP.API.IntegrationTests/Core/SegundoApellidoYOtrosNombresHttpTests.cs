using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;
using static IngenIA365ERP.API.IntegrationTests.Payroll.NominaE2E;

namespace IngenIA365ERP.API.IntegrationTests.Core;

/// <summary>
/// «Segundo apellido» y «Otros nombres» (feature 010, D-06) recorridos por HTTP, como los manda la
/// pantalla: crear en Personas, editar en Personas y dar de alta un empleado con persona nueva.
/// Cada camino relee la persona con <c>GET /api/core/people/{id}</c>.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class SegundoApellidoYOtrosNombresHttpTests(CentralIdentityApiFixture fx)
{
    private static int _documento = 810_000_000;
    private static string Documento() => Interlocked.Increment(ref _documento).ToString();

    private static object Persona(string documento, string? segundoApellido, string? otrosNombres) => new
    {
        idType = "C", taxId = documento, firstName = "WILLIAN", lastName = "LAGOS",
        secondLastName = segundoApellido, otherNames = otrosNombres, personType = "01",
    };

    [Fact]
    public async Task Crear_y_editar_en_personas_guardan_los_dos_campos()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var documento = Documento();

        var alta = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/core/people",
            Persona(documento, "PÉREZ", "ANDRÉS FELIPE"));
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var id = (await LeerAsync(alta)).GetGuid();

        var creada = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{id}");
        creada.GetProperty("secondLastName").GetString().Should().Be("PÉREZ");
        creada.GetProperty("otherNames").GetString().Should().Be("ANDRÉS FELIPE");

        var edicion = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Put, $"/api/core/people/{id}",
            Persona(documento, "GÓMEZ", "JOSÉ"));
        edicion.StatusCode.Should().Be(HttpStatusCode.NoContent, await edicion.Content.ReadAsStringAsync());

        var editada = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{id}");
        editada.GetProperty("secondLastName").GetString().Should().Be("GÓMEZ");
        editada.GetProperty("otherNames").GetString().Should().Be("JOSÉ");
    }

    [Fact]
    public async Task Empleado_con_persona_nueva_guarda_los_dos_campos()
    {
        var ctx = await PrepararAsync(fx);
        using var http = fx.CreateClient();
        var documento = Documento();

        var alta = await EnviarAsync(http, ctx.TokenAdmin, HttpMethod.Post, "/api/payroll/employees/with-person", new
        {
            person = Persona(documento, "PÉREZ", "ANDRÉS"),
            employee = new { baseSalary = 2_600_000m, contractType = 1, hireDate = new DateTime(2026, 9, 15), payrollBankAccountType = 1 },
        });
        alta.StatusCode.Should().Be(HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());
        var personaId = (await LeerAsync(alta)).GetProperty("personPublicId").GetGuid();

        var persona = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/{personaId}");
        persona.GetProperty("secondLastName").GetString().Should().Be("PÉREZ");
        persona.GetProperty("otherNames").GetString().Should().Be("ANDRÉS");

        // Listados y buscador muestran el nombre completo y también encuentran por las partes nuevas.
        const string completo = "WILLIAN ANDRÉS LAGOS PÉREZ";
        var listado = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people?SearchTerm={documento}");
        listado.GetProperty("items")[0].GetProperty("fullName").GetString().Should().Be(completo);

        var porOtroNombre = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people?SearchTerm=ANDRÉS");
        porOtroNombre.GetProperty("items").EnumerateArray()
            .Should().Contain(i => i.GetProperty("taxId").GetString() == documento);

        var busqueda = await GetAsync(http, ctx.TokenAdmin, $"/api/core/people/search?q={documento}");
        busqueda[0].GetProperty("fullName").GetString().Should().Be(completo);

        var empleados = await GetAsync(http, ctx.TokenAdmin, $"/api/payroll/employees?PageNumber=1&PageSize=20&Search={documento}");
        var items = empleados.ValueKind == System.Text.Json.JsonValueKind.Array ? empleados : empleados.GetProperty("items");
        items[0].GetProperty("fullName").GetString().Should().Be(completo);
    }
}
