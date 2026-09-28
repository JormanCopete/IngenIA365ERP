using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Channels.Simulado;
using IngenIA365ERP.ElectronicInvoicing.Circuit;
using IngenIA365ERP.ElectronicInvoicing.Credentials;
using IngenIA365ERP.ElectronicInvoicing.Processor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ElectronicInvoicing;

/// <summary>
/// Raíz de composición de la facturación electrónica (feature 012, entrega I4, T688, T728–T731; decisiones-transversales §1.1,
/// contracts/dian.md §2). La llama sólo <c>API/Program.cs</c> (<c>ElProveedorTecnologicoSoloLoConoceSuAdaptador</c>), <b>después</b> de
/// <c>AddApplication</c>: reemplaza los defectos que ése deja (<c>SinCanalesRegistrados</c>, <c>SinCredencialesConfiguradas</c> y las esperas
/// del contrato) por los reales. Registra:
/// <list type="bullet">
/// <item>la sección técnica <c>ElectronicInvoicing</c> (<see cref="ElectronicInvoicingOptions"/>) y las esperas de <c>ElectronicInvoicing:Retries</c>;</item>
/// <item>los canales: <see cref="CanalSimulado"/> (con su memoria) y el registro <see cref="CanalesDeEmision"/>; el adaptador del proveedor
/// (T732) y <c>CanalServicioCentral</c> (T733) se suman aquí cuando existan, con sus clientes HTTP (<see cref="AgregarClientesDelCanal"/>);</item>
/// <item>las credenciales en archivo (<see cref="CredencialesEnArchivo"/>, con su caché);</item>
/// <item>el circuito (<see cref="CircuitoDeCanal"/>, que es el <see cref="IRegistroDeFallasDelCanal"/>);</item>
/// <item>el procesador (<see cref="ProcesadorDeDocumentosElectronicos"/>) como singleton, <b>sin</b> <c>AddHostedService</c>: lo arranca
/// <c>Program.cs</c> (T47, T749).</item>
/// </list>
/// </summary>
public static class DependencyInjection
{
    /// <param name="services">El contenedor.</param>
    /// <param name="configuration">La configuración de la API.</param>
    /// <param name="baseLista">
    /// Si la base ya está migrada (en la API, <c>DatabaseReadiness.IsReady</c>): el procesador no arranca antes. Este proyecto no conoce
    /// Persistence, así que la condición la da quien lo compone; nula = siempre lista.
    /// </param>
    public static IServiceCollection AddElectronicInvoicing(this IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, bool>? baseLista = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var seccion = configuration.GetSection(ElectronicInvoicingOptions.SectionName);
        services.Configure<ElectronicInvoicingOptions>(seccion);

        // Esperas entre intentos (§6.3): las de la sección reemplazan los defectos de AddApplication.
        services.RemoveAll<EsperasDeReintento>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ElectronicInvoicingOptions>>().Value.Retries.ComoEsperas());

        // Canales.
        services.TryAddSingleton<MemoriaDelCanalSimulado>();
        services.TryAddSingleton<CanalSimulado>();
        services.AddSingleton<ICanalDeEmisionElectronica>(sp => sp.GetRequiredService<CanalSimulado>());
        services.RemoveAll<ICanalesDeEmision>();
        services.AddSingleton<ICanalesDeEmision, CanalesDeEmision>();
        var leidas = seccion.Get<ElectronicInvoicingOptions>() ?? new ElectronicInvoicingOptions();
        foreach (var (canal, opciones) in leidas.Channels)
        {
            if (string.Equals(canal, CanalSimulado.Codigo, StringComparison.OrdinalIgnoreCase)) continue;
            AgregarClientesDelCanal(services, canal, opciones ?? new OpcionesDeCanal());
        }

        // Credenciales en archivo (§11).
        services.TryAddSingleton<CacheDeCredenciales>();
        services.RemoveAll<ICredencialesDeCanal>();
        services.AddScoped<ICredencialesDeCanal, CredencialesEnArchivo>();

        // Circuito por cooperativa y canal (§7.2).
        services.TryAddSingleton<EstadoDeLosCircuitos>();
        services.TryAddScoped<CircuitoDeCanal>();
        services.RemoveAll<IRegistroDeFallasDelCanal>();
        services.AddScoped<IRegistroDeFallasDelCanal>(sp => sp.GetRequiredService<CircuitoDeCanal>());

        // Procesador de fondo (§6.4): sólo el singleton; AddHostedService es de Program.cs.
        services.TryAddSingleton(sp => new ProcesadorDeDocumentosElectronicos(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IEjecutorEnCooperativa>(),
            sp.GetRequiredService<IDateTimeService>(),
            sp.GetRequiredService<IOptions<ElectronicInvoicingOptions>>(),
            sp.GetRequiredService<ILogger<ProcesadorDeDocumentosElectronicos>>(),
            baseLista is null ? null : () => baseLista(sp)));

        return services;
    }

    /// <summary>El cliente HTTP de la emisión de un canal: sólo tiempos, <b>sin reintentos</b> (§3.3 regla 7). (nuevo)</summary>
    public static string ClienteDeEmision(string channelCode) => $"einvoicing:{channelCode.ToUpperInvariant()}:emision";

    /// <summary>El cliente HTTP de consulta y descarga de un canal: con la resiliencia estándar, porque son idempotentes (§3.3 regla 7). (nuevo)</summary>
    public static string ClienteDeConsulta(string channelCode) => $"einvoicing:{channelCode.ToUpperInvariant()}:consulta";

    /// <summary>
    /// Registra los dos clientes HTTP de un canal con los tiempos de <c>ElectronicInvoicing:Channels:{channelCode}</c> (§3.3 regla 8): el de
    /// emisión sin reintentos y el de consulta y descarga con <c>AddStandardResilienceHandler</c>. Lo usará el adaptador del proveedor
    /// (T732); <c>CanalSimulado</c> no hace HTTP. (nuevo)
    /// </summary>
    public static void AgregarClientesDelCanal(IServiceCollection services, string channelCode, OpcionesDeCanal opciones)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelCode);
        ArgumentNullException.ThrowIfNull(opciones);
        var conexion = TimeSpan.FromSeconds(Math.Max(1, opciones.ConnectTimeoutSeconds));
        var total = TimeSpan.FromSeconds(Math.Max(opciones.ConnectTimeoutSeconds + 1, opciones.TotalTimeoutSeconds));

        services.AddHttpClient(ClienteDeEmision(channelCode), c => c.Timeout = total)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectTimeout = conexion });

        var intento = TimeSpan.FromSeconds(Math.Max(1, total.TotalSeconds / 3));
        services.AddHttpClient(ClienteDeConsulta(channelCode))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectTimeout = conexion })
            .AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = intento;
                o.TotalRequestTimeout.Timeout = total;
                o.CircuitBreaker.SamplingDuration = intento * 2 + TimeSpan.FromSeconds(1);
            });
    }
}
