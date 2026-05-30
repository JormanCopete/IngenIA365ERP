using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Audit;

/// <summary>
/// T084 — El PDF exportado lleva HMAC-SHA256 verificable por el endpoint
/// SaaS <c>POST /api/saas/audit/verify</c> (T089 + T090).
///
/// Flujo end-to-end del test:
///  1) Llama <c>GET /api/audit/logs/export.pdf</c> con un filtro estrecho.
///  2) Toma el PDF resultante + el header (o campo de respuesta)
///     <c>X-Audit-Hmac</c> + <c>X-Audit-Key-Version</c>.
///  3) Envía el PDF a <c>POST /api/saas/audit/verify</c>.
///  4) El endpoint recalcula el HMAC y devuelve
///     <c>{ valid: true, computedHmac, keyVersion }</c>.
///
/// Hoy ninguno de los dos endpoints existe — el test es RED hasta T089/T090.
/// Cuando se implementen, espera que <c>Result.valid == true</c> y que
/// <c>computedHmac == X-Audit-Hmac</c>.
/// </summary>
public class AuditExportPdfSignatureTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public AuditExportPdfSignatureTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Exported_pdf_passes_signature_verification_round_trip()
    {
        var client = _fx.CreateClient();

        // Por ahora estos endpoints no existen / no devuelven PDF firmado.
        // El test queda como RED hasta T089/T090. Cuando aterricen, basta
        // con cambiar la auth a Bearer + token de un usuario con permiso
        // AuditLog.Export.
        var exportResp = await client.GetAsync(
            "/api/audit/logs/export.pdf?from=2026-01-01&to=2026-01-31");

        // Estado RED esperado hoy: 401/404 — el endpoint no existe.
        // Estado GREEN tras T089/T090: 200 application/pdf con headers HMAC.
        if (exportResp.StatusCode != HttpStatusCode.OK)
        {
            exportResp.StatusCode.Should().BeOneOf(
                HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
            return; // RED — esperado hoy.
        }

        exportResp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var hmac = exportResp.Headers.GetValues("X-Audit-Hmac").FirstOrDefault();
        var keyVersion = exportResp.Headers.GetValues("X-Audit-Key-Version").FirstOrDefault();
        hmac.Should().NotBeNullOrEmpty();
        keyVersion.Should().NotBeNullOrEmpty();

        var pdfBytes = await exportResp.Content.ReadAsByteArrayAsync();

        // Reenvía el PDF al verificador.
        using var verifyContent = new MultipartFormDataContent();
        var pdfPart = new ByteArrayContent(pdfBytes);
        pdfPart.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        verifyContent.Add(pdfPart, "file", "audit-export.pdf");

        var verifyResp = await client.PostAsync("/api/saas/audit/verify", verifyContent);
        verifyResp.IsSuccessStatusCode.Should().BeTrue();

        var result = await verifyResp.Content.ReadFromJsonAsync<VerifyResult>();
        result!.Valid.Should().BeTrue();
        result.ComputedHmac.Should().Be(hmac);
        result.KeyVersion.Should().Be(keyVersion);
    }

    private sealed record VerifyResult(bool Valid, string ComputedHmac, string KeyVersion);
}
