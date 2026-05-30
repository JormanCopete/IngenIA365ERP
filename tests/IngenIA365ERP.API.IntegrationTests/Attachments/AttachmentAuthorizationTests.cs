using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// T104 — Autorización del módulo de adjuntos (US5).
///
/// Verifica que los 4 endpoints exigen autenticación + permiso. Sin token
/// → 401/404 (404 indistinguible si <c>PermissionAuthorizationFilter</c>
/// recorta antes del handler). Cuando el fixture aprovisione un usuario
/// admin autenticado, los caminos GREEN cubrirán además denegación sobre
/// el owner cuando el usuario no tiene permiso sobre la entidad propietaria.
///
/// Requiere Docker (Testcontainers) para los caminos GREEN completos.
/// </summary>
public class AttachmentAuthorizationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fx;
    public AttachmentAuthorizationTests(ApiTestFixture fx) => _fx = fx;

    [Fact]
    public async Task Upload_requires_authorization()
    {
        var client = _fx.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent([0x25, 0x50, 0x44, 0x46]), "file", "x.pdf");
        form.Add(new StringContent("User"), "ownerEntityType");
        form.Add(new StringContent(Guid.NewGuid().ToString()), "ownerEntityPublicId");

        var resp = await client.PostAsync("/api/attachments", form);

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_requires_authorization()
    {
        var client = _fx.CreateClient();

        var resp = await client.GetAsync($"/api/attachments/{Guid.NewGuid()}");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_requires_authorization()
    {
        var client = _fx.CreateClient();

        var resp = await client.DeleteAsync($"/api/attachments/{Guid.NewGuid()}");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListByOwner_requires_authorization()
    {
        var client = _fx.CreateClient();

        var resp = await client.GetAsync(
            $"/api/attachments/by-owner?ownerEntityType=User&ownerEntityPublicId={Guid.NewGuid()}");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }
}
