using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

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
    ];

    private static readonly Regex Requerido = new(@"IsProduction\(\)", RegexOptions.Compiled);

    [Fact]
    public void El_registro_de_cada_doble_mira_si_es_produccion()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Un doble de prueba se registra sin mirar si es producción (FR-089, T20)");
    }
}
