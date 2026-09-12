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

    /// <summary>
    /// QA, 2026-09-11: toda liquidación salía con «Sin clase de riesgo ARL registrada en la
    /// ficha». Dos causas a la vez: la cooperativa no tenía las clases (nadie las sembraba) y
    /// la ficha no tenía dónde elegirla (el comando no la recibía). Con la clase en la ficha,
    /// el aporte ARL sale con el porcentaje de esa clase y el empleado no queda bloqueado por
    /// ARL.
    /// </summary>
    [Fact]
    public async Task Con_clase_de_riesgo_en_la_ficha_se_liquida_el_aporte_ARL()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        // La semilla deja las cinco clases en toda cooperativa; la ficha guarda la fila, no el número.
        var clases = (await NominaE2E.GetAsync(http, admin, "/api/payroll/work-risk-rates?pageSize=50")).GetProperty("items").EnumerateArray().ToList();
        clases.Select(c => c.GetProperty("code").GetInt32()).Should().BeEquivalentTo([1, 2, 3, 4, 5]);
        var claseIII = clases.Single(c => c.GetProperty("code").GetInt32() == 3).GetProperty("publicId").GetGuid();

        var (empleadoId, _) = await NominaE2E.CrearEmpleadoAsync(http, admin, "Fabio", 3_000_000m, new DateTime(2024, 2, 1));
        var actualizar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/employees/{empleadoId}", new
        {
            baseSalary = 3_000_000m, contractType = 1, workRiskRatePublicId = claseIII, payrollBankAccountType = 1,
        });
        actualizar.IsSuccessStatusCode.Should().BeTrue(await actualizar.Content.ReadAsStringAsync());
        var ficha = await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{empleadoId}");
        ficha.GetProperty("workRiskRatePublicId").GetGuid().Should().Be(claseIII);
        ficha.GetProperty("workRiskRateName").GetString().Should().StartWith("Clase III");

        var periodoId = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2026, 7, 1), new DateTime(2026, 7, 31));
        var corrida = await NominaE2E.CalcularAsync(http, admin, periodoId);
        var runId = corrida.GetProperty("runPublicId").GetGuid();

        var detalle = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/employees/{empleadoId}");
        var arl = detalle.GetProperty("lines").EnumerateArray().SingleOrDefault(l => l.GetProperty("conceptCode").GetString() == "ARL");
        arl.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object, "con clase en la ficha el aporte ARL se calcula");
        // Clase III = 2,436 % del IBC (3.000.000 × 30/30), el porcentaje vigente del parámetro legal.
        arl.GetProperty("amount").GetDecimal().Should().Be(73_080m);
        detalle.GetProperty("refusals").EnumerateArray().Select(r => r.GetString()).Should().NotContain(r => r!.Contains("ARL"));
    }

    /// <summary>
    /// La ficha guarda fondo de cesantías y caja de compensación (2026-09-12): hasta entonces
    /// no había dónde elegirlos, y el fondo estaba en una columna decimal que nadie leía. Los
    /// dos son catálogos nuevos o sin uso, así que la prueba los crea por la API.
    /// </summary>
    [Fact]
    public async Task La_ficha_guarda_fondo_de_cesantias_y_caja_de_compensacion()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var fondo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/severance-providers",
            new { code = 901, name = "Fondo Nacional del Ahorro", shortName = "FNA", taxId = "899999284", checkDigit = 1 });
        fondo.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.BadRequest], "otra corrida pudo crearlo ya");
        var caja = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/family-compensation-funds",
            new { code = 901, name = "Comfandi", shortName = "COMFANDI", taxId = "890303093", checkDigit = 5 });
        caja.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.BadRequest], "otra corrida pudo crearlo ya");

        var fondoId = (await NominaE2E.GetAsync(http, admin, "/api/payroll/severance-providers?pageSize=500")).GetProperty("items")
            .EnumerateArray().Single(f => f.GetProperty("code").GetInt32() == 901).GetProperty("publicId").GetGuid();
        var cajaId = (await NominaE2E.GetAsync(http, admin, "/api/payroll/family-compensation-funds?pageSize=500")).GetProperty("items")
            .EnumerateArray().Single(c => c.GetProperty("code").GetInt32() == 901).GetProperty("publicId").GetGuid();

        var (empleadoId, _) = await NominaE2E.CrearEmpleadoAsync(http, admin, "Gloria", 2_500_000m, new DateTime(2024, 2, 1));
        var actualizar = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/employees/{empleadoId}", new
        {
            baseSalary = 2_500_000m, contractType = 1, severanceProviderPublicId = fondoId, familyCompensationFundPublicId = cajaId, payrollBankAccountType = 1,
        });
        actualizar.IsSuccessStatusCode.Should().BeTrue(await actualizar.Content.ReadAsStringAsync());

        var ficha = await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{empleadoId}");
        ficha.GetProperty("severanceProviderPublicId").GetGuid().Should().Be(fondoId);
        ficha.GetProperty("severanceProviderName").GetString().Should().Be("Fondo Nacional del Ahorro");
        ficha.GetProperty("familyCompensationFundPublicId").GetGuid().Should().Be(cajaId);
        ficha.GetProperty("familyCompensationFundName").GetString().Should().Be("Comfandi");
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
