using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-010 (DT §2.25, T35): las configuraciones EF de Cartera fijan la precisión por los alias de
/// <c>PrecisionDeCartera</c>, nunca con números sueltos.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0003) y la llena la fase Fundacional B con <c>Persistence/Configurations/Lending</c> nuevo (las de hoy son de la cartera heredada); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LosSaldosYTasasDeCarteraTienenSuPrecision
{
    /// <summary>Carpetas de configuraciones EF de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\.HasPrecision\(\s*\d", RegexOptions.Compiled), "precisión con números sueltos"),
        (new Regex(@"HasColumnType\(\s*\""(numeric|decimal)\(", RegexOptions.Compiled), "tipo decimal con números sueltos"),
    ];

    [Fact]
    public void Las_configuraciones_usan_los_alias_de_precision()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Una configuración de Cartera fija la precisión sin PrecisionDeCartera (FR-010)");
    }
}
