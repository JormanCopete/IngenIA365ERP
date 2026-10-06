using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, Principio XI (DT §2.25, T51, T56): lo que Contabilidad registró por un hecho de Cartera no se
/// edita ni se borra; se corrige con un comprobante nuevo.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0001) y la llena US7 con <c>Application/Accounting/Lending</c> y <c>Application/Accounting/Integration</c>; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LoDeCarteraSeCorrigeConComprobanteNuevo
{
    /// <summary>Carpetas del lado contable de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\.IsPosted\s*=\s*false", RegexOptions.Compiled), "deshace un contabilizado"),
        (new Regex(@"\bJournalEntries\.Remove(Range)?\(", RegexOptions.Compiled), "borra movimientos"),
        (new Regex(@"\bDocuments\.Remove(Range)?\(", RegexOptions.Compiled), "borra un comprobante"),
    ];

    [Fact]
    public void El_lado_contable_de_Cartera_no_edita_lo_registrado()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Lo de Cartera se corrige editando (Principio XI)");
    }
}
