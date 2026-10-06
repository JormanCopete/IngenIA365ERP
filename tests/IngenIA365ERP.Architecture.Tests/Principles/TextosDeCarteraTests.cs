using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §7.6): ningún texto visible de Cartera muestra códigos técnicos ni el nombre del sistema
/// anterior. Molde: <c>TextosDeInventarioTests</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0007) y la llena cada historia con sus archivos de textos y pantallas (<c>Services/Cartera/TextosDeCartera.cs</c>, <c>Pages/Cartera</c>); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class TextosDeCarteraTests
{
    /// <summary>Archivos de textos y pantallas de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos =
    [
        (new Regex("SOLIDO", RegexOptions.Compiled | RegexOptions.IgnoreCase), "nombra el sistema anterior"),
        (new Regex("\"Lending\\.[A-Z]\\w*\\.[A-Z]\\w*\"", RegexOptions.Compiled), "muestra un código de error como texto"),
    ];

    [Fact]
    public void Los_textos_de_Cartera_no_muestran_codigos_ni_el_sistema_anterior()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosNombrados(Archivos), Prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Textos de Cartera con códigos técnicos o el nombre del sistema anterior (§7.6)");
    }
}
