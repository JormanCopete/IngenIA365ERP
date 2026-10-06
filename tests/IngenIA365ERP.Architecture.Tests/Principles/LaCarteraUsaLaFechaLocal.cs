using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T9): todo Cartera usa la fecha local <c>America/Bogota</c> (<c>HoyLocal</c>); el motor
/// recibe las fechas por parámetro. Recorre el motor nuevo desde el Setup; la fase Fundacional B suma
/// <c>Application/Lending</c> cuando la cartera heredada se retira.
/// </summary>
public class LaCarteraUsaLaFechaLocal
{
    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = [
        "Core/IngenIA365ERP.Domain/Lending",
    ];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b", RegexOptions.Compiled), "lee el reloj directo"),
        (new Regex(@"\bTimeProvider\.System\b", RegexOptions.Compiled), "usa el reloj del sistema"),
    ];

    [Fact]
    public void Cartera_no_lee_el_reloj_por_su_cuenta()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Cartera lee el reloj fuera de la fecha local (T9)");
    }
}
