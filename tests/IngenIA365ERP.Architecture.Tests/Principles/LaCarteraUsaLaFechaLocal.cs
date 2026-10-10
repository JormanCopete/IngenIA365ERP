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
        // Fase Fundacional B (T0143): la Cartera nueva, ya sin la heredada.
        "Core/IngenIA365ERP.Application/Lending",
    ];

    /// <summary>El procesador de Cartera de la API (T0143).</summary>
    private static readonly string[] Archivos = [
        "Presentation/IngenIA365ERP.API/Integration/ProcesadorDeCartera.cs",
        "Presentation/IngenIA365ERP.API/Integration/OpcionesDelProcesoDeCartera.cs",
    ];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bTodayUtc\b", RegexOptions.Compiled), "usa la fecha UTC en vez de HoyLocal"),
        (new Regex(@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b", RegexOptions.Compiled), "lee el reloj directo"),
        (new Regex(@"\bTimeProvider\.System\b", RegexOptions.Compiled), "usa el reloj del sistema"),
    ];

    [Fact]
    public void La_regla_mira_la_ventana_legal_y_la_cola_de_mensajes()
    {
        // US17 (T0273): Domain/Lending/Collections y Application/Lending/Collections entran por sus carpetas (recursivas).
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Collections", "VentanaLegalDeContacto.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Collections", "ColaDeMensajesAlDeudor.cs"), StringComparison.Ordinal));
    }

    [Fact]
    public void Cartera_no_lee_el_reloj_por_su_cuenta()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas).Concat(ReglaSobreFuente.ArchivosNombrados(Archivos)), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Cartera lee el reloj fuera de la fecha local (T9)");
    }
}
