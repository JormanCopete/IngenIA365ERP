using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Infrastructure;

namespace IngenIA365ERP.API.IntegrationTests.Middleware;

/// <summary>
/// Todo lo que responde bajo <c>/api</c> declara <c>Cache-Control: no-store</c>
/// (lo pone <c>SecurityHeadersMiddleware</c> al empezar la respuesta), salvo que
/// el endpoint haya fijado el suyo. Hasta el 2026-09-12 la API no declaraba
/// ninguno y se confiaba en que Cloudflare no cachea JSON «por defecto»; un
/// saldo o un listado servido desde una caché intermedia sería un dato de otra
/// persona en otra pantalla.
/// </summary>
[Collection(IdentidadCentralCollection.Nombre)]
public sealed class CacheControlBajoApiTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Una_respuesta_200_bajo_api_trae_no_store()
    {
        using var http = fx.CreateClient();

        // Anónimo y de sólo lectura: dos enteros de configuración. Ya lo usa
        // RenovacionDeSesionTests, así que si cambia de ruta se rompe allá también.
        var resp = await http.GetAsync("/api/auth/session-policy");

        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        resp.Headers.CacheControl.Should().NotBeNull("la API tiene que declarar su política de caché, no heredarla del borde");
        resp.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Un_error_bajo_api_tambien_trae_no_store()
    {
        using var http = fx.CreateClient();

        // Sin token → 401 con sobre de error. Un 401 cacheado dejaría fuera a
        // quien sí trae sesión.
        var resp = await http.GetAsync("/api/auth/me");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        resp.Headers.CacheControl.Should().NotBeNull();
        resp.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task El_endpoint_que_fija_la_suya_gana()
    {
        using var http = fx.CreateClient();

        // MapHealthChecks pone «no-store, no-cache» + Pragma + Expires por su
        // cuenta (AllowCachingResponses=false). El middleware no debe pisarlo ni
        // duplicarlo: si viera dos valores, el de acá se habría sumado al suyo.
        var resp = await http.GetAsync("/api/health");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.CacheControl.Should().NotBeNull();
        resp.Headers.CacheControl!.NoStore.Should().BeTrue();
        resp.Headers.CacheControl.NoCache.Should().BeTrue("es el valor del health check, no el del middleware");
        resp.Headers.Pragma.Should().ContainSingle(p => p.Name == "no-cache", "las otras cabeceras del health check siguen ahí");
    }
}
