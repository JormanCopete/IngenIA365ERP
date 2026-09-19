using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// QA, 2026-09-18: «Nuevo concepto» en /nomina/conceptos respondía «Error HTTP 400». La pantalla
/// manda los enums por nombre («Earning», «PercentOfBase») y la API sólo aceptaba números, así
/// que el binding del cuerpo fallaba antes del handler y sin sobre de error. Lo mismo le pasaba
/// a crear un plan de nómina por la pantalla. Esta prueba manda exactamente lo que manda la
/// pantalla; las demás pruebas mandan números y siguen valiendo.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class ConceptoPorNombreTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Un_concepto_y_un_plan_mandados_por_nombre_como_la_pantalla_se_crean()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var concepto = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/concept-definitions", new
        {
            code = "BONO_QA", name = "Bono de prueba", nature = "Earning", calculationKind = "PercentOfBase",
            baseKind = "BasicSalary", percent = 10m, prorateByDays = false, affectsSalaryBase = true,
            applicableClasses = 0, validFrom = new DateTime(2026, 1, 1),
        });
        concepto.StatusCode.Should().Be(HttpStatusCode.Created, await concepto.Content.ReadAsStringAsync());
        var versiones = (await NominaE2E.GetAsync(http, admin, "/api/payroll/concept-definitions/BONO_QA/versions")).EnumerateArray().ToList();
        versiones.Should().ContainSingle().Which.GetProperty("calculationKind").GetString().Should().Be("PercentOfBase");

        var plan = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "QUINCENA_QA", name = "Quincenal", periodicity = "Biweekly" });
        plan.StatusCode.Should().Be(HttpStatusCode.Created, await plan.Content.ReadAsStringAsync());

        // Un nombre inexistente sigue siendo un 400 (el binding no llega al handler), no un 500.
        var malo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/payroll/plans", new { code = "X", name = "X", periodicity = "Lunar" });
        malo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
