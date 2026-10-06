using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-001 (DT §2.25, T23, C45): el libro, la marca de agua, las posiciones, las operaciones, las
/// versiones de condiciones y el plan sólo los escribe <c>RegistroDeCartera</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0003) y la llena la fase Fundacional B al crear <c>RegistroDeCartera</c> (carpetas de Cartera nueva y el registro como exento); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class NadieEscribeElLibroDeCarteraFueraDelRegistro
{
    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [];

    /// <summary>Archivos exentos: <c>RegistroDeCartera</c> (rutas relativas a la raíz).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(LoanTransactions|LoanOperations|LoanDailyPositions|LoanTermsVersions|RepaymentInstallments)\.Add(Range)?\(", RegexOptions.Compiled), "escribe el libro o sus proyecciones"),
        (new Regex(@"\bnew\s+(LoanTransaction|LoanOperation|LoanDailyPosition)\s*\(", RegexOptions.Compiled), "construye una fila del libro"),
    ];

    [Fact]
    public void Solo_el_registro_escribe_el_libro()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Alguien escribe el libro de Cartera fuera de RegistroDeCartera (FR-001)");
    }
}
