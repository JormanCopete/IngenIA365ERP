using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.ExternalChannels.Circuit;
using IngenIA365ERP.ExternalChannels.CreditBureaus;
using IngenIA365ERP.ExternalChannels.Credentials;
using IngenIA365ERP.ExternalChannels.Messaging;
using IngenIA365ERP.ExternalChannels.RestrictiveLists;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IngenIA365ERP.ExternalChannels;

/// <summary>
/// Raíz de composición de los canales externos (feature 013, T0081, T0082; decisiones-transversales T71–T73; contracts/adaptadores-y-archivos.md
/// §2, §3). La llama sólo <c>API/Program.cs</c> (<c>LosProveedoresExternosSoloLosConoceSuAdaptador</c>), <b>después</b> de
/// <c>AddApplicationServices</c>: reemplaza los defectos que ése deja (<c>SinCredencialesDeCanalExterno</c>, <c>CircuitoSiempreCerrado</c>)
/// y suma los adaptadores a los registros de Application (<c>CanalesDeMensajeria</c>, <c>ProveedoresDeListas</c>). Registra:
/// <list type="bullet">
/// <item>la sección técnica <c>ExternalChannels</c> (<see cref="ExternalChannelsOptions"/>);</item>
/// <item>las credenciales en archivo (<see cref="CredencialesDeCanalExternoEnArchivo"/>, con su caché);</item>
/// <item>el circuito por cooperativa y canal (<see cref="CircuitoDeCanalExterno"/>, con su estado en memoria);</item>
/// <item>los adaptadores: <see cref="MensajeriaSimulada"/>, <see cref="ListasSimuladas"/> y <see cref="CanalDeListasCargadas"/>
/// (<c>CARGA-PROPIA</c>). <c>CentralDeRiesgoSimulada</c> (US2). Los de <c>Securities/</c> los suma US3, y cada proveedor real su
/// adaptador con sus clientes HTTP (<see cref="AgregarClientesDelCanal"/>).</item>
/// </list>
/// Dos adaptadores del mismo puerto con el mismo código hacen <b>fallar la composición</b> —el arranque— (§3.1 regla 6): cada uno se anota
/// como <see cref="CanalExternoRegistrado"/> y el segundo lanza aquí, antes de construir nada.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddExternalChannels(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var seccion = configuration.GetSection(ExternalChannelsOptions.SectionName);
        services.Configure<ExternalChannelsOptions>(seccion);

        // Credenciales en archivo (§3.4).
        services.TryAddSingleton<CacheDeCredencialesExternas>();
        services.RemoveAll<ICredencialesDeCanalExterno>();
        services.AddScoped<ICredencialesDeCanalExterno, CredencialesDeCanalExternoEnArchivo>();

        // Circuito por cooperativa y canal (§3.5).
        services.TryAddSingleton<EstadoDeLosCircuitosExternos>();
        services.RemoveAll<ICircuitoDeCanalExterno>();
        services.AddScoped<ICircuitoDeCanalExterno, CircuitoDeCanalExterno>();

        // Mensajería (§3.2): el simulado atiende SMS y WhatsApp; el correo ya lo registró Application.
        AgregarCanal(services, PuertosExternos.Mensajeria, MensajeriaSimulada.Codigo, typeof(MensajeriaSimulada));
        services.TryAddSingleton<MemoriaDeMensajeriaSimulada>();
        services.TryAddSingleton<MensajeriaSimulada>();
        services.AddSingleton<ICanalDeMensajeria>(sp => sp.GetRequiredService<MensajeriaSimulada>());

        // Listas restrictivas (§3.8): el simulado y la carga propia, que vale en producción.
        AgregarCanal(services, PuertosExternos.ListasRestrictivas, ListasSimuladas.Codigo, typeof(ListasSimuladas));
        services.TryAddSingleton<ListasSimuladas>();
        services.AddSingleton<IListasRestrictivas>(sp => sp.GetRequiredService<ListasSimuladas>());
        AgregarCanal(services, PuertosExternos.ListasRestrictivas, CanalDeListasCargadas.Codigo, typeof(CanalDeListasCargadas));
        services.AddScoped<IListasRestrictivas, CanalDeListasCargadas>();

        // Centrales de riesgo (§3.7, US2 bloque 6B): el simulado, que recuerda por cooperativa lo que respondió.
        AgregarCanal(services, PuertosExternos.CentralesDeRiesgo, CentralDeRiesgoSimulada.Codigo, typeof(CentralDeRiesgoSimulada));
        services.TryAddSingleton<MemoriaDeCentralSimulada>();
        services.TryAddSingleton<CentralDeRiesgoSimulada>();
        services.AddSingleton<ICentralDeRiesgo>(sp => sp.GetRequiredService<CentralDeRiesgoSimulada>());

        // Los clientes HTTP de los proveedores reales que tengan sección (los simulados no hacen HTTP).
        var leidas = seccion.Get<ExternalChannelsOptions>() ?? new ExternalChannelsOptions();
        foreach (var (canal, opciones) in leidas.Channels)
        {
            if (CodigosDeCanalExterno.NoPideCredencial(canal)) continue;
            AgregarClientesDelCanal(services, canal, opciones ?? new OpcionesDeCanalExterno());
        }

        return services;
    }

    /// <summary>
    /// Anota un adaptador de <paramref name="puerto"/> con su código; si ya había otro del mismo puerto y código, lanza: dos adaptadores con
    /// el mismo código hacen fallar el arranque (§3.1 regla 6). (nuevo)
    /// </summary>
    public static void AgregarCanal(IServiceCollection services, string puerto, string channelCode, Type implementacion)
    {
        ArgumentNullException.ThrowIfNull(services);
        var repetido = services
            .Where(d => d.ServiceType == typeof(CanalExternoRegistrado) && d.ImplementationInstance is CanalExternoRegistrado)
            .Select(d => (CanalExternoRegistrado)d.ImplementationInstance!)
            .FirstOrDefault(r => r.Puerto == puerto && string.Equals(r.ChannelCode, channelCode, StringComparison.OrdinalIgnoreCase));
        if (repetido is not null)
            throw new InvalidOperationException(
                $"Dos adaptadores de {puerto} declaran el canal «{channelCode}»: {repetido.Implementacion.Name} y {implementacion.Name}.");
        services.AddSingleton(new CanalExternoRegistrado(puerto, channelCode, implementacion));
    }

    /// <summary>El cliente HTTP de las escrituras de un canal (enviar, crear, endosar, anular): sólo tiempos, <b>sin reintentos</b> (§3.1 regla 4).</summary>
    public static string ClienteDeEscritura(string channelCode) => $"canales-externos:{channelCode.ToUpperInvariant()}:escritura";

    /// <summary>El cliente HTTP de las consultas de un canal: con la resiliencia estándar, porque son idempotentes (§3.1 regla 4).</summary>
    public static string ClienteDeConsulta(string channelCode) => $"canales-externos:{channelCode.ToUpperInvariant()}:consulta";

    /// <summary>
    /// Registra los dos clientes HTTP de un canal con los tiempos de <c>ExternalChannels:Channels:{channelCode}</c> (§3.1 reglas 4 y 5): el de
    /// escritura sin reintentos y el de consulta con <c>AddStandardResilienceHandler</c>. Lo usará el adaptador de cada proveedor. (nuevo)
    /// </summary>
    public static void AgregarClientesDelCanal(IServiceCollection services, string channelCode, OpcionesDeCanalExterno opciones)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelCode);
        ArgumentNullException.ThrowIfNull(opciones);
        var conexion = TimeSpan.FromSeconds(Math.Max(1, opciones.ConnectTimeoutSeconds));
        var total = TimeSpan.FromSeconds(Math.Max(opciones.ConnectTimeoutSeconds + 1, opciones.TotalTimeoutSeconds));

        services.AddHttpClient(ClienteDeEscritura(channelCode), c => c.Timeout = total)
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
