using FluentAssertions;
using IngenIA365ERP.API.HealthChecks;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.API.IntegrationTests.Attachments;

/// <summary>
/// La sonda del almacén de adjuntos (2026-09-22). Lo que se fija acá no es que el chequeo funcione
/// —eso es media línea— sino <b>cada cuánto escribe</b>: la <c>readinessProbe</c> pega cada 5 s por
/// pod, y el día que salió a los ambientes el bucket juntó 18 versiones en dos minutos. Sin esta
/// prueba, alguien vuelve a escribir en cada sonda sin enterarse de que acaba de multiplicar por
/// sesenta las peticiones a S3 y las versiones no vigentes del bucket.
/// </summary>
public class BlobStoreHealthCheckTests
{
    private static readonly HealthCheckContext Contexto = new()
    {
        Registration = new HealthCheckRegistration("blobstore", _ => null!, null, null),
    };

    /// <summary>Un reloj que solo avanza cuando la prueba lo dice. Sin paquete extra: es una propiedad y un metodo.</summary>
    private sealed class RelojFalso : TimeProvider
    {
        private DateTimeOffset _ahora = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _ahora;
        public void Advance(TimeSpan cuanto) => _ahora += cuanto;
    }

    private static (BlobStoreHealthCheck Sonda, IBlobStore Almacen, RelojFalso Reloj) Crear(string donde = "s3://bucket/pdn")
    {
        var almacen = Substitute.For<IBlobStore>();
        almacen.ProbarAsync(Arg.Any<CancellationToken>()).Returns(donde);
        var reloj = new RelojFalso();
        var opciones = Options.Create(new AttachmentStorageSettings { Provider = AttachmentStorageSettings.ProveedorS3 });
        return (new BlobStoreHealthCheck(opciones, almacen, reloj), almacen, reloj);
    }

    [Fact]
    public async Task El_primer_arranque_siempre_escribe_de_verdad()
    {
        var (sonda, almacen, _) = Crear();

        var resultado = await sonda.CheckHealthAsync(Contexto);

        resultado.Status.Should().Be(HealthStatus.Healthy);
        resultado.Description.Should().Be("S3: s3://bucket/pdn");
        await almacen.Received(1).ProbarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Las_sondas_seguidas_repiten_el_veredicto_sin_volver_a_escribir()
    {
        var (sonda, almacen, reloj) = Crear();
        await sonda.CheckHealthAsync(Contexto);

        // Cuatro minutos de sondas cada 5 s: 48 llamadas, una sola escritura.
        for (var i = 0; i < 48; i++)
        {
            reloj.Advance(TimeSpan.FromSeconds(5));
            (await sonda.CheckHealthAsync(Contexto)).Status.Should().Be(HealthStatus.Healthy);
        }

        await almacen.Received(1).ProbarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pasado_el_intervalo_vuelve_a_escribir()
    {
        var (sonda, almacen, reloj) = Crear();
        await sonda.CheckHealthAsync(Contexto);

        reloj.Advance(TimeSpan.FromMinutes(5));
        await sonda.CheckHealthAsync(Contexto);

        await almacen.Received(2).ProbarAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_almacen_que_no_responde_deja_el_pod_fuera_de_Ready()
    {
        var (sonda, almacen, _) = Crear();
        almacen.ProbarAsync(Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new InvalidOperationException("credencial sin permiso"));

        var resultado = await sonda.CheckHealthAsync(Contexto);

        resultado.Status.Should().Be(HealthStatus.Unhealthy);
        resultado.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Un_fallo_tambien_se_recuerda_el_intervalo_completo()
    {
        var (sonda, almacen, reloj) = Crear();
        almacen.ProbarAsync(Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new InvalidOperationException("bucket caido"));
        await sonda.CheckHealthAsync(Contexto);

        // Con el bucket caído, reintentar en cada sonda sería el mismo desperdicio al revés:
        // cada réplica martillando S3 cada 5 s mientras dure la caída.
        reloj.Advance(TimeSpan.FromMinutes(1));
        (await sonda.CheckHealthAsync(Contexto)).Status.Should().Be(HealthStatus.Unhealthy);

        await almacen.Received(1).ProbarAsync(Arg.Any<CancellationToken>());
    }
}
