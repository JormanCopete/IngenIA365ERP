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
        DoblesDePruebaDeCarteraEnDi.Registrar(services, pedidos, environment.IsProduction());
    }
}
