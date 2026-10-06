using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-144 (DT §2.25, T2, R-05): ninguna de las 55 tablas de crédito heredadas vuelve, ni por nombre
/// en el modelo EF ni en el fuente.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0005) y la llena la fase Fundacional B al retirar (data-model §13.1); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LaCarteraHeredadaNoVuelve
{
    /// <summary>Nombres de las 55 tablas retiradas.</summary>
    private static readonly string[] Nombres = [];

    /// <summary>Carpetas revisadas (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [
        "Core",
        "Infrastructure",
        "Presentation",
    ];

    [Fact]
    public void Ninguna_tabla_retirada_aparece_en_el_fuente()
    {
        var prohibidos = Nombres
            .Select(n => (new Regex($@"\b{Regex.Escape(n)}\b", RegexOptions.Compiled), $"nombra la tabla retirada {n}"))
            .ToList();
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(Carpetas), prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "La cartera heredada vuelve a aparecer (FR-144)");
    }
}
