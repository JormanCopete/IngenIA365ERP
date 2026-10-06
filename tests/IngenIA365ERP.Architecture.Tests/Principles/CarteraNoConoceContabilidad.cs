using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-126 y E-01 (DT §2.25, T51): Cartera nunca conoce una cuenta ni escribe asientos; lo que cruza
/// a Contabilidad cruza por mensajes. Ni el escritor contable, ni tablas <c>ACC_</c>, ni entidades contables.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0001) y la llena la fase Fundacional B con las carpetas de Cartera nueva (las de hoy son de la cartera heredada, que se retira); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class CarteraNoConoceContabilidad
{
    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bAccountingPoster\b", RegexOptions.Compiled), "usa AccountingPoster"),
        (new Regex(@"""ACC_", RegexOptions.Compiled), "nombra una tabla ACC_"),
        (new Regex(@"IngenIA365ERP\.Domain\.Entities\.Accounting\b", RegexOptions.Compiled), "usa entidades contables"),
    ];

    [Fact]
    public void Cartera_no_escribe_asientos_ni_conoce_cuentas()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Cartera conoce Contabilidad (FR-126, E-01)");
    }
}
