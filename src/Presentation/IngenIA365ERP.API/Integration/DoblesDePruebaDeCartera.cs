using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// La guarda de los dobles de prueba de Cartera en el arranque de la API (feature 013, T0230; T20, FR-089;
/// <c>LosDoblesDePruebaNoSeRegistranEnProduccion</c>). Se llama una vez desde <c>Program.cs</c>: lee
/// <c>Lending:TestDoubles:Enabled</c> y el carácter de producción del host (<c>IsProduction()</c>) y delega la decisión en
/// <see cref="DoblesDePruebaDeCarteraEnDi"/>: registra <c>TesoreriaDePrueba</c> (y desde US5 el recaudador) sólo si se piden y el host
/// no es de producción; si se piden en producción, lanza y la API no arranca. (nuevo)
/// </summary>
public static class DoblesDePruebaDeCartera
{
    public static void Registrar(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        var pedidos = configuration.GetValue(DoblesDePruebaDeCarteraEnDi.ClaveDeConfiguracion, false);
        var dobles = DoblesDePruebaDeCarteraEnDi.Registrar(services, pedidos, environment.IsProduction());
        // Feature 015, T0141 (FR-223; DT Q9, R-26): la bandera transitoria. Mientras existan los dobles, los consumidores reales de Treasury y Receipts se
        // registran sólo si los dobles no: ConsumidoresPorDestino.Validar() nunca ve dos consumidores del mismo destino. La fase 19 borra los dobles y esto.
        if (!dobles) ConsumidoresDeRecaudoYTesoreria.Registrar(services);
    }
}

/// <summary>
/// Los consumidores reales de los destinos <c>Treasury</c> y <c>Receipts</c> (feature 015, T0141, T0176): <c>DestinoTesoreria</c> desde US10 y <c>DestinoRecaudo</c>
/// desde US5. Los registra <see cref="DoblesDePruebaDeCartera"/> sólo cuando los dobles de prueba no están registrados (la bandera transitoria). (nuevo)
/// </summary>
public static class ConsumidoresDeRecaudoYTesoreria
{
    public static void Registrar(IServiceCollection services)
    {
        services.AddScoped<IngenIA365ERP.Application.Common.Integration.IDestinoDeMensajes, IngenIA365ERP.Application.Treasury.Integration.DestinoTesoreria>();
    }
}
