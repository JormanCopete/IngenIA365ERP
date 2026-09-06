using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Payroll;

/// <summary>
/// T115 — US5 y US7 por HTTP: aprobar → relación de pago → marcar pago → PDF con
/// <c>application/pdf</c> → envío por correo → el pago bloquea la reversión → retirar la
/// marca → reversar y volver a calcular.
/// </summary>
[Collection(NominaCollection.Nombre)]
public class PaymentsAndPayslipsEndpointsTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Relacion_de_pago_marca_comprobante_pdf_envio_y_reversion()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var (periodoId, empleadoId, runId, _) = await NominaE2E.CicloAprobadoAsync(http, admin, 7, "Julia");

        // --- relación de pago (el período trae a todos los empleados vigentes del plan) ---
        var relacion = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payments");
        var empleadosEnCorrida = relacion.GetProperty("employees").GetInt32();
        empleadosEnCorrida.Should().BeGreaterThanOrEqualTo(1);
        relacion.GetProperty("paidCount").GetInt32().Should().Be(0);
        var fila = relacion.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeePublicId").GetGuid() == empleadoId);
        fila.GetProperty("paid").GetBoolean().Should().BeFalse();
        fila.GetProperty("netPay").GetDecimal().Should().BeGreaterThan(0m);

        // --- marcar pagados (todos) ---
        var marca = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/payments", new
        {
            employeePublicIds = (Guid[]?)null, paidAt = "2026-07-30T00:00:00", method = "Transfer", reference = "LOTE-7",
        });
        marca.StatusCode.Should().Be(HttpStatusCode.OK, $"marcar: «{await marca.Content.ReadAsStringAsync()}»");
        (await NominaE2E.LeerAsync(marca)).GetInt32().Should().Be(empleadosEnCorrida);

        relacion = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payments");
        relacion.GetProperty("paidCount").GetInt32().Should().Be(empleadosEnCorrida);
        fila = relacion.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("employeePublicId").GetGuid() == empleadoId);
        fila.GetProperty("paymentMethod").GetString().Should().Be("Transfer");
        fila.GetProperty("reference").GetString().Should().Be("LOTE-7");

        var repetida = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/payments", new
        {
            employeePublicIds = new[] { empleadoId }, paidAt = "2026-07-31T00:00:00", method = "Cash", reference = (string?)null,
        });
        repetida.IsSuccessStatusCode.Should().BeFalse();
        (await NominaE2E.CodigoDeErrorAsync(repetida)).Should().Be("Payroll.PaymentAlreadyMarked");

        // --- comprobante en PDF, uno y todos ---
        var pdf = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/payroll/runs/{runId}/payslips/{empleadoId}/pdf", null);
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, $"pdf: «{await pdf.Content.ReadAsStringAsync()}»");
        pdf.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        var todos = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/payroll/runs/{runId}/payslips/pdf", null);
        todos.StatusCode.Should().Be(HttpStatusCode.OK);
        todos.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        // --- envío por correo: manual, uno a uno, con el PDF adjunto; la fixture declara Smtp y captura el transporte ---
        var enviadosAntes = fx.Emails.Sent.Count;
        var envio = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/payslips/send", new { employeePublicIds = new[] { empleadoId } });
        envio.StatusCode.Should().Be(HttpStatusCode.OK, $"enviar: «{await envio.Content.ReadAsStringAsync()}»");
        var resultado = await NominaE2E.LeerAsync(envio);
        resultado.GetProperty("sent").GetInt32().Should().Be(1);
        resultado.GetProperty("failed").GetInt32().Should().Be(0);
        var correo = fx.Emails.Sent.Skip(enviadosAntes).Single(m => m.Subject.Contains("Comprobante de pago"));
        correo.Attachments.Should().ContainSingle(a => a.ContentType == "application/pdf" && a.Content.Length > 1000);
        correo.BodyHtml.Should().Contain("Julia Prueba").And.NotContain("{{", "la plantilla se rellenó completa");
        var entregas = await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payslips/deliveries");
        entregas.GetArrayLength().Should().Be(1);
        entregas[0].GetProperty("status").GetString().Should().Be("Sent");

        // --- el pago vigente bloquea la reversión; retiradas las marcas, se reversa y el período vuelve a abrirse ---
        var bloqueada = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/reverse", new { reason = "Faltó una novedad" });
        bloqueada.IsSuccessStatusCode.Should().BeFalse();
        (await NominaE2E.CodigoDeErrorAsync(bloqueada)).Should().Be("Payroll.PaymentBlocksReversal");

        foreach (var pagado in relacion.GetProperty("rows").EnumerateArray().Select(r => r.GetProperty("employeePublicId").GetGuid()))
        {
            var retiro = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/payments/{pagado}/revert", new { reason = "Transferencia devuelta" });
            retiro.IsSuccessStatusCode.Should().BeTrue($"retirar marca: «{await retiro.Content.ReadAsStringAsync()}»");
        }
        (await NominaE2E.GetAsync(http, admin, $"/api/payroll/runs/{runId}/payments")).GetProperty("paidCount").GetInt32().Should().Be(0);

        var reversion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, $"/api/payroll/runs/{runId}/reverse", new { reason = "Faltó una novedad" });
        reversion.StatusCode.Should().Be(HttpStatusCode.OK, $"reversar: «{await reversion.Content.ReadAsStringAsync()}»");
        (await NominaE2E.LeerAsync(reversion)).GetProperty("reversalAccountingDocumentNumber").GetString().Should().StartWith("NM-");

        var actual = await NominaE2E.GetAsync(http, admin, $"/api/payroll/pay-periods/{periodoId}/runs/current");
        actual.GetProperty("status").GetString().Should().Be("Reversed");
        actual.GetProperty("reversalReason").GetString().Should().Be("Faltó una novedad");

        // El comprobante del período reversado sigue disponible (la corrida no se borró) y el período se recalcula.
        (await NominaE2E.EnviarAsync(http, admin, HttpMethod.Get, $"/api/payroll/runs/{runId}/payslips/{empleadoId}/pdf", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var v2 = await NominaE2E.CalcularAsync(http, admin, periodoId);
        v2.GetProperty("version").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Sin_permiso_de_marcar_pagos_la_ruta_no_existe_pero_la_relacion_se_ve()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var (_, empleadoId, runId, _) = await NominaE2E.CicloAprobadoAsync(http, ctx.TokenAdmin, 8, "Karen");

        var lectura = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Get, $"/api/payroll/runs/{runId}/payments", null);
        lectura.StatusCode.Should().Be(HttpStatusCode.OK, "sólo lectura tiene Payroll.Payments.View");

        var marca = await NominaE2E.EnviarAsync(http, ctx.TokenSoloLectura, HttpMethod.Post, $"/api/payroll/runs/{runId}/payments", new
        {
            employeePublicIds = new[] { empleadoId }, paidAt = "2026-08-30T00:00:00", method = "Transfer", reference = (string?)null,
        });
        marca.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
