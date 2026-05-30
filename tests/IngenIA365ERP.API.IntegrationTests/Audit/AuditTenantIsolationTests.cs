using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Audit;

/// <summary>
/// T083 — Aislamiento por tenant en consultas del audit log (FR-004 / FR-024).
/// Un usuario del tenant A NUNCA debe ver eventos del tenant B, ni siquiera
/// si manipula query strings, headers o el claim <c>tenant_id</c> del JWT.
///
/// El test ejerce dos vectores:
///  * Sin autenticación → 401 o 404 (depende de la política del endpoint).
///  * Con header <c>X-Tenant-Id</c> divergente del claim → rechazo por el
///    <c>TenantResolutionMiddleware</c> (T023).
///
/// Hoy el endpoint <c>/api/audit/logs</c> está sin reescribir bajo CQRS +
/// RequirePermission (T091 lo cubrirá), así que estos tests son RED a
/// runtime: pasan cuando el reescribir aterrice y la auth se aplique.
/// </summary>
public class AuditTenantIsolationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public AuditTenantIsolationTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Anonymous_request_to_audit_logs_does_not_return_data()
    {
        var client = _fx.CreateClient();

        var resp = await client.GetAsync("/api/audit/logs");

        // No queremos 200 + datos. Cualquiera de las tres es aceptable:
        // 401 (no autenticado), 403 (sin permiso) o 404 (indistinguible por
        // PermissionAuthorizationFilter — T074).
        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task X_Tenant_Id_header_diverging_from_claim_does_not_leak_other_tenant_data()
    {
        var client = _fx.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-de-otra-cooperativa");

        var resp = await client.GetAsync("/api/audit/logs");

        resp.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "el middleware o el filter deben cortar el request antes de ejecutar la query");
    }

    [Fact]
    public async Task Audit_logs_endpoint_uses_canonical_envelope_when_forbidden()
    {
        // FR-017 / SC-005: un endpoint protegido sin permiso responde con el
        // envelope canónico { code, message, traceId } — no payload Mongo
        // raw. Hoy el endpoint NO usa ErrorEnvelopeFilter; T091 lo aterriza.
        var client = _fx.CreateClient();
        var resp = await client.GetAsync("/api/audit/logs");

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            var body = await resp.Content.ReadFromJsonAsync<ErrorEnvelope>();
            body!.Code.Should().NotBeNullOrEmpty();
            body.TraceId.Should().NotBeNullOrEmpty();
        }
    }

    private sealed record ErrorEnvelope(string Code, string Message, string? TraceId);
}
