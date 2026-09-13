using System.Net;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// Feature 006 US5: las cinco vistas del centro de reportes responden en JSON con los mismos
/// totales de la corrida y se exportan a Excel, PDF y Word con contenido real. Diciembre
/// es el mes libre de esta colección (enero a noviembre están
/// tomados por otras pruebas).
/// </summary>
[Collection(NominaCollection.Nombre)]
public class ReportesNominaTests(CentralIdentityApiFixture fx)
{
    private static readonly (string Formato, string TipoContenido, byte[] Firma)[] Formatos =
    [
        ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [0x50, 0x4B]),
        ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", [0x50, 0x4B]),
        ("pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]),
    ];

    [Fact]
    public async Task Las_cinco_vistas_responden_con_los_totales_de_la_corrida_y_exportan_a_tres_formatos()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;
        var (periodoId, empleadoId, runId, documento) = await NominaE2E.CicloAprobadoAsync(http, admin, 12, "Reporte");
        var corrida = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}");
        var neto = corrida.GetProperty("totals").GetProperty("net").GetDecimal();
        var empleados = corrida.GetProperty("employeeCount").GetInt32();

        var rutas = new Dictionary<string, string>
        {
            ["comprobante"] = $"comprobante?runId={runId}&employeeId={empleadoId}",
            ["resumen"] = $"resumen?runId={runId}",
            ["detalle"] = $"detalle?runId={runId}",
            ["novedades"] = $"novedades?periodId={periodoId}",
            ["historico"] = $"historico?employeeId={empleadoId}&desde=2026-01-01&hasta=2026-12-31",
        };

        foreach (var (vista, ruta) in rutas)
        {
            var tabla = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/{ruta}");
            tabla.GetProperty("titulo").GetString().Should().NotBeNullOrWhiteSpace(vista);
            tabla.GetProperty("columnas").GetArrayLength().Should().BeGreaterThan(0, vista);
            // El tipo de columna viaja por nombre: la pantalla lo deserializa como string y con un
            // número caía con DeserializeUnableToConvertValue en $.columnas[0].tipo.
            tabla.GetProperty("columnas")[0].GetProperty("tipo").ValueKind.Should().Be(System.Text.Json.JsonValueKind.String, vista);
            tabla.GetProperty("filas").GetArrayLength().Should().BeGreaterThan(0, vista);

            foreach (var (formato, tipo, firma) in Formatos)
            {
                var resp = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/payroll/{ruta}&format={formato}", null);
                resp.StatusCode.Should().Be(HttpStatusCode.OK, $"{vista} {formato}: «{await resp.Content.ReadAsStringAsync()}»");
                resp.Content.Headers.ContentType!.MediaType.Should().Be(tipo);
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                bytes.Length.Should().BeGreaterThan(500, $"{vista} {formato} tiene contenido");
                bytes.Take(firma.Length).Should().Equal(firma, $"{vista} {formato} es un archivo del tipo declarado");
            }
        }

        // Los totales del reporte son los de la corrida: resumen (neto y empleados) y detalle (neto sumado).
        var resumen = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/{rutas["resumen"]}");
        var totalesResumen = resumen.GetProperty("totales").GetProperty("valores");
        totalesResumen[2].GetInt32().Should().Be(empleados);
        totalesResumen[3].GetDecimal().Should().Be(neto);

        var detalle = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/{rutas["detalle"]}");
        var columnas = detalle.GetProperty("columnas").EnumerateArray().Select(c => c.GetProperty("nombre").GetString()).ToList();
        var indiceNeto = columnas.IndexOf("Neto");
        detalle.GetProperty("totales").GetProperty("valores")[indiceNeto].GetDecimal().Should().Be(neto);

        var comprobante = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/{rutas["comprobante"]}");
        comprobante.GetProperty("titulo").GetString().Should().Contain(documento);
        comprobante.GetProperty("filas").EnumerateArray().Select(f => f.GetProperty("seccion").GetString()).Distinct()
            .Should().Contain("Devengos").And.Contain("Deducciones");

        var historico = await NominaE2E.GetAsync(http, admin, $"/api/reports/payroll/{rutas["historico"]}");
        historico.GetProperty("filas").GetArrayLength().Should().BeGreaterThanOrEqualTo(1, "al menos la corrida aprobada de diciembre");

        var formatoMalo = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/reports/payroll/{rutas["resumen"]}&format=csv", null);
        formatoMalo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
