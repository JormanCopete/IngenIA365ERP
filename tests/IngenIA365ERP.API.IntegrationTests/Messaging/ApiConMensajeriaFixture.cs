using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Mensajeria;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.RabbitMq;

namespace IngenIA365ERP.API.IntegrationTests.Messaging;

/// <summary>
/// Feature 023 (research R12; FR-031): el host de siempre (<see cref="CentralIdentityApiFixture"/>) con la mensajería en un RabbitMQ de verdad
/// —colas quorum, confirmación del broker, ack manual— y consumiendo, como en los ambientes. El procesador de fondo sigue apagado: lo que no
/// emite la cola lo emiten las pasadas a mano. El broker sólo vive en esta fixture, no en la base: sumarlo a las ~25 clases multiplicaría la
/// carga. (nuevo)
/// </summary>
public sealed class ApiConMensajeriaFixture : CentralIdentityApiFixture
{
    public const string Usuario = "erp";
    public const string Clave = "erp-e2e";

    public RabbitMqContainer Rabbit { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.1-management-alpine")
        .WithUsername(Usuario)
        .WithPassword(Clave)
        .Build();

    protected override Task AntesDeArrancarAsync() => Rabbit.StartAsync();

    protected override void ConfigurarHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mensajeria:Proveedor", "RabbitMq");
        builder.UseSetting("Mensajeria:Host", Rabbit.Hostname);
        builder.UseSetting("Mensajeria:Puerto", Rabbit.GetMappedPublicPort(5672).ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Mensajeria:VirtualHost", "/");
        builder.UseSetting("Mensajeria:Usuario", Usuario);
        builder.UseSetting("Mensajeria:Clave", Clave);
        builder.UseSetting("Mensajeria:Consumir", "true");
    }

    protected override Task DespuesDeCerrarAsync() => Rabbit.DisposeAsync().AsTask();

    /// <summary>La cola de la réplica de prueba.</summary>
    public IColaDeTrabajos Cola => Factory.Services.GetRequiredService<IColaDeTrabajos>();

    /// <summary>Espera a que la réplica conecte con el broker (lo hace en segundo plano al arrancar).</summary>
    public async Task EsperarConexionAsync()
    {
        for (var i = 0; i < 150 && !Cola.Disponible; i++) await Task.Delay(200);
        if (!Cola.Disponible) throw new InvalidOperationException("La API no conectó con RabbitMQ en 30 s.");
    }
}

/// <summary>
/// Feature 023: una réplica configurada con RabbitMQ en un puerto donde no hay nadie. Prueba que sin broker la API arranca, vende, informa
/// salud degradada y el barrido emite (FR-010, FR-012). (nuevo)
/// </summary>
public sealed class ApiSinBrokerFixture : CentralIdentityApiFixture
{
    protected override void ConfigurarHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mensajeria:Proveedor", "RabbitMq");
        builder.UseSetting("Mensajeria:Host", "127.0.0.1");
        builder.UseSetting("Mensajeria:Puerto", "1");
        builder.UseSetting("Mensajeria:VirtualHost", "/");
        builder.UseSetting("Mensajeria:Usuario", "nadie");
        builder.UseSetting("Mensajeria:Clave", "nadie");
        builder.UseSetting("Mensajeria:Consumir", "true");
    }
}

[CollectionDefinition(Nombre)]
public sealed class MensajeriaCollection : ICollectionFixture<ApiConMensajeriaFixture>
{
    public const string Nombre = "Mensajeria e2e";
}

[CollectionDefinition(Nombre)]
public sealed class SinBrokerCollection : ICollectionFixture<ApiSinBrokerFixture>
{
    public const string Nombre = "Mensajeria sin broker e2e";
}
