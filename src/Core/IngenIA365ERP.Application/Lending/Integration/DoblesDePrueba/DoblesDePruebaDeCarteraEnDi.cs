using IngenIA365ERP.Application.Common.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;

/// <summary>
/// La decisión de registrar los dobles de prueba de Cartera (feature 013, T0230; T20, FR-089). La llama sólo
/// <c>API/Integration/DoblesDePruebaDeCartera.Registrar</c>, que lee <c>Lending:TestDoubles:Enabled</c> y le pasa
/// <c>IHostEnvironment.IsProduction()</c> (el único criterio de producción, el del despliegue y no el de la cooperativa):
/// <list type="bullet">
/// <item>pedidos fuera de producción: se registran (<see cref="TesoreriaDePrueba"/> y, desde US5, <see cref="RecaudadorDePrueba"/>);</item>
/// <item>no pedidos: no se registra nada;</item>
/// <item>pedidos en producción: <see cref="InvalidOperationException"/> con un mensaje en español, y la API no arranca.</item>
/// </list>
/// Vive en Application para que las pruebas sin contenedores la fijen; la API sólo la llama. (nuevo)
///
/// <para>
/// <b>Bandera transitoria de la feature 015 (T0141; DT Q9, R-26; del 04/11 al 19/11).</b> Cuando esto devuelve <c>false</c> (los dobles no se registran), la
/// API registra los consumidores reales <c>DestinoTesoreria</c> y <c>DestinoRecaudo</c> (<c>API/Integration/ConsumidoresDeRecaudoYTesoreria</c>); con
/// <c>true</c>, sólo los dobles. Así <c>ConsumidoresPorDestino.Validar()</c> nunca ve dos consumidores del mismo destino. <c>Lending:TestDoubles:Enabled</c>
/// pasa a <c>false</c> en DEV desde el hito del 04/11, en QA desde el 10/11 y en la fixture de las e2e antes de la puerta (T0300a); la fase 19 borra los dobles,
/// esta clase y la bandera (T0307).
/// </para>
/// </summary>
public static class DoblesDePruebaDeCarteraEnDi
{
    /// <summary>La clave de configuración que los pide.</summary>
    public const string ClaveDeConfiguracion = "Lending:TestDoubles:Enabled";

    /// <summary>
    /// Registra los dobles si se piden fuera de producción y devuelve si los registró. Lanza si se piden en producción.
    /// </summary>
    /// <exception cref="InvalidOperationException">La configuración pide los dobles en un despliegue de producción.</exception>
    public static bool Registrar(IServiceCollection services, bool pedidos, bool esProduccion)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!pedidos) return false;
        if (esProduccion)
        {
            throw new InvalidOperationException(
                $"La configuración pide los dobles de prueba de Cartera ({ClaveDeConfiguracion} = true) en un despliegue de producción. " +
                "Los dobles de Recaudo y Tesorería no existen en producción (FR-089): quite la clave o póngala en false.");
        }

        services.AddScoped<IDestinoDeMensajes, TesoreriaDePrueba>();
        // US5 (T0557): el recaudador de prueba, consumidor del destino Receipts y recaudador TST del contrato.
        services.AddScoped<IDestinoDeMensajes, RecaudadorDePrueba>();
        return true;
    }
}
