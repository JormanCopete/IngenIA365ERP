using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// T104 — Autorización del módulo de adjuntos (US5).
///
/// Verifica que los endpoints exigen autenticación + permiso. Sin token
/// → 401/404 (404 indistinguible si <c>PermissionAuthorizationFilter</c>
/// recorta antes del handler). Los caminos con sesión, sobre adjuntos del
/// formato anterior, están en <see cref="AdjuntosDelFormatoAnteriorTests"/>.
///
/// Requiere Docker (Testcontainers).
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public class AttachmentAuthorizationTests(CentralIdentityApiFixture fx)
{
    /// <summary>
    /// Feature 011: la subida multipart a través de la API se retiró sin alias el 2026-09-23. Se mira
    /// la tabla de rutas y no la respuesta, porque un 404 también es lo que contesta una ruta que existe
    /// cuando falta el permiso: la respuesta no distingue «no existe» de «no te dejo».
    /// </summary>
    [Fact]
    public void La_subida_a_traves_de_la_API_ya_no_existe()
    {
        var rutas = fx.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        rutas.Where(r => string.Equals(r.RoutePattern.RawText?.Trim('/'), "api/attachments", StringComparison.OrdinalIgnoreCase))
            .Where(r => r.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Post) ?? false)
            .Should().BeEmpty("las personas suben directo al almacén con una autorización firmada (contracts/api.md §1)");
        rutas.Should().Contain(r => r.RoutePattern.RawText == "/api/attachments/by-owner",
            "si no, la prueba pasaría con una tabla de rutas vacía");
    }

    /// <summary>Feature 011 (R13): toda ruta de adjuntos pasa por el limitador de concurrencia.</summary>
    [Fact]
    public void Todas_las_rutas_de_adjuntos_llevan_el_limitador()
    {
        var rutas = fx.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(r => r.RoutePattern.RawText?.StartsWith("/api/attachments", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        rutas.Should().NotBeEmpty();
        rutas.Where(r => r.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName
                         != IngenIA365ERP.API.Endpoints.Attachments.LimiteDeAdjuntos.Politica)
            .Select(r => r.RoutePattern.RawText)
            .Should().BeEmpty("sin el limitador, veinte descargas del formato anterior son 1,5 GB en un pod de 1 GiB");
    }

    [Fact]
    public async Task Download_requires_authorization()
    {
        var client = fx.CreateClient();

        var resp = await client.GetAsync($"/api/attachments/{Guid.NewGuid()}");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_requires_authorization()
    {
        var client = fx.CreateClient();

        var resp = await client.DeleteAsync($"/api/attachments/{Guid.NewGuid()}");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListByOwner_requires_authorization()
    {
        var client = fx.CreateClient();

        var resp = await client.GetAsync(
            $"/api/attachments/by-owner?ownerEntityType=User&ownerEntityPublicId={Guid.NewGuid()}");

        resp.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }
}
