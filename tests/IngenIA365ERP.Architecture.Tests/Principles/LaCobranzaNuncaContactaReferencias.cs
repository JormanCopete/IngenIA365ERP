using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-117 y Ley 2300 (DT §2.25, T74): la cobranza sólo contacta al deudor y a los codeudores
/// obligados; nunca a las referencias personales o familiares.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0007) y la llena US17 (cola de mensajes) y US12 (cobranza) con sus carpetas; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LaCobranzaNuncaContactaReferencias
{
    /// <summary>Carpetas de mensajería y cobranza (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(PersonalReferences?|FamilyReferences?|Referencias(Personales|Familiares))\b", RegexOptions.Compiled), "usa referencias personales o familiares"),
    ];

    [Fact]
    public void La_cobranza_no_lee_referencias()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "La cobranza contacta referencias (FR-117)");
    }
}
