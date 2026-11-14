using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Cuenta contable propia de cada entidad de seguridad social, por HTTP sobre la cooperativa compartida de nómina: dos EPS
/// con su auxiliar 245005xx (que exige tercero) y una persona que las representa, un empleado en cada una, y la nómina
/// aprobada acredita el aporte del empleador y la deducción de salud en la cuenta de cada EPS, con la EPS como tercero,
/// mientras el débito de la deducción sigue con el empleado. La pantalla de EPS la lee con sus reglas, el catálogo rechaza
/// una cuenta que no es auxiliar y el diálogo de cuentas del concepto lo avisa. Plan propio (CTAEPS) y septiembre de 2027
/// para no mezclarse con las otras e2e.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class CuentaPorEntidadTests(CentralIdentityApiFixture fx)
{
    private static int _documento = 830_000_000;

    [Fact]
    public async Task Una_nomina_con_empleados_en_dos_EPS_acredita_la_cuenta_de_cada_EPS_con_la_EPS_como_tercero()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        await Accounting.ContabilidadE2E.AbrirEjercicioAsync(http, admin, 2027);

        // --- dos auxiliares bajo 245005 (aportes a EPS) que exigen tercero, y una EPS con cada una ---
        var sufijo = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var cuentaA = await AuxiliarAsync(http, admin, "245005", "EPS A por pagar");
        var cuentaB = await AuxiliarAsync(http, admin, "245005", "EPS B por pagar");

        // Una cuenta que no es auxiliar (la subcuenta del catálogo) no sirve para la entidad.
        var personaA = await PersonaAsync(http, admin, "Eps", "Alfa");
        var malo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/health-insurance", new
        {
            code = "EA" + sufijo, name = "EPS Alfa " + sufijo, taxId = "800100001", checkDigit = 1, personPublicId = personaA, payrollAccountCode = "245005",
        });
        malo.StatusCode.Should().Be(HttpStatusCode.BadRequest, await malo.Content.ReadAsStringAsync());
        (await malo.Content.ReadAsStringAsync()).Should().Contain("Accounting.Account.NotEligible");

        var epsA = await EpsAsync(http, admin, "EA" + sufijo, "EPS Alfa " + sufijo, personaA, cuentaA);
        var personaB = await PersonaAsync(http, admin, "Eps", "Beta");
        var epsB = await EpsAsync(http, admin, "EB" + sufijo, "EPS Beta " + sufijo, personaB, cuentaB);

        // La pantalla la lee con sus reglas.
        var leida = await NominaE2E.GetAsync(http, admin, $"/api/payroll/health-insurance/{epsA}");
        leida.GetProperty("payrollAccount").GetProperty("code").GetString().Should().Be(cuentaA);
        leida.GetProperty("payrollAccount").GetProperty("requiresThirdParty").GetBoolean().Should().BeTrue();
        leida.GetProperty("payrollAccount").GetProperty("problem").ValueKind.Should().Be(JsonValueKind.Null);

        // El diálogo de cuentas del concepto avisa que el crédito se reemplaza.
        var aviso = await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/SALUD_EMPLEADOR/entity-accounts");
        aviso.GetProperty("entityKind").GetString().Should().Be("Eps");
        aviso.GetProperty("withAccount").EnumerateArray().Select(e => e.GetProperty("accountCode").GetString()).Should().Contain([cuentaA, cuentaB]);
        (await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/SALARIO/entity-accounts"))
            .GetProperty("entityKind").ValueKind.Should().Be(JsonValueKind.Null);

        // --- plan propio, un empleado en cada EPS; liquidar y aprobar septiembre de 2027 ---
        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "CTAEPS", name = "Plan cuenta por entidad e2e", periodicity = "Monthly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());
        var planId = (await NominaE2E.LeerAsync(plan)).GetProperty("publicId").GetGuid();
        var (_, documentoAna) = await EmpleadoAsync(http, admin, "Ana", 2_000_000m, planId, epsA);
        var (_, documentoBeto) = await EmpleadoAsync(http, admin, "Beto", 3_000_000m, planId, epsB);

        var periodo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/pay-periods",
            new { planPublicId = planId, startDate = new DateTime(2027, 9, 1), endDate = new DateTime(2027, 9, 30), description = "Septiembre 2027 (cuenta por entidad e2e)", statusMessage = string.Empty });
        periodo.StatusCode.Should().Be(HttpStatusCode.Created, await periodo.Content.ReadAsStringAsync());
        var calculo = await NominaE2E.CalcularAsync(http, admin, (await NominaE2E.LeerAsync(periodo)).GetGuid());
        var runId = calculo.GetProperty("runPublicId").GetGuid();
        await NominaE2E.AprobarAsync(http, admin, runId);

        var comprobante = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/by-source?module=NOM&sourcePublicId={runId}");
        comprobante.GetProperty("totalDebit").GetDecimal().Should().Be(comprobante.GetProperty("totalCredit").GetDecimal());
        var lineas = comprobante.GetProperty("lines").EnumerateArray().ToList();
        static bool De(JsonElement l, string concepto) => (l.GetProperty("detail").GetString() ?? "").Split(" · ")[0] == $"Nómina {concepto}";
        static decimal Credito(JsonElement l) => l.GetProperty("credit").GetDecimal();

        // Aporte del empleador: dos créditos en dos cuentas, cada uno con su EPS como tercero; nada en la cuenta del concepto.
        var aporte = lineas.Where(l => De(l, "SALUD_EMPLEADOR") && Credito(l) > 0m).ToList();
        aporte.Should().HaveCount(2);
        aporte.Select(l => l.GetProperty("accountCode").GetString()).Should().BeEquivalentTo([cuentaA, cuentaB]);
        aporte.Single(l => l.GetProperty("accountCode").GetString() == cuentaA).GetProperty("personPublicId").GetGuid().Should().Be(personaA);
        aporte.Single(l => l.GetProperty("accountCode").GetString() == cuentaB).GetProperty("personPublicId").GetGuid().Should().Be(personaB);
        aporte.Single(l => l.GetProperty("accountCode").GetString() == cuentaB).GetProperty("credit").GetDecimal()
            .Should().BeGreaterThan(aporte.Single(l => l.GetProperty("accountCode").GetString() == cuentaA).GetProperty("credit").GetDecimal(), "Beto gana más que Ana");

        // Deducción de salud del empleado: el crédito va a la cuenta de cada EPS con la EPS; el débito sigue con el empleado.
        var deduccion = lineas.Where(l => De(l, "SALUD_EMP")).ToList();
        deduccion.Where(l => Credito(l) > 0m).Select(l => (l.GetProperty("accountCode").GetString(), l.GetProperty("personPublicId").GetGuid()))
            .Should().BeEquivalentTo([(cuentaA, personaA), (cuentaB, personaB)]);
        deduccion.Where(l => l.GetProperty("debit").GetDecimal() > 0m).Select(l => l.GetProperty("personTaxId").GetString())
            .Should().BeEquivalentTo([documentoAna, documentoBeto]);

        // Sin cuenta propia en los fondos de pensiones, la pensión del empleado queda con la cuenta del concepto.
        lineas.Where(l => De(l, "PENSION_EMP") && Credito(l) > 0m).Should().NotBeEmpty()
            .And.OnlyContain(l => l.GetProperty("accountCode").GetString() == NominaE2E.CuentaCredito);
    }

    private static async Task<string> AuxiliarAsync(HttpClient http, string token, string padre, string nombre)
    {
        var busqueda = await NominaE2E.GetAsync(http, token, $"/api/accounting/accounts/search?q={padre}&onlyMovement=false");
        var padreId = busqueda.EnumerateArray().First(c => c.GetProperty("code").GetString() == padre).GetProperty("publicId").GetGuid();
        for (var intento = 0; intento < 20; intento++)
        {
            var codigo = padre + Random.Shared.Next(10, 99).ToString();
            var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/accounting/accounts", new
            {
                code = codigo, name = nombre, parentPublicId = padreId,
                enabledModules = new[] { "CNT", "NOM" }, requiresThirdParty = true, requiresCrossDocument = false, requiresCostCenter = false, requiresBranch = false,
            });
            if (resp.IsSuccessStatusCode) return codigo;
            if (resp.StatusCode is not (HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest))
                resp.IsSuccessStatusCode.Should().BeTrue($"auxiliar {codigo}: «{await resp.Content.ReadAsStringAsync()}»");
        }
        throw new InvalidOperationException($"No se pudo crear una auxiliar libre bajo {padre}.");
    }

    private static async Task<Guid> PersonaAsync(HttpClient http, string token, string nombre, string apellido)
    {
        var documento = Interlocked.Increment(ref _documento).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = apellido, email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test",
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(persona)).GetGuid();
    }

    private static async Task<Guid> EpsAsync(HttpClient http, string token, string codigo, string nombre, Guid persona, string cuenta)
    {
        var resp = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/health-insurance", new
        {
            code = codigo, name = nombre, taxId = "800100002", checkDigit = 1, personPublicId = persona, payrollAccountCode = cuenta,
        });
        resp.StatusCode.Should().Be(HttpStatusCode.Created, $"EPS {codigo}: «{await resp.Content.ReadAsStringAsync()}»");
        return (await NominaE2E.LeerAsync(resp)).GetGuid();
    }

    private static async Task<(Guid Empleado, string Documento)> EmpleadoAsync(HttpClient http, string token, string nombre, decimal salario, Guid planId, Guid eps)
    {
        var documento = Interlocked.Increment(ref _documento).ToString();
        var persona = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/core/people", new
        {
            idType = "C", taxId = documento, firstName = nombre, lastName = "Prueba", email = $"{nombre.ToLowerInvariant()}.{documento}@coop.nomina.test", isEmployee = true,
        });
        persona.StatusCode.Should().Be(HttpStatusCode.Created, $"persona: «{await persona.Content.ReadAsStringAsync()}»");
        var empleado = await NominaE2E.EnviarAsync(http, token, HttpMethod.Post, "/api/payroll/employees", new
        {
            personPublicId = (await NominaE2E.LeerAsync(persona)).GetGuid(), baseSalary = salario, contractType = 1, hireDate = new DateTime(2026, 1, 15),
            payrollPlanPublicId = planId, healthInsurancePublicId = eps,
        });
        empleado.StatusCode.Should().Be(HttpStatusCode.Created, $"empleado: «{await empleado.Content.ReadAsStringAsync()}»");
        return ((await NominaE2E.LeerAsync(empleado)).GetGuid(), documento);
    }
}
