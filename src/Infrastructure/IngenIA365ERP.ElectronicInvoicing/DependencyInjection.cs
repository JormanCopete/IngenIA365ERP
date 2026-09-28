using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.ElectronicInvoicing;

/// <summary>
/// Raíz de composición de la facturación electrónica (feature 012, entrega I4, T688; decisiones-transversales §1.1,
/// contracts/dian.md §2). La llama sólo <c>API/Program.cs</c> (<c>ElProveedorTecnologicoSoloLoConoceSuAdaptador</c>).
/// Nace vacía: los canales (<c>Channels/Simulado</c>, <c>Channels/ServicioCentral</c> y el del proveedor cuando se
/// elija), las credenciales en archivo (<c>Credentials</c>), el procesador de fondo (<c>Processor</c>) y el circuito
/// por canal (<c>Circuit</c>) se registran aquí en las tareas que los construyen.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddElectronicInvoicing(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
