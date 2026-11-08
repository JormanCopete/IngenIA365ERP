using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Shared.Services.Transporte;

/// <summary>
/// Los clientes tipados de la feature 020 (Transporte, servicios por vehículo y conectores), en un solo lugar para Web, WASM y MAUI.
/// </summary>
public static class RegistroDeClientesDeTransporte
{
    public static IServiceCollection AddClientesDeTransporte(this IServiceCollection services)
    {
        services.AddScoped<Security.ModulosClient>();
        services.AddScoped<Conectores.ConectoresDePlataformaClient>();
        services.AddScoped<VehiculosClient>();
        services.AddScoped<DocumentosDeVehiculoClient>();
        services.AddScoped<ConductoresClient>();
        services.AddScoped<ServiciosDeTransporteClient>();
        return services;
    }
}
