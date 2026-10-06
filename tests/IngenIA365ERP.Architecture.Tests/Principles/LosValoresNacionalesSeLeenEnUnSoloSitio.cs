using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-015 (DT §2.25, T40): los valores nacionales replicados se leen sólo por <c>IValoresNacionales</c>,
/// <c>ICalendarioHabil</c> e <c>INormativaVigente</c>.
///
/// <para>
/// Llenada en la fase 2 (T0036): se revisa todo el código de producción que podría tocar la copia (Application, Domain, la API, las
/// pantallas, las semillas y los servicios de Persistence) y sólo quedan exentos los tres lectores, el núcleo de la réplica y las
/// consultas de la copia misma (<c>Application/Core/NationalValues</c>: la pantalla de la cooperativa la consulta y la revisa, no
/// calcula con ella). El contexto de datos y las configuraciones de Persistence no entran: declaran la tabla.
/// </para>
/// </summary>
public class LosValoresNacionalesSeLeenEnUnSoloSitio
{
    /// <summary>Carpetas consumidoras (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        "Core/IngenIA365ERP.Application",
        "Core/IngenIA365ERP.Domain",
        "Presentation",
        "Infrastructure/IngenIA365ERP.Persistence/Seeding",
        "Infrastructure/IngenIA365ERP.Persistence/Services",
        "Infrastructure/IngenIA365ERP.ExternalChannels",
        "Infrastructure/IngenIA365ERP.ElectronicInvoicing",
    ];

    /// <summary>Archivos exentos: los tres lectores, la réplica y las consultas de la copia (rutas relativas a la raíz).</summary>
    private static readonly string[] Exentos =
    [
        "src/Core/IngenIA365ERP.Application/Common/NationalValues/LectorDeValoresNacionales.cs",
        "src/Core/IngenIA365ERP.Application/Common/Calendar/CalendarioDeDiasHabiles.cs",
        "src/Core/IngenIA365ERP.Application/Common/NationalValues/LectorDeDocumentoNormativo.cs",
        "src/Core/IngenIA365ERP.Application/Common/NationalValues/ReplicacionDeValoresNacionales.cs",
        "src/Core/IngenIA365ERP.Application/Core/NationalValues/CopiaDeValoresNacionales.cs",
        // Declara los DbSet (no lee).
        "src/Core/IngenIA365ERP.Application/Common/Interfaces/IApplicationDbContext.cs",
    ];

    /// <summary>
    /// El acceso al <c>DbSet</c> por un contexto (<c>db.</c>, <c>_db.</c>, <c>context.</c>…) o por <c>Set&lt;…&gt;()</c>. Los nombres de
    /// espacio (<c>Application.Common.NationalValues</c>) no cuentan: no van detrás de una variable de contexto.
    /// </summary>
    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b_?(db|context|contexto|dbContext|Db|Context)\s*\.\s*(NationalValues|RegulatoryDocuments)\b", RegexOptions.Compiled),
            "lee la réplica de valores nacionales directo"),
        (new Regex(@"\bSet<\s*(NationalValue|RegulatoryDocument)\s*>", RegexOptions.Compiled), "lee la réplica por Set<>"),
    ];

    [Fact]
    public void Nadie_lee_la_replica_salvo_los_lectores()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Alguien lee los valores nacionales fuera de sus lectores (FR-015)");
    }

    [Fact]
    public void Los_exentos_existen_y_los_lectores_si_leen()
    {
        // La lista no puede quedar desfasada: si un lector se mueve, la prueba dejaría de mirarlo sin avisar.
        var root = RepoPath.FindRepoRoot();
        foreach (var exento in Exentos)
            Assert.True(File.Exists(Path.Combine(root, exento)), $"{exento} no existe: si se movió, actualizá Exentos.");

        foreach (var lector in Exentos.Take(4))
        {
            var texto = File.ReadAllText(Path.Combine(root, lector));
            Assert.True(Prohibidos.Any(p => p.Patron.IsMatch(texto)), $"{lector} ya no lee la copia: ¿cambió el lector único?");
        }
    }

    [Fact]
    public void Hay_un_solo_lector_por_puerto()
    {
        var asm = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        string[] Implementaciones(Type puerto) => [.. asm.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && puerto.IsAssignableFrom(t))
            .Select(t => t.Name)];

        Assert.Equal(["LectorDeValoresNacionales"], Implementaciones(typeof(IngenIA365ERP.Application.Common.NationalValues.IValoresNacionales)));
        Assert.Equal(["LectorDeDocumentoNormativo"], Implementaciones(typeof(IngenIA365ERP.Application.Common.NationalValues.INormativaVigente)));
        Assert.Equal(["CalendarioDeDiasHabiles"], Implementaciones(typeof(IngenIA365ERP.Application.Common.Calendar.ICalendarioHabil)));
    }
}
