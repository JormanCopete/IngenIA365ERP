using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, Principio XI (DT §2.25, R18, CH39): un cierre mensual de Cartera no se reescribe; la reapertura
/// es otro hecho.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0003) y la llena US9 con las carpetas del cierre; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
///
/// <para>
/// US9 (T0674; data-model §23; R61): mira todo Cartera: nadie actualiza ni borra un cierre, sus líneas, su general, su reapertura, el historial de
/// mora ni la política, ni en bloque; nadie escribe un <c>Status</c> ni un <c>SupersededAt</c> sobre un cierre (el estado se deriva de la reapertura y
/// volver a cerrar es <c>Version + 1</c>).
/// </para>
/// </summary>
public class LosCierresDeCarteraNoSeReescriben
{
    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>). US9 (T0674): todo Cartera, Application y Domain.</summary>
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Application/Lending", "Core/IngenIA365ERP.Domain/Lending"];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(MonthlyCloses|MonthlyCloseLoans|MonthlyCloseGeneralImpairments)\.(Update|Remove)(Range)?\(", RegexOptions.Compiled), "modifica o borra un cierre"),
        // US9 (T0674; R61): tampoco la reapertura, el historial de mora ni la política; ni en bloque; ni el estado o el reemplazo de un cierre.
        (new Regex(@"\b(MonthlyCloseReopenings|LoanArrearsHistory|RiskPolicyVersions)\.(Update|Remove)(Range)?\(", RegexOptions.Compiled),
            "modifica o borra una reapertura, el historial de mora o la política"),
        (new Regex(@"\b(MonthlyCloses|MonthlyCloseLoans|MonthlyCloseGeneralImpairments|MonthlyCloseReopenings|LoanArrearsHistory)\b[^;]*\.Execute(Update|Delete)(Async)?\(",
            RegexOptions.Compiled), "actualiza o borra en bloque un cierre"),
        (new Regex(@"\b(cierre|Cierre|close|Close|monthlyClose)\w*\.(Status|SupersededAt)\s*=[^=]", RegexOptions.Compiled),
            "escribe el estado o el reemplazo de un cierre (se derivan, R61)"),
    ];

    [Fact]
    public void Ningun_cierre_se_modifica_ni_se_borra()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Un cierre de Cartera se reescribe (Principio XI)");
    }

    /// <summary>US9 (T0674): la regla mira las piezas del cierre, que tienen que existir.</summary>
    [Fact]
    public void La_regla_mira_el_cierre_y_su_reapertura()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        foreach (var pieza in new[] { "CierreMensualDeCartera.cs", "ReaperturaDelCierre.cs", "CreateBaseCloseCommand.cs", "MonthlyCloseQueries.cs" })
            Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Risk", pieza), StringComparison.Ordinal));
        // US8 (bloque 13C, T0728): la activación hace nacer el cierre base una sola vez, después de todas las tandas; también está en el recorrido.
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Activation", "ActivateLendingCommand.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Activation", "NacimientoDeCreditosMigrados.cs"), StringComparison.Ordinal));
        // K3 (US14, bloque B; T0901): la evaluación de cartera y su asunto de acta también están en el recorrido.
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Evaluations", "PortfolioEvaluationCommands.cs"), StringComparison.Ordinal));
        Assert.Contains(archivos, a => a.EndsWith(Path.Combine("Evaluations", "AsuntoDeEvaluacionDeCartera.cs"), StringComparison.Ordinal));
    }

    /// <summary>
    /// K3 (US14, bloque B; T0901, T0913; FR-102): la evaluación de cartera entra al cálculo del cierre sólo por el cargador del riesgo, como candidata, y
    /// sólo desde el cierre de su mes de efecto con lo aplicado hasta R; aplicarla no toca ningún cierre (las piezas de la evaluación no nombran los
    /// cierres ni sus líneas).
    /// </summary>
    [Fact]
    public void La_evaluacion_entra_solo_al_cierre_de_su_mes_de_efecto_y_no_toca_cierres()
    {
        var raiz = RepoPath.FindRepoRoot();
        var cargador = FuenteSinComentarios.Leer(Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Risk", "CargadorDeRiesgo.cs"));
        Assert.Matches(@"PortfolioEvaluations[\s\S]*?Status == PortfolioEvaluationStatus\.Applied && e\.AppliedAt <= r && e\.EffectiveYear \* 12 \+ e\.EffectiveMonth <= mesDelCorte", cargador);
        var carpeta = Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Risk", "Evaluations");
        var infracciones = Directory.EnumerateFiles(carpeta, "*.cs")
            .Where(a => Regex.IsMatch(FuenteSinComentarios.Leer(a), @"\b(MonthlyCloses|MonthlyCloseLoans)\b"))
            .Select(a => Path.GetFileName(a)).ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "La evaluación de cartera toca un cierre (Principio XI)");
    }
}
