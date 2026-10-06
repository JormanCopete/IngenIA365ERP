using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, Principio XI (DT §2.25, R18, CH39): un cierre mensual de Cartera no se reescribe; la reapertura
/// es otro hecho.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0003) y la llena US9 con las carpetas del cierre; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LosCierresDeCarteraNoSeReescriben
{
    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(MonthlyCloses|MonthlyCloseLoans|MonthlyCloseGeneralImpairments)\.(Update|Remove)(Range)?\(", RegexOptions.Compiled), "modifica o borra un cierre"),
    ];

    [Fact]
    public void Ningun_cierre_se_modifica_ni_se_borra()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Un cierre de Cartera se reescribe (Principio XI)");
    }
}
