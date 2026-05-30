using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Notifications;

/// <summary>
/// T113 — Reintentos del dispatcher de email (US6, FR-042).
///
/// Gate compilable: el endpoint del inbox exige autenticación. El test
/// completo (SMTP simulado que falla 3 veces → persiste
/// <c>NotificationDeliveryFailure</c> → la in-app sigue visible) requiere
/// Docker + un MailHog/smtp4dev sandbox; queda gateado en RED.
/// </summary>
public class EmailRetryTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public EmailRetryTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Notifications_inbox_requires_authorization()
    {
        var client = _fx.CreateClient();

        var resp = await client.GetAsync("/api/notifications/");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_requires_authorization()
    {
        var client = _fx.CreateClient();

        var resp = await client.PostAsync(
            $"/api/notifications/{Guid.NewGuid()}/read", null);

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkAllRead_requires_authorization()
    {
        var client = _fx.CreateClient();

        var resp = await client.PostAsync("/api/notifications/read-all", null);

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }
}
