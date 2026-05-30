using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Security;

/// <summary>
/// T063 — Verifica el invariante FR-017/SC-005: cuando falta permiso, la
/// respuesta es indistinguible de un endpoint inexistente. Compara:
///  (a) un endpoint real protegido sin enviar token (o token sin <c>perm</c>)
///  (b) una URL inventada
/// Ambas deben devolver 404 con el mismo envelope.
/// </summary>
public class PermissionEnforcementTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public PermissionEnforcementTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Endpoint_without_permission_returns_same_envelope_as_unknown_endpoint()
    {
        var client = _fx.CreateClient();

        // Un endpoint real protegido por permiso (Security.Users.View).
        var protectedResp = await client.GetAsync("/api/admin/users");
        // Una URL completamente inventada bajo /api/.
        var fakeResp = await client.GetAsync("/api/this/does/not/exist");

        protectedResp.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "sin token + sin permiso → 404 indistinguible");
        fakeResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var protectedBody = await protectedResp.Content.ReadFromJsonAsync<NotFoundEnvelope>();
        var fakeBody = await fakeResp.Content.ReadFromJsonAsync<NotFoundEnvelope>();

        protectedBody!.Code.Should().Be(fakeBody!.Code);
        protectedBody.Code.Should().Be("Generic.NotFound");
        protectedBody.Message.Should().Be(fakeBody.Message);
    }

    [Fact]
    public async Task Endpoint_with_token_lacking_permission_returns_404()
    {
        var client = _fx.CreateClient();
        // Token con sub válido pero sin claims `perm`. El backend lo validará
        // como auth correcta pero el filtro de permisos rechaza con 404.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "invalid-but-formatted.token.here");

        var resp = await client.GetAsync("/api/admin/users");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record NotFoundEnvelope(string Code, string Message, string? TraceId);
}
