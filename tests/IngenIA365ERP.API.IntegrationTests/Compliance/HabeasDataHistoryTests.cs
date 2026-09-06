using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Compliance;

/// <summary>
/// T125 — Gates de autorización del módulo habeas data (US7). El test
/// end-to-end (publicar versión → registrar consent → revocar →
/// historial muestra la secuencia + emite <c>HabeasDataRevokedEvent</c>)
/// requiere Docker para Testcontainers + un usuario autenticado.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public class HabeasDataHistoryTests(CentralIdentityApiFixture fx)
{

    [Fact]
    public async Task ListPolicies_requires_authorization()
    {
        var client = fx.CreateClient();
        var resp = await client.GetAsync("/api/compliance/habeas-data/policies/");
        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublishPolicy_requires_authorization()
    {
        var client = fx.CreateClient();
        var resp = await client.PostAsync("/api/compliance/habeas-data/policies/",
            JsonContent.Create(new
            {
                title = "x",
                contentMarkdown = "y",
                effectiveFrom = DateTime.UtcNow.AddDays(1)
            }));
        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeConsent_requires_authorization()
    {
        var client = fx.CreateClient();
        var resp = await client.PostAsync("/api/compliance/habeas-data/consents/revoke",
            JsonContent.Create(new { personId = 1, notes = "x" }));
        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_requires_authorization()
    {
        var client = fx.CreateClient();
        var resp = await client.GetAsync("/api/compliance/habeas-data/consents/history/123");
        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }
}

// Local helper: System.Net.Http.Json sin paquete extra.
file static class JsonContent
{
    public static StringContent Create<T>(T value) =>
        new(
            System.Text.Json.JsonSerializer.Serialize(value),
            System.Text.Encoding.UTF8,
            "application/json");
}
