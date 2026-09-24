using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests;

/// <summary>
/// El endpoint de salud del API. Hasta el 2026-09-23 levantaba un <c>WebApplicationFactory</c> pelado:
/// sin contenedores, con la configuración local de cada máquina —y su base de desarrollo real— y, en un
/// clon limpio, sin arrancar siquiera (falta WebAuthn, falta el maestro). Ahora usa la fixture compartida.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public class HealthEndpointTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnOk()
    {
        var response = await fx.CreateClient().GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthyText()
    {
        var response = await fx.CreateClient().GetAsync("/api/health");
        var content = await response.Content.ReadAsStringAsync();

        // MapHealthChecks returns "Healthy" as plain text by default
        content.Should().Be("Healthy");
    }
}
