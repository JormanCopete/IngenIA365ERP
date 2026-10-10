using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-117 y Ley 2300 (DT §2.25, T74): la cobranza sólo contacta al deudor y a los codeudores
/// obligados; nunca a las referencias personales o familiares.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0007) y la llena US17 (cola de mensajes) y US12 (cobranza) con sus carpetas; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LaCobranzaNuncaContactaReferencias
{
    /// <summary>Carpetas de mensajería y cobranza (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        // US17 (T0273, C108): la cola de mensajes al deudor, sus tareas, comandos y consultas. US12 (K4) suma la cobranza.
        "Core/IngenIA365ERP.Application/Lending/Collections",
    ];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(PersonalReferences?|FamilyReferences?|Referencias(Personales|Familiares))\b", RegexOptions.Compiled), "usa referencias personales o familiares"),
        // T0273: las referencias de la persona viven en COR_References (DbSet References): la cola no las lee.
        (new Regex(@"\.References\b|\bCOR_References\b|\bReference\b", RegexOptions.Compiled), "lee COR_References"),
    ];

    [Fact]
    public void La_regla_mira_la_cola_de_mensajes()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).Select(Path.GetFileName).ToList();
        Assert.Contains("ColaDeMensajesAlDeudor.cs", archivos);
        Assert.Contains("TareaDeMensajesAlDeudor.cs", archivos);
    }

    [Fact]
    public void La_cobranza_no_lee_referencias()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "La cobranza contacta referencias (FR-117)");
    }
}
