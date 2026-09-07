using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>T067 — US1 por HTTP: crear período, registrar, corregir, anular y listar novedades; sin permiso, 404 indistinguible.</summary>
[Collection(NominaCollection.Nombre)]
public class NoveltiesEndpointsTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Registrar_corregir_anular_y_listar_novedades_del_periodo()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var (empleadoId, _) = await NominaE2E.CrearEmpleadoAsync(http, admin, "Ana", 2_000_000m, new DateTime(2025, 1, 15));
        var periodoId = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));

        // Registrar: horas extra y una incapacidad que cruza el fin del período.
        var horasId = await NominaE2E.RegistrarNovedadAsync(http, admin, periodoId, new { employeePublicId = empleadoId, conceptCode = "HEX_NOCTURNA", quantity = 6 });
        var incapacidadId = await NominaE2E.RegistrarNovedadAsync(http, admin, periodoId, new
        {
            employeePublicId = empleadoId, conceptCode = "INCAP_GENERAL", startDate = "2026-03-28", endDate = "2026-04-03", notes = "Incapacidad general",
        });

        var lista = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/novelties");
        lista.GetArrayLength().Should().Be(2);
        var horas = lista.EnumerateArray().Single(n => n.GetProperty("publicId").GetGuid() == horasId);
        horas.GetProperty("estimatedAmount").GetDecimal().Should().BeGreaterThan(0m, "la lista trae el valor previsto con el salario vigente");
        horas.GetProperty("status").GetString().Should().Be("Active");
        var incapacidad = lista.EnumerateArray().Single(n => n.GetProperty("publicId").GetGuid() == incapacidadId);
        incapacidad.GetProperty("daysInPeriod").GetInt32().Should().BeGreaterThan(0);
        incapacidad.GetProperty("carryOverDays").GetInt32().Should().Be(3, "1..3 de abril se trasladan al período siguiente");

        // Corregir: crea una versión nueva; la anterior queda reemplazada y en el historial.
        var correccion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Put, $"/api/payroll/novelties/{horasId}", new { quantity = 8, reason = "Eran ocho horas, no seis" });
        correccion.StatusCode.Should().Be(HttpStatusCode.OK, $"corregir: «{await correccion.Content.ReadAsStringAsync()}»");
        var horasV2 = (await NominaE2E.LeerAsync(correccion)).GetProperty("publicId").GetGuid();
        horasV2.Should().NotBe(horasId);

        var historial = await NominaE2E.GetAsync(http, admin, $"/api/payroll/novelties/{horasV2}/history");
        historial.GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
        historial.EnumerateArray().Select(h => h.GetProperty("status").GetString()).Should().Contain("Superseded").And.Contain("Active");

        // Anular con motivo.
        var anulacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/novelties/{incapacidadId}/cancel", new { reason = "Se registró al empleado equivocado" });
        anulacion.IsSuccessStatusCode.Should().BeTrue($"anular: «{await anulacion.Content.ReadAsStringAsync()}»");

        var activas = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/novelties?status=Active");
        activas.GetArrayLength().Should().Be(1);
        activas[0].GetProperty("publicId").GetGuid().Should().Be(horasV2);
        activas[0].GetProperty("quantity").GetDecimal().Should().Be(8m);

        var todas = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/novelties");
        todas.EnumerateArray().Select(n => n.GetProperty("status").GetString()).Should().BeEquivalentTo(["Active", "Superseded", "Cancelled"]);
    }

    [Fact]
    public async Task Sin_permiso_de_crear_la_ruta_no_existe()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();

        var (empleadoId, _) = await NominaE2E.CrearEmpleadoAsync(http, ctx.TokenAdmin, "Beto", 2_000_000m, new DateTime(2025, 1, 15));
        var periodoId = await NominaE2E.CrearPeriodoAsync(http, ctx.TokenAdmin, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28));

        // Sólo lectura ve la lista (Payroll.Novelties.View) pero para él la ruta de registro no existe.
        var lectura = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, $"/api/payroll/pay-periods/{periodoId}/novelties", null);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK);

        var registro = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/novelties",
            new { employeePublicId = empleadoId, conceptCode = "HEX_NOCTURNA", quantity = 6 });
        registro.StatusCode.Should().Be(HttpStatusCode.NotFound, "sin el permiso la ruta es indistinguible de una inexistente");

        var lista = await NominaE2E.GetAsync(http, ctx.TokenAdmin, $"/api/payroll/pay-periods/{periodoId}/novelties");
        lista.GetArrayLength().Should().Be(0, "el 404 no dejó nada registrado");
    }
}
