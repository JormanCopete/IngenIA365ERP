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
        // US1 (T0340): tampoco por la puerta de atrás.
        (new Regex(@"\bMathF\.", RegexOptions.Compiled), "usa MathF (coma flotante)"),
        (new Regex(@"\bConvert\.To(Double|Single)\(", RegexOptions.Compiled), "convierte a coma flotante"),
    ];

    /// <summary>US1 (T0340): las piezas del motor de esta fase, que la regla tiene que estar mirando.</summary>
    private static readonly string[] PiezasDeUS1 =
    [
        Path.Combine("DayCount", "ContadorDeDias.cs"),
        Path.Combine("Rates", "EquivalenciaDeTasas.cs"),
        Path.Combine("Rates", "TasaDelDia.cs"),
        Path.Combine("Rates", "ControlDeUsura.cs"),
        Path.Combine("Schedules", "FechasDeVencimiento.cs"),
        Path.Combine("Schedules", "GeneradorDePlan.cs"),
        Path.Combine("Lending", "MotorDeCartera.cs"),
        Path.Combine("Lending", "RedondeoDeCartera.cs"),
    ];

    [Fact]
    public void La_regla_mira_los_dias_las_tasas_la_usura_y_el_plan()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        foreach (var pieza in PiezasDeUS1)
            Assert.Contains(archivos, a => a.EndsWith(pieza, StringComparison.Ordinal));
    }

    /// <summary>La regla bitemporal que leen las versiones de Cartera (T0143): también sin coma flotante.</summary>
    private static readonly string[] Archivos = ["Core/IngenIA365ERP.Domain/Common/VigenciaBitemporal.cs"];

    [Fact]
    public void La_regla_mira_la_ventana_legal_de_contacto()
    {
        // US17 (T0273): Domain/Lending/Collections entra por la carpeta del motor (recursiva).
        Assert.Contains(ReglaSobreFuente.ArchivosEn(Carpetas), a => a.EndsWith(Path.Combine("Collections", "VentanaLegalDeContacto.cs"), StringComparison.Ordinal));
    }

    /// <summary>US4 (T0480): la causación, la mora, la suspensión y la reconstrucción a (D, R), que la regla también tiene que estar mirando.</summary>
    private static readonly string[] PiezasDeUS4 =
    [
        Path.Combine("Accrual", "Causacion.cs"),
        Path.Combine("Accrual", "ReglaDeSuspension.cs"),
        Path.Combine("Delinquency", "AlturaDeMora.cs"),
        Path.Combine("Replay", "CausacionDeCredito.cs"),
        Path.Combine("Replay", "EntradaDeReconstruccion.cs"),
        Path.Combine("Replay", "EstadoDelCredito.cs"),
        Path.Combine("Replay", "LibroTeorico.cs"),
        Path.Combine("Replay", "ReconstruccionDeCredito.cs"),
        Path.Combine("Lending", "VersionesDeReglas.cs"),
        Path.Combine("Lending", "ExplicacionDeCartera.cs"),
        Path.Combine("Lending", "HuellaDeCartera.cs"),
        // US5 (T0533–T0536): la prelación, el aplicador de pagos y el tope del prepago.
        Path.Combine("Payments", "Prelacion.cs"),
        Path.Combine("Payments", "AplicadorDePagos.cs"),
        Path.Combine("Payments", "DistribucionDePago.cs"),
        Path.Combine("Payments", "TopeLegalDePrepago.cs"),
    ];

    [Fact]
    public void La_regla_mira_la_causacion_la_mora_y_la_reconstruccion()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        foreach (var pieza in PiezasDeUS4)
            Assert.Contains(archivos, a => a.EndsWith(pieza, StringComparison.Ordinal));
    }

    [Fact]
    public void El_motor_no_usa_coma_flotante()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas).Concat(ReglaSobreFuente.ArchivosNombrados(Archivos)), Prohibidos,
            relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "El motor de Cartera usa coma flotante (FR-010)");
    }
}
