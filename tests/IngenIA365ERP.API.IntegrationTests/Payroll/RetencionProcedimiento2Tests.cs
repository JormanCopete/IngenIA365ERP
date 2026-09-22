using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Feature 010, US7 (T103; quickstart §3.7) por HTTP: un empleado en procedimiento 2 en un plan
/// propio con tres meses aprobados (septiembre a noviembre de 2027) → calcular el semestre 2 de
/// 2027 (rige enero–junio de 2028) → divisor = 3 meses de vinculación, explicación mes a mes →
/// sin permiso no se aprueba (404) → aprobar → la ficha tiene la vigencia nueva con origen
/// «calculado» y la anterior cerrada → la nómina de enero de 2028 aplica el porcentaje y su
/// explicación lo nombra → recalcular crea la versión 2.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class RetencionProcedimiento2Tests(CentralIdentityApiFixture fx)
{
    private const string Ruta = "/api/payroll/withholding-rates";

    [Fact]
    public async Task El_porcentaje_fijo_se_calcula_se_aprueba_y_la_nomina_siguiente_lo_aplica()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // El ejercicio contable de 2027: aprobar contabiliza el NM en el período del mes (otra prueba pudo abrirlo antes).
        var ejercicio2027 = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years", new { year = 2027 });
        ejercicio2027.StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity], await ejercicio2027.Content.ReadAsStringAsync());
        // El ejercicio contable de 2028: aprobar contabiliza el NM en el período del mes (otra prueba pudo abrirlo antes).
        var ejercicio2028 = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/periods/years", new { year = 2028 });
        ejercicio2028.StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity], await ejercicio2028.Content.ReadAsStringAsync());
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "P2E2E", name = "Plan procedimiento 2 e2e", periodicity = "Monthly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();

        // Juan gana 12.000.000 y está en procedimiento 2 con un 5 % digitado a mano, abierto.
        var documento = (700_000_000 + Random.Shared.Next(1, 99_999_999)).ToString();
        var persona = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/people", new { idType = "C", taxId = documento, firstName = "Juan", lastName = "Procedimiento", email = $"juan.{documento}@coop.nomina.test", isEmployee = true });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, await persona.Content.ReadAsStringAsync());
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();
        var empleado = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/employees", new { personPublicId = personaId, baseSalary = 12_000_000m, contractType = 1, hireDate = new DateTime(2027, 9, 1), payrollPlanPublicId = planId });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, await empleado.Content.ReadAsStringAsync());
        var juan = (await NominaE2E.LeerAsync(empleado)).GetGuid();
        var retencion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/employees/{juan}/withholding", new
        {
            procedure = 2, rates = new[] { new { ratePercent = 5.0m, validFrom = new DateTime(2027, 9, 1), validTo = (DateTime?)null } }, deductions = Array.Empty<object>(),
        });
        retencion.IsSuccessStatusCode.Should().BeTrue(await retencion.Content.ReadAsStringAsync());

        foreach (var mes in new[] { 9, 10, 11 })
        {
            var desde = new DateTime(2027, mes, 1);
            var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods",
                new { planPublicId = planId, startDate = desde, endDate = desde.AddMonths(1).AddDays(-1), description = $"P2 e2e {desde:yyyy-MM}", statusMessage = string.Empty });
            periodo.StatusCode.Should().Be(HttpStatusCode.Created, await periodo.Content.ReadAsStringAsync());
            var periodoId = (await NominaE2E.LeerAsync(periodo)).GetGuid();
            var calculada = await NominaE2E.CalcularAsync(http, admin, periodoId);
            await NominaE2E.AprobarAsync(http, admin, calculada.GetProperty("runPublicId").GetGuid());
        }

        // ------------------------------------------------------------- calcular --
        var calcular = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/calculate", new { year = 2027, semester = 2, employeePublicIds = new[] { juan } });
        calcular.StatusCode.Should().Be(HttpStatusCode.Created, await calcular.Content.ReadAsStringAsync());
        var lote = await NominaE2E.LeerAsync(calcular);
        var item = lote.GetProperty("items").EnumerateArray().Single();
        var calculoId = item.GetProperty("calculationPublicId").GetGuid();
        item.GetProperty("monthsUsed").GetInt32().Should().Be(3);
        item.GetProperty("divisor").GetDecimal().Should().Be(3m, "menos de doce meses: el divisor son los meses de vinculación");
        item.GetProperty("validFrom").GetString().Should().Be("2028-01-01");
        item.GetProperty("validTo").GetString().Should().Be("2028-06-30");
        var porcentaje = item.GetProperty("percentage").GetDecimal();
        porcentaje.Should().BeGreaterThan(0m, "12.000.000 mensuales pagan retención");
        lote.GetProperty("skipped").GetArrayLength().Should().Be(0);

        var detalle = await NominaE2E.GetAsync(http, admin, $"{Ruta}/{calculoId}");
        detalle.GetProperty("months").GetArrayLength().Should().Be(3);
        detalle.GetProperty("divisorSource").GetString().Should().Be("MesesDeVinculacion");
        detalle.GetProperty("steps").EnumerateArray().Should().Contain(s => s.GetProperty("label").GetString()!.Contains("meses de vinculación"));
        detalle.GetProperty("depurationSteps").GetArrayLength().Should().BeGreaterThan(3);
        (await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/retencion-p2?calculationId={calculoId}")).GetProperty("filas").GetArrayLength().Should().Be(4, "tres meses y la fila de la sumatoria");

        // -------------------------------------------------- sin permiso: 404 --
        var prohibido = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"{Ruta}/{calculoId}/approve", null);
        prohibido.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // -------------------------------------------------------------- aprobar --
        var aprobar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{calculoId}/approve", null);
        aprobar.StatusCode.Should().Be(HttpStatusCode.OK, await aprobar.Content.ReadAsStringAsync());
        var aprobado = await NominaE2E.LeerAsync(aprobar);
        aprobado.GetProperty("previousClosedAt").GetString().Should().Be("2027-12-31", "la vigencia manual abierta se cierra la víspera");
        var ficha = await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{juan}/withholding");
        var tasas = ficha.GetProperty("rates").EnumerateArray().OrderBy(r => r.GetProperty("validFrom").GetDateTime()).ToList();
        tasas.Should().HaveCount(2, "la anterior se cierra, no se borra (R8)");
        tasas[0].GetProperty("validTo").GetDateTime().Should().Be(new DateTime(2027, 12, 31));
        tasas[1].GetProperty("ratePercent").GetDecimal().Should().Be(porcentaje);
        (await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/{calculoId}/approve", null)).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // --------------------------------------- enero de 2028 aplica el porcentaje --
        var enero = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods",
            new { planPublicId = planId, startDate = new DateTime(2028, 1, 1), endDate = new DateTime(2028, 1, 31), description = "P2 e2e 2028-01", statusMessage = string.Empty });
        enero.StatusCode.Should().Be(HttpStatusCode.Created, await enero.Content.ReadAsStringAsync());
        var eneroId = (await NominaE2E.LeerAsync(enero)).GetGuid();
        var corrida = await NominaE2E.CalcularAsync(http, admin, eneroId);
        var runId = corrida.GetProperty("runPublicId").GetGuid();
        var detalleJuan = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/employees/{juan}");
        var retefte = detalleJuan.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("conceptCode").GetString() == "RETEFTE");
        retefte.GetProperty("amount").GetDecimal().Should().BeGreaterThan(0m);
        var explicacion = JsonSerializer.Serialize(retefte.GetProperty("explanation"));
        explicacion.Should().Contain("procedimiento 2", "la explicación nombra el porcentaje fijo aplicado");
        explicacion.Should().Contain(JsonSerializer.Serialize(porcentaje), "y trae el porcentaje aprobado");

        // ----------------------------------------------------- recalcular: versión 2 --
        var otra = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"{Ruta}/calculate", new { year = 2027, semester = 2, employeePublicIds = new[] { juan } });
        otra.StatusCode.Should().Be(HttpStatusCode.Created);
        (await NominaE2E.LeerAsync(otra)).GetProperty("items").EnumerateArray().Single().GetProperty("version").GetInt32().Should().Be(2);
        var lista = await NominaE2E.GetAsync(http, admin, $"{Ruta}?year=2027&semester=2&employeeId={juan}");
        lista.EnumerateArray().Should().Contain(c => c.GetProperty("calculationPublicId").GetGuid() == calculoId && c.GetProperty("status").GetInt32() == 1, "la aprobada no se toca");
    }
}
