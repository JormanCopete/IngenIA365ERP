using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0003 (R-26; FR-223; TR32): con Recaudo y Tesorería reales no hay dobles de prueba en ningún ambiente. El consumidor de los
/// destinos <c>Receipts</c> y <c>Treasury</c> es el del módulo (<c>DestinoRecaudo</c>, <c>DestinoTesoreria</c>) y ningún otro tipo implementa
/// <see cref="IDestinoDeMensajes"/> en esos espacios. Reemplaza a <see cref="LosDoblesDePruebaNoSeRegistranEnProduccion"/> cuando la fase 19
/// (US9, T0300 y siguientes) borra <c>RecaudadorDePrueba</c> y <c>TesoreriaDePrueba</c>.
///
/// <para>
/// Árbol que recorre: los tipos de <see cref="RecaudoYTesoreria.NamespacesDeRecaudo"/> y de la Tesorería nueva. <see cref="Permitidos"/>, los
/// únicos <see cref="IDestinoDeMensajes"/> admitidos en esos espacios, nace vacía en el Setup y la llenan las fases 7 (<c>DestinoTesoreria</c>)
/// y 12 (<c>DestinoRecaudo</c>). <see cref="PendientesDeRetiro"/> nombra los dos dobles de la 013 que todavía existen: la fase 19 los borra y
/// vacía la lista; hasta entonces ninguno más puede nacer.
/// </para>
/// </summary>
public class NoHayDoblesDePrueba
{
    /// <summary>Los consumidores de mensajes admitidos en Recaudo y Tesorería, por nombre completo.</summary>
    private static readonly string[] Permitidos =
    [
    ];

    /// <summary>Los dobles de la 013 que la fase 19 retira (por nombre de tipo).</summary>
    private static readonly string[] PendientesDeRetiro = ["RecaudadorDePrueba", "TesoreriaDePrueba"];

    [Fact]
    public void Solo_los_consumidores_del_modulo_implementan_los_destinos_de_Recaudo_y_Tesoreria()
    {
        var infractores = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDestinoDeMensajes).IsAssignableFrom(t))
            .Where(t => RecaudoYTesoreria.EsDeRecaudo(t.Namespace) || RecaudoYTesoreria.EsDeTesoreria(t.Namespace))
            .Where(t => !Permitidos.Contains(t.FullName, StringComparer.Ordinal))
            .Select(t => t.FullName!)
            .ToList();
        ReglaSobreFuente.SinInfracciones(infractores, "Un consumidor no admitido de los destinos de Recaudo o Tesorería (FR-223)");
    }

    [Fact]
    public void No_nace_ningun_doble_de_prueba_nuevo()
    {
        var dobles = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t.IsClass && t.Name.EndsWith("DePrueba", StringComparison.Ordinal) && !t.Name.Contains('<', StringComparison.Ordinal))
            .Where(t => !PendientesDeRetiro.Contains(t.Name, StringComparer.Ordinal))
            .Select(t => t.FullName!)
            .ToList();
        ReglaSobreFuente.SinInfracciones(dobles, "Un doble de prueba nuevo en la aplicación (FR-223, TR32)");
    }
}
