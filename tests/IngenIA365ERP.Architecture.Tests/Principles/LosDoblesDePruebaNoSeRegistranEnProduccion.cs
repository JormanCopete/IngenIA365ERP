using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-089 (DT §2.25, T20, pregunta B4): <c>RecaudadorDePrueba</c> y <c>TesoreriaDePrueba</c> no existen
/// en un despliegue de producción: su registro está detrás de <c>IsProduction()</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0002) y la llena las fases que crean <c>TesoreriaDePrueba</c> (fase 3) y <c>RecaudadorDePrueba</c> (US5); el caso final construye el contenedor de DI con <c>IHostEnvironment</c> de producción; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LosDoblesDePruebaNoSeRegistranEnProduccion
{
    /// <summary>Archivos donde se registra cada doble de prueba (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // Fase 3 (T0146, T0230): TesoreriaDePrueba; US5 suma RecaudadorDePrueba sobre la misma guarda.
        "Presentation/IngenIA365ERP.API/Integration/DoblesDePruebaDeCartera.cs",
        // US3 (T0428, T0457): las rutas de TesoreriaDePrueba se mapean con la misma guarda (en producción no existen).
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/TestDoublesEndpoints.cs",
    ];

    private static readonly Regex Requerido = new(@"IsProduction\(\)", RegexOptions.Compiled);

    [Fact]
    public void El_registro_de_cada_doble_mira_si_es_produccion()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Un doble de prueba se registra sin mirar si es producción (FR-089, T20)");
    }

    /// <summary>
    /// US5 (T0526): el contenedor de DI con un despliegue de producción —<c>IsProduction()</c>, lo que la API le pasa a
    /// <see cref="DoblesDePruebaDeCarteraEnDi"/>— no trae <see cref="RecaudadorDePrueba"/> ni <see cref="TesoreriaDePrueba"/>; si la configuración
    /// los pide (<c>Lending:TestDoubles:Enabled = true</c>), el arranque falla con un mensaje en español. Fuera de producción y pedidos, están los
    /// dos, cada uno como consumidor de su destino.
    /// </summary>
    [Fact]
    public void En_produccion_el_contenedor_no_tiene_los_dobles_y_pedirlos_tumba_el_arranque()
    {
        var produccion = new ServiceCollection();
        Assert.False(DoblesDePruebaDeCarteraEnDi.Registrar(produccion, pedidos: false, esProduccion: true));
        Assert.DoesNotContain(produccion, d => d.ImplementationType == typeof(RecaudadorDePrueba) || d.ImplementationType == typeof(TesoreriaDePrueba));

        var falla = Assert.Throws<InvalidOperationException>(() => DoblesDePruebaDeCarteraEnDi.Registrar(new ServiceCollection(), pedidos: true, esProduccion: true));
        Assert.Contains(DoblesDePruebaDeCarteraEnDi.ClaveDeConfiguracion, falla.Message, StringComparison.Ordinal);
        Assert.Contains("producción", falla.Message, StringComparison.Ordinal);

        var pruebas = new ServiceCollection();
        Assert.True(DoblesDePruebaDeCarteraEnDi.Registrar(pruebas, pedidos: true, esProduccion: false));
        Assert.Contains(pruebas, d => d.ServiceType == typeof(IDestinoDeMensajes) && d.ImplementationType == typeof(RecaudadorDePrueba));
        Assert.Contains(pruebas, d => d.ServiceType == typeof(IDestinoDeMensajes) && d.ImplementationType == typeof(TesoreriaDePrueba));
    }

    /// <summary>
    /// US5 (T0526): las rutas <c>/api/lending/test-doubles</c> —también las del recaudador— se mapean con la misma guarda que el registro
    /// (<c>Publicados</c>: pedidos y <c>!IsProduction()</c>), y la API le pasa a la guarda de DI el <c>IsProduction()</c> del host.
    /// </summary>
    [Fact]
    public void Las_rutas_de_los_dobles_y_su_registro_miran_el_mismo_criterio()
    {
        var rutas = FuenteSinComentarios.Leer(ReglaSobreFuente.ArchivosNombrados(["Presentation/IngenIA365ERP.API/Endpoints/Lending/TestDoublesEndpoints.cs"]).Single());
        Assert.Matches(@"if\s*\(\s*!Publicados\(", rutas);
        Assert.Matches(@"Publicados\([^)]*\)\s*=>\s*[^;]*ClaveDeConfiguracion[^;]*!ambiente\.IsProduction\(\)", rutas);
        Assert.Contains("\"/collector/payments\"", rutas, StringComparison.Ordinal);
        var guarda = FuenteSinComentarios.Leer(ReglaSobreFuente.ArchivosNombrados(["Presentation/IngenIA365ERP.API/Integration/DoblesDePruebaDeCartera.cs"]).Single());
        Assert.Contains("environment.IsProduction()", guarda, StringComparison.Ordinal);
    }
}
