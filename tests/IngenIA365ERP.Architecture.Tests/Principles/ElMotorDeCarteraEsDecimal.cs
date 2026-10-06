using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-010 (DT §2.25, T35, M16): el motor financiero calcula en <c>decimal</c> exacto; ni <c>double</c>,
/// ni <c>float</c>, ni las funciones de <c>Math</c> que trabajan en coma flotante. Las potencias, raíces y la TIR
/// son de <c>AritmeticaDecimal</c>. Recorre el motor nuevo desde el Setup.
/// </summary>
public class ElMotorDeCarteraEsDecimal
{
    /// <summary>Carpetas del motor (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [
        "Core/IngenIA365ERP.Domain/Lending",
    ];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(double|float|Single|Double)\b", RegexOptions.Compiled), "usa coma flotante"),
        (new Regex(@"\bMath\.(Pow|Exp|Log|Log10|Sqrt)\(", RegexOptions.Compiled), "usa Math en coma flotante"),
    ];

    [Fact]
    public void El_motor_no_usa_coma_flotante()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "El motor de Cartera usa coma flotante (FR-010)");
    }
}
