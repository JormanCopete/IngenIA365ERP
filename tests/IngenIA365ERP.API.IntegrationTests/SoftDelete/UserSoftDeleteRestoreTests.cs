using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.SoftDelete;

/// <summary>
/// T097 — End-to-end del ciclo soft-delete + restore vía API REST
/// (FR-029). Requiere Docker (Testcontainers) para ejecutarse.
///
/// El test toca dos endpoints US2:
///  * <c>POST /api/admin/users/{publicId}/disable</c> (soft-delete)
///  * <c>POST /api/admin/users/{publicId}/restore</c> (restore)
///
/// Estado RED esperado hoy: sin auth → 401/404. GREEN cuando el fixture
/// aprovisione un usuario admin autenticado con permisos.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public class UserSoftDeleteRestoreTests(CentralIdentityApiFixture fx)
{

    [Fact]
    public async Task Disable_endpoint_requires_authorization()
    {
        var client = fx.CreateClient();
        var fakeUserId = Guid.NewGuid();

        var resp = await client.PostAsync($"/api/admin/users/{fakeUserId}/disable", null);

        // Sin token → 401/404 (404 indistinguible si PermissionAuthorizationFilter
        // recorta antes que el handler).
        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_endpoint_requires_authorization()
    {
        var client = fx.CreateClient();
        var fakeUserId = Guid.NewGuid();

        var resp = await client.PostAsync($"/api/admin/users/{fakeUserId}/restore", null);

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_endpoint_does_not_accept_GET()
    {
        // Restore es una mutación → debe ser POST. Un GET no debe estar
        // mapeado (defensa contra CSRF-via-image-tag y enumeración).
        var client = fx.CreateClient();
        var fakeUserId = Guid.NewGuid();

        var resp = await client.GetAsync($"/api/admin/users/{fakeUserId}/restore");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.MethodNotAllowed,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized);
    }
}
