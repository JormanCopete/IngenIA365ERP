using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Decisión del dueño del 2026-10-22, por HTTP sobre la cooperativa compartida de nómina: el empleado lleva un centro de
/// costo contable (alta, alta con persona y edición), la ficha y el listado lo devuelven, el listado filtra por él, un PUT
/// que no lo manda no lo borra, el cambio queda auditado, y el comprobante <c>NM</c> de la nómina aprobada pone ese centro
/// en las líneas cuya cuenta lo maneja y usa la cuenta por concepto y centro. Plan propio (CCEMP) y agosto de 2027 para no
/// mezclarse con las otras e2e: cada período incluye a todos los empleados vigentes de su plan.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class CentroDeCostoDelEmpleadoTests(CentralIdentityApiFixture fx)
{
    private const string GastoComercial = "51050302";
    private static int _documento = 820_000_000;

    [Fact]
    public async Task El_centro_del_empleado_llega_al_comprobante_NM_con_la_cuenta_por_concepto_y_centro()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        await Accounting.ContabilidadE2E.AbrirEjercicioAsync(http, admin, 2027);

        // --- el centro "Comercial" y su gasto de salarios, que exige centro de costo ---
        var codigo = "CM" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var centro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/core/cost-centers", new { code = codigo, name = "Comercial", payrollType = 1, period = 1, payrollPeriodicity = 30 });
        centro.StatusCode.Should().Be(HttpStatusCode.Created, await centro.Content.ReadAsStringAsync());
        var comercial = (await NominaE2E.LeerAsync(centro)).GetGuid();

        var busqueda = await NominaE2E.GetAsync(http, admin, "/api/accounting/accounts/search?q=510503&onlyMovement=false");
        var padre = busqueda.EnumerateArray().First(c => c.GetProperty("code").GetString() == "510503").GetProperty("publicId").GetGuid();
        var cuenta = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/accounts", new
        {
            code = GastoComercial, name = "Sueldos del área comercial", parentPublicId = padre,
            enabledModules = new[] { "CNT", "NOM" }, requiresThirdParty = false, requiresCrossDocument = false, requiresCostCenter = true, requiresBranch = false,
        });
        cuenta.StatusCode.Should().BeOneOf([HttpStatusCode.Created, HttpStatusCode.OK], await cuenta.Content.ReadAsStringAsync());
        var cuentas = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, "/api/payroll/concept-definitions/SALARIO/accounts", new
        {
            rows = new object[]
            {
                new { costCenterPublicId = (Guid?)null, debitAccountCode = NominaE2E.CuentaDebito, creditAccountCode = NominaE2E.CuentaCredito },
                new { costCenterPublicId = (Guid?)comercial, debitAccountCode = GastoComercial, creditAccountCode = NominaE2E.CuentaCredito },
            },
        });
        cuentas.IsSuccessStatusCode.Should().BeTrue($"cuentas de SALARIO: «{await cuentas.Content.ReadAsStringAsync()}»");

        // --- plan propio y tres empleados: Alba (alta con el centro), Beto (sin centro), Carla (alta con persona y centro) ---
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "CCEMP", name = "Plan centro de costo e2e", periodicity = "Monthly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();

        var alba = await EmpleadoAsync(http, admin, "Alba", 2_000_000m, planId, comercial);
        var beto = await EmpleadoAsync(http, admin, "Beto", 3_000_000m, planId, null);
        var documentoCarla = Interlocked.Increment(ref _documento).ToString();
        var conPersona = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/employees/with-person", new
        {
            person = new { idType = "C", taxId = documentoCarla, firstName = "Carla", lastName = "Prueba", email = $"carla.{documentoCarla}@coop.nomina.test" },
            employee = new { baseSalary = 2_500_000m, contractType = 1, hireDate = new DateTime(2026, 1, 15), payrollPlanPublicId = planId, costCenterPublicId = (Guid?)comercial },
        });
        conPersona.StatusCode.Should().Be(HttpStatusCode.Created, await conPersona.Content.ReadAsStringAsync());
        var carla = (await NominaE2E.LeerAsync(conPersona)).GetProperty("employeePublicId").GetGuid();

        // --- la ficha y el listado lo devuelven; el listado filtra por centro ---
        var ficha = await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{alba}");
        ficha.GetProperty("costCenterPublicId").GetGuid().Should().Be(comercial);
        ficha.GetProperty("costCenterCode").GetString().Should().Be(codigo);
        ficha.GetProperty("costCenterName").GetString().Should().Be("Comercial");
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{beto}")).GetProperty("costCenterPublicId").ValueKind.Should().Be(JsonValueKind.Null);
        var filtrado = (await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees?PageNumber=1&PageSize=100&CostCenterPublicId={comercial}"))
            .GetProperty("items").EnumerateArray().Select(i => i.GetProperty("publicId").GetGuid()).ToList();
        filtrado.Should().BeEquivalentTo([alba, carla]);

        // --- un PUT que no lo manda no lo borra; un centro inexistente se rechaza; asignar y quitar queda auditado ---
        (await PutAsync(http, admin, alba, 2_000_000m, new { })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{alba}")).GetProperty("costCenterPublicId").GetGuid().Should().Be(comercial);

        var inexistente = await PutAsync(http, admin, beto, 3_000_000m, new { costCenterPublicId = Guid.NewGuid() });
        inexistente.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await inexistente.Content.ReadAsStringAsync()).Should().Contain("Core.CostCenter.NotFound");

        (await PutAsync(http, admin, beto, 3_000_000m, new { costCenterPublicId = comercial })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{beto}")).GetProperty("costCenterPublicId").GetGuid().Should().Be(comercial);
        (await PutAsync(http, admin, beto, 3_000_000m, new { clearCostCenter = true })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/employees/{beto}")).GetProperty("costCenterPublicId").ValueKind.Should().Be(JsonValueKind.Null);

        var auditoria = await Accounting.ContabilidadE2E.EventosDeAuditoriaAsync(http, admin, "Payroll", "Payroll.Employee.CostCenterChanged",
            e => e.GetProperty("entityId").GetString() == beto.ToString(), minimo: 2);
        auditoria.Should().HaveCountGreaterThanOrEqualTo(2, "asignar y quitar son dos cambios");
        auditoria.Should().Contain(e => (e.GetProperty("newValuesJson").GetString() ?? "").Contains(codigo), "el evento de la asignación trae el centro nuevo");
        auditoria.Should().Contain(e => (e.GetProperty("oldValuesJson").GetString() ?? "").Contains(codigo), "el de la retirada trae el centro anterior");

        // --- liquidar y aprobar agosto de 2027 del plan: el NM lleva el centro donde la cuenta lo maneja ---
        var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods",
            new { planPublicId = planId, startDate = new DateTime(2027, 8, 1), endDate = new DateTime(2027, 8, 31), description = "Agosto 2027 (centro de costo e2e)", statusMessage = string.Empty });
        periodo.StatusCode.Should().Be(HttpStatusCode.Created, await periodo.Content.ReadAsStringAsync());
        var periodoId = (await NominaE2E.LeerAsync(periodo)).GetGuid();
        var calculo = await NominaE2E.CalcularAsync(http, admin, periodoId);
        var runId = calculo.GetProperty("runPublicId").GetGuid();
        await NominaE2E.AprobarAsync(http, admin, runId);

        var comprobante = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/by-source?module=NOM&sourcePublicId={runId}");
        comprobante.GetProperty("voucherTypeCode").GetString().Should().Be("NM");
        var lineas = comprobante.GetProperty("lines").EnumerateArray().ToList();
        var salarios = lineas.Where(l => l.GetProperty("detail").GetString() == "Nómina SALARIO" && l.GetProperty("debit").GetDecimal() > 0m).ToList();

        // Alba y Carla (centro Comercial) van a la cuenta por concepto y centro, con el centro en la línea…
        var delCentro = salarios.Where(l => l.GetProperty("accountCode").GetString() == GastoComercial).ToList();
        delCentro.Should().HaveCount(2);
        delCentro.Should().OnlyContain(l => l.GetProperty("costCenterPublicId").GetGuid() == comercial);
        // …y Beto, sin centro, a la fila por defecto del concepto y sin centro.
        var deBeto = salarios.Where(l => l.GetProperty("accountCode").GetString() == NominaE2E.CuentaDebito).ToList();
        deBeto.Should().ContainSingle().Which.GetProperty("costCenterPublicId").ValueKind.Should().Be(JsonValueKind.Null);
        // El pasivo y el resto de cuentas no manejan centro: el contrato lo descarta, no rechaza el comprobante.
        lineas.Where(l => l.GetProperty("costCenterPublicId").ValueKind == JsonValueKind.String)
            .Should().OnlyContain(l => l.GetProperty("accountCode").GetString() == GastoComercial);
        comprobante.GetProperty("totalDebit").GetDecimal().Should().Be(comprobante.GetProperty("totalCredit").GetDecimal());
    }

    private static async Task<Guid> EmpleadoAsync(HttpClient http, string token, string nombre, decimal salario, Guid planId, Guid? centro)
    {
        var documento = Interlocked.Increment(ref _documento).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Prueba", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var personaId = (await NominaE2E.LeerAsync(persona)).GetGuid();
        var empleado = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = personaId, baseSalary = salario, contractType = 1, hireDate = new DateTime(2026, 1, 15), payrollPlanPublicId = planId, costCenterPublicId = centro,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await empleado.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(empleado)).GetGuid();
    }

    /// <summary>PUT de la ficha con lo mínimo de siempre más lo que se quiera decir del centro.</summary>
    private static Task<HttpResponseMessage> PutAsync(HttpClient http, string token, Guid empleado, decimal salario, object centro)
    {
        var cuerpo = new Dictionary<string, object?> { ["baseSalary"] = salario, ["contractType"] = 1 };
        foreach (var p in centro.GetType().GetProperties()) cuerpo[p.Name] = p.GetValue(centro);
        return NominaE2E.EnviarAsync(http, token, HttpMethod.Put, $"/api/payroll/employees/{empleado}", cuerpo);
    }
}
