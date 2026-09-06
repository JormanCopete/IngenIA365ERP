using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// T079 — US2 por HTTP, el ciclo entero: período → novedades → calcular → recalcular → aprobar →
/// segundo cálculo rechazado → comprobante NM cuadrado. Todo por la API y con la base real.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class PayrollRunsEndpointsTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Ciclo_completo_calcular_recalcular_aprobar_y_cuadrar()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var (empleadoId, _) = await NominaE2E.CrearEmpleadoAsync(http, admin, "Diana", 3_000_000m, new DateTime(2024, 2, 1));
        var periodoId = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30));
        var horasId = await NominaE2E.RegistrarNovedadAsync(http, admin, periodoId, new { employeePublicId = empleadoId, conceptCode = "HEX_NOCTURNA", quantity = 6 });

        // --- calcular: borrador v1 con el bloqueo de afiliaciones del empleado de prueba ---
        // El período incluye a todos los empleados vigentes del plan (los de las otras pruebas también).
        var v1 = await NominaE2E.CalcularAsync(http, admin, periodoId);
        v1.GetProperty("version").GetInt32().Should().Be(1);
        v1.GetProperty("employeeCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        v1.GetProperty("totals").GetProperty("net").GetDecimal().Should().BeGreaterThan(0m);
        v1.GetProperty("blockers").EnumerateArray().Should().Contain(b => b.GetProperty("flag").GetString() == "MissingAffiliation" && b.GetProperty("employeePublicId").GetGuid() == empleadoId);
        v1.GetProperty("blockers").EnumerateArray().Should().OnlyContain(b => b.GetProperty("employeeName").GetString()!.Contains("Prueba"), "el bloqueo nombra al empleado, no su identificador");
        var run1 = v1.GetProperty("runPublicId").GetGuid();

        var actual = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/runs/current");
        actual.GetProperty("status").GetString().Should().Be("Draft");

        var empleados = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{run1}/employees");
        var diana = empleados.EnumerateArray().Single(e => e.GetProperty("employeePublicId").GetGuid() == empleadoId);
        diana.GetProperty("daysWorked").GetInt32().Should().Be(30);
        diana.GetProperty("flags").EnumerateArray().Select(f => f.GetString()).Should().Contain("MissingAffiliation");

        var detalle = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{run1}/employees/{empleadoId}");
        var lineas = detalle.GetProperty("lines").EnumerateArray().ToList();
        lineas.Select(l => l.GetProperty("conceptCode").GetString()).Should().Contain("SALARIO").And.Contain("HEX_NOCTURNA").And.Contain("SALUD_EMP").And.Contain("PENSION_EMP");
        lineas.Single(l => l.GetProperty("conceptCode").GetString() == "SALARIO").GetProperty("amount").GetDecimal().Should().Be(3_000_000m);
        lineas.Should().OnlyContain(l => l.GetProperty("explanation").ValueKind == System.Text.Json.JsonValueKind.Object, "cada línea trae su explicación");

        // --- corregir la novedad deja el borrador desactualizado; aprobar así se niega ---
        var correccion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/novelties/{horasId}", new { quantity = 8, reason = "Eran ocho" });
        correccion.StatusCode.Should().Be(HttpStatusCode.OK, $"«{await correccion.Content.ReadAsStringAsync()}»");
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/runs/current")).GetProperty("status").GetString().Should().Be("Stale");

        var aprobarStale = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{run1}/approve", new { confirm = true, exceptions = Array.Empty<object>() });
        aprobarStale.IsSuccessStatusCode.Should().BeFalse();
        (await NominaE2E.CodigoDeErrorAsync(aprobarStale)).Should().Be("Payroll.RunStale");

        // --- recalcular: v2, el empleado cambió, v1 queda reemplazada ---
        var v2 = await NominaE2E.CalcularAsync(http, admin, periodoId);
        v2.GetProperty("version").GetInt32().Should().Be(2);
        v2.GetProperty("changedEmployees").GetInt32().Should().Be(1);
        var run2 = v2.GetProperty("runPublicId").GetGuid();
        v2.GetProperty("totals").GetProperty("net").GetDecimal().Should().BeGreaterThan(v1.GetProperty("totals").GetProperty("net").GetDecimal(), "dos horas extra más");

        var historial = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/runs");
        historial.EnumerateArray().Single(r => r.GetProperty("publicId").GetGuid() == run1).GetProperty("status").GetString().Should().Be("Superseded");

        // --- aprobar: sin la segunda confirmación de segregación se niega (quien calculó aprueba) ---
        var sinSegunda = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{run2}/approve", new
        {
            confirm = true,
            exceptions = await NominaE2E.ExcepcionesParaAsync(http, admin, run2),
        });
        sinSegunda.IsSuccessStatusCode.Should().BeFalse();
        (await NominaE2E.CodigoDeErrorAsync(sinSegunda)).Should().Be("Payroll.ConfirmationRequired");

        var aprobacion = await NominaE2E.AprobarAsync(http, admin, run2);
        aprobacion.GetProperty("accountingDocumentNumber").GetString().Should().StartWith("NM-");
        aprobacion.GetProperty("approvedWithoutSegregation").GetBoolean().Should().BeTrue();

        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/runs/current")).GetProperty("status").GetString().Should().Be("Approved");

        // --- un segundo cálculo se rechaza; el comprobante cuadra ---
        var otraVez = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/runs", null);
        otraVez.IsSuccessStatusCode.Should().BeFalse();
        (await NominaE2E.CodigoDeErrorAsync(otraVez)).Should().Be("Payroll.PeriodApproved");

        var cuadre = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{run2}/balance-check");
        cuadre.GetProperty("earningsMinusDeductionsEqualsNet").GetBoolean().Should().BeTrue();
        cuadre.GetProperty("linesMatchEmployeeTotals").GetBoolean().Should().BeTrue();
        cuadre.GetProperty("employerAndProvisionsOutsideNet").GetBoolean().Should().BeTrue();
        cuadre.GetProperty("accountingDocumentBalanced").GetBoolean().Should().BeTrue("el NM se generó cuadrado en la misma transacción");

        var comparativo = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{run2}/comparison");
        comparativo.GetProperty("rows").EnumerateArray().Should().Contain(r => r.GetProperty("employeePublicId").GetGuid() == empleadoId);

        var exportacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/payroll/runs/{run2}/export", null);
        exportacion.StatusCode.Should().Be(HttpStatusCode.OK);
        exportacion.Content.Headers.ContentType?.MediaType.Should().Be("text/csv");
        (await exportacion.Content.ReadAsStringAsync()).Should().Contain("HEX_NOCTURNA");
    }

    [Fact]
    public async Task Sin_permiso_de_calcular_la_ruta_no_existe()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var periodoId = await NominaE2E.CrearPeriodoAsync(http, ctx.TokenAdmin, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        var resp = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/runs", null);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
