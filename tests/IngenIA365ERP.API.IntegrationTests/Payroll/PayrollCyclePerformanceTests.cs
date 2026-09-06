using System.Diagnostics;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// T132 / SC-004 — el ciclo completo (calcular + aprobar con contabilización) de 200 empleados
/// con 8 novedades cada uno tarda menos de 60 s sobre la base real.
///
/// <para>
/// Sólo corre con <c>RUN_PERF_TESTS=1</c>, como las demás de rendimiento de la suite: monta
/// 200 personas, 200 empleados y 1 600 novedades por HTTP antes de medir, y eso no es algo
/// que deba pagar cada corrida de CI. Sin la variable hace <c>return</c> y se cuenta como
/// pasada sin medir (xUnit 2 no omite en tiempo de ejecución): está en la lista de CLAUDE.md.
/// </para>
/// </summary>
[Collection(NominaCollection.Nombre)]
public class PayrollCyclePerformanceTests(CentralIdentityApiFixture fx)
{
    private const int Empleados = 200;

    [Fact]
    public async Task Doscientos_empleados_con_ocho_novedades_se_calculan_y_aprueban_en_menos_de_un_minuto()
    {
        // Mismo gate que AuditPerformanceTests (xUnit 2 no sabe omitir en tiempo de ejecución):
        // sin la variable se cuenta como pasada sin haber medido. CLAUDE.md lo lista entre las
        // pruebas que el verde tapa.
        if (Environment.GetEnvironmentVariable("RUN_PERF_TESTS") != "1") return;

        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var periodoId = await NominaE2E.CrearPeriodoAsync(http, admin, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
        var empleados = new List<Guid>(Empleados);
        var csv = new System.Text.StringBuilder("documento;concepto;cantidad;valor;desde;hasta;observacion\n");
        for (var i = 0; i < Empleados; i++)
        {
            var (id, documento) = await NominaE2E.CrearEmpleadoAsync(http, admin, $"Perf{i}", 1_600_000m + (i % 40) * 200_000m, new DateTime(2020, 1, 15), conCorreo: false);
            empleados.Add(id);
            // Las 1 600 novedades entran por importación, como haría una cooperativa con ese
            // volumen (y sin tropezar con el límite de 1 000 peticiones por minuto de la API).
            csv.Append(documento).Append(";HEX_DIURNA;").Append(4 + i % 6).Append(";;;;\n");
            csv.Append(documento).Append(";HEX_NOCTURNA;").Append(2 + i % 4).Append(";;;;\n");
            csv.Append(documento).Append(";RECARGO_NOCTURNO;8;;;;\n");
            csv.Append(documento).Append(";COMISION;;").Append(150_000 + (i % 7) * 50_000).Append(";;;\n");
            csv.Append(documento).Append(";BONIF_NO_SALARIAL;;100000;;;\n");
            csv.Append(documento).Append(";LIBRANZA;;80000;;;\n");
            csv.Append(documento).Append(";INCAP_GENERAL;;;2026-10-10;2026-10-12;\n");
            csv.Append(documento).Append(";LIC_REMUNERADA;;;2026-10-20;2026-10-21;\n");
        }

        using var form = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(new System.Text.UTF8Encoding(true).GetBytes(csv.ToString()));
        archivo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(archivo, "file", "novedades-perf.csv");
        using var subida = new HttpRequestMessage(HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/novelties/import") { Content = form };
        subida.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin);
        var importacion = await http.SendAsync(subida);
        importacion.IsSuccessStatusCode.Should().BeTrue($"importar: «{await importacion.Content.ReadAsStringAsync()}»");
        (await NominaE2E.LeerAsync(importacion)).GetProperty("applied").GetInt32().Should().Be(Empleados * 8);

        var reloj = Stopwatch.StartNew();
        var calculo = await NominaE2E.CalcularAsync(http, admin, periodoId);
        var calculoEn = reloj.Elapsed;
        calculo.GetProperty("employeeCount").GetInt32().Should().Be(Empleados);

        var runId = calculo.GetProperty("runPublicId").GetGuid();
        var aprobacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/approve", new
        {
            confirm = true,
            exceptions = empleados.Select(e => new { employeePublicId = e, flag = "MissingAffiliation", reason = "Empleado sintético" }).ToArray(),
            confirmEmpty = false,
            confirmWithoutSegregation = true,
        });
        reloj.Stop();
        aprobacion.IsSuccessStatusCode.Should().BeTrue($"aprobar: «{await aprobacion.Content.ReadAsStringAsync()}»");

        reloj.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(60),
            $"calcular tardó {calculoEn.TotalSeconds:0.0} s y el ciclo completo {reloj.Elapsed.TotalSeconds:0.0} s");
    }
}
