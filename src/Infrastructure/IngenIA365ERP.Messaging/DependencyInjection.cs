using IngenIA365ERP.Application.Common.Mensajeria;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Messaging;

/// <summary>
/// Registra la mensajería (feature 023). Sólo la llama <c>API/Program.cs</c>, que además arranca los servicios de fondo que devuelve
/// <see cref="ServiciosDeFondo"/> (como el procesador electrónico: ningún otro sitio llama <c>AddHostedService</c>). El DbMigrator no la
/// llama: allí queda la <see cref="ColaNula"/> de Application.
/// </summary>
public static class DependencyInjection
{
    /// <param name="baseLista">Si la base ya está lista (los consumidores esperan a que lo esté).</param>
    /// <param name="etiquetaReady">La etiqueta de los chequeos de <c>/health/ready</c>.</param>
    public static OpcionesDeMensajeria AddMessaging(this IServiceCollection services, IConfiguration configuracion, Func<IServiceProvider, bool> baseLista,
        string etiquetaReady)
    {
        var seccion = configuracion.GetSection(OpcionesDeMensajeria.Seccion);
        services.AddOptions<OpcionesDeMensajeria>().Bind(seccion)
            .Validate(o => o.Problemas().Count == 0, "La sección Mensajeria no es válida (ver el log de arranque).")
            .ValidateOnStart();
        var opciones = seccion.Get<OpcionesDeMensajeria>() ?? new OpcionesDeMensajeria();

        if (opciones.EsRabbitMq)
        {
            services.AddSingleton<ConexionRabbitMq>();
            services.AddSingleton<ColaRabbitMq>();
            services.Replace(ServiceDescriptor.Singleton<IColaDeTrabajos>(sp => sp.GetRequiredService<ColaRabbitMq>()));
            services.AddSingleton<ConectorRabbitMq>();
            services.AddSingleton(sp => new ConsumidorRabbitMq(
                sp.GetRequiredService<ConexionRabbitMq>(), sp.GetRequiredService<ConsumoDeAvisos>(),
                sp.GetRequiredService<IMedidorDeTrabajos>(), sp.GetRequiredService<IOptions<OpcionesDeMensajeria>>(),
                sp.GetRequiredService<ILogger<ConsumidorRabbitMq>>(), () => baseLista(sp)));
            services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>(RabbitMqHealthCheck.Nombre, HealthStatus.Degraded, [etiquetaReady]);
        }
        else
        {
            services.AddSingleton<ColaEnProceso>();
            services.Replace(ServiceDescriptor.Singleton<IColaDeTrabajos>(sp => sp.GetRequiredService<ColaEnProceso>()));
            services.AddSingleton(sp => new ConsumidorEnProceso(
                sp.GetRequiredService<ColaEnProceso>(), sp.GetRequiredService<ConsumoDeAvisos>(),
                sp.GetRequiredService<IMedidorDeTrabajos>(), sp.GetRequiredService<IOptions<OpcionesDeMensajeria>>(),
                sp.GetRequiredService<ILogger<ConsumidorEnProceso>>(), () => baseLista(sp)));
        }
        return opciones;
    }

    /// <summary>Los servicios de fondo que <c>Program.cs</c> arranca con <c>AddHostedService</c>, según el proveedor.</summary>
    public static IReadOnlyList<Type> ServiciosDeFondo(OpcionesDeMensajeria opciones) =>
        opciones.EsRabbitMq ? [typeof(ConectorRabbitMq), typeof(ConsumidorRabbitMq)] : [typeof(ConsumidorEnProceso)];
}
