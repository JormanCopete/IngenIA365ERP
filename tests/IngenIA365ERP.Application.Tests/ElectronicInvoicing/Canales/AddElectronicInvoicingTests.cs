using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;
using IngenIA365ERP.ElectronicInvoicing.Circuit;
using IngenIA365ERP.ElectronicInvoicing.Credentials;
using IngenIA365ERP.ElectronicInvoicing.Processor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Canales;

/// <summary>
/// <c>AddElectronicInvoicing</c> (feature 012, I4, T728–T731): reemplaza los defectos de <c>AddApplication</c> por los reales, lee la sección
/// técnica <c>ElectronicInvoicing</c> y deja el procesador registrado <b>sin</b> arrancarlo (eso es de <c>Program.cs</c>, T47). (nuevo)
/// </summary>
public sealed class AddElectronicInvoicingTests
{
    private static ServiceCollection ConDefectosDeApplication()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new EsperasDeReintento([TimeSpan.FromSeconds(15)], TimeSpan.FromHours(1), TimeSpan.FromMinutes(2)));
        services.AddScoped<ICanalesDeEmision, SinCanalesRegistrados>();
        services.AddScoped<ICredencialesDeCanal, SinCredencialesConfiguradas>();
        return services;
    }

    private static IConfiguration Configuracion(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    [Fact]
    public void Reemplaza_los_defectos_por_los_canales_las_credenciales_y_el_circuito_reales()
    {
        var services = ConDefectosDeApplication();

        services.AddElectronicInvoicing(Configuracion([]));

        Assert.Equal(typeof(CanalesDeEmision), Assert.Single(services, d => d.ServiceType == typeof(ICanalesDeEmision)).ImplementationType);
        Assert.Equal(typeof(CredencialesEnArchivo), Assert.Single(services, d => d.ServiceType == typeof(ICredencialesDeCanal)).ImplementationType);
        Assert.Single(services, d => d.ServiceType == typeof(IRegistroDeFallasDelCanal));
        Assert.Single(services, d => d.ServiceType == typeof(EsperasDeReintento));
        Assert.Contains(services, d => d.ServiceType == typeof(CircuitoDeCanal));
        Assert.Contains(services, d => d.ServiceType == typeof(ProcesadorDeDocumentosElectronicos));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));

        using var sp = services.BuildServiceProvider();
        var canales = sp.GetRequiredService<ICanalesDeEmision>();
        Assert.Equal(["SIMULADO"], canales.Codigos);
        Assert.IsType<CanalSimulado>(canales.Resolver("simulado"));
        Assert.Throws<InvalidOperationException>(() => canales.Resolver("NINGUNO"));
    }

    [Fact]
    public void Lee_la_seccion_tecnica_con_sus_defectos()
    {
        var services = ConDefectosDeApplication();
        services.AddElectronicInvoicing(Configuracion(new()
        {
            ["ElectronicInvoicing:CredentialsPath"] = "/tmp/fe",
            ["ElectronicInvoicing:Retries:DelaysSeconds:0"] = "5",
            ["ElectronicInvoicing:Retries:DelaysSeconds:1"] = "10",
            ["ElectronicInvoicing:Retries:ThenEverySeconds"] = "600",
            ["ElectronicInvoicing:Channels:SIMULADO:DelayMilliseconds"] = "1500",
        }));
        using var sp = services.BuildServiceProvider();

        var opciones = sp.GetRequiredService<IOptions<ElectronicInvoicingOptions>>().Value;
        var esperas = sp.GetRequiredService<EsperasDeReintento>();

        Assert.Equal("/tmp/fe", opciones.CredentialsPath);
        Assert.Equal(1500, opciones.Canal("SIMULADO").DelayMilliseconds);
        Assert.Equal(5, opciones.Canal("OTRO").ConnectTimeoutSeconds);
        Assert.Equal(TimeSpan.FromSeconds(5), esperas.Tras(1));
        Assert.Equal(TimeSpan.FromSeconds(10), esperas.Tras(2));
        Assert.Equal(TimeSpan.FromMinutes(10), esperas.Tras(3));
    }

    [Fact]
    public void Sin_seccion_las_esperas_son_las_del_contrato_y_las_credenciales_van_al_Secret_montado()
    {
        var opciones = new ElectronicInvoicingOptions();
        var esperas = opciones.Retries.ComoEsperas();

        Assert.Equal("/secrets/facturacion-electronica/", opciones.CredentialsPath);
        Assert.Equal(
            [TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15),
             TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60)],
            esperas.Esperas);
        Assert.Equal(TimeSpan.FromHours(1), esperas.Despues);
    }
}
