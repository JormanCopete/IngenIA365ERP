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

    [Theory]
    [InlineData(false, new[] { "CONECTA-PRUEBAS", "SIMULADO" })]
    [InlineData(true, new[] { "CONECTA", "CONECTA-PRUEBAS", "SIMULADO" })]
    public void Los_canales_de_Conecta_se_registran_con_sus_extremos_y_el_real_solo_en_produccion(bool produccion, string[] esperados)
    {
        // Feature 028: la sección se escribe con guion bajo en las variables de entorno (CONECTA_PRUEBAS) y vale igual.
        var services = ConDefectosDeApplication();
        services.AddLogging();
        services.AddElectronicInvoicing(Configuracion(new()
        {
            ["ElectronicInvoicing:Channels:CONECTA_PRUEBAS:Endpoints:QA"] = "https://conecta-qa.ingenia365.com",
            ["ElectronicInvoicing:Channels:CONECTA:Endpoints:PDN"] = "https://conecta.ingenia365.com",
        }), procesoDeProduccion: produccion);
        using var sp = services.BuildServiceProvider();

        var canales = sp.GetRequiredService<ICanalesDeEmision>();

        Assert.Equal(esperados, canales.Codigos);
        var pruebas = Assert.IsType<IngenIA365ERP.ElectronicInvoicing.Channels.Conecta.CanalConecta>(canales.Resolver("CONECTA-PRUEBAS"));
        Assert.Equal(["QA"], pruebas.Extremos);
    }

    [Fact]
    public void Sin_extremos_ningun_canal_de_Conecta_se_registra()
    {
        var services = ConDefectosDeApplication();
        services.AddLogging();
        services.AddElectronicInvoicing(Configuracion(new() { ["ElectronicInvoicing:Channels:CONECTA:SyncWaitSeconds"] = "3" }), procesoDeProduccion: true);
        using var sp = services.BuildServiceProvider();

        Assert.Equal(["SIMULADO"], sp.GetRequiredService<ICanalesDeEmision>().Codigos);
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
