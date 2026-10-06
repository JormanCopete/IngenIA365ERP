using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T39, I-3): el registro central de valores nacionales (base administrativa) sólo lo lee
/// el replicador; ningún cálculo lo lee de una base compartida.
///
/// <para>
/// Llenada en la fase 2 (T0036): (1) fuera de <c>Application/Saas/NationalValues</c> (los comandos del maestro), de Persistence (el
/// contexto administrativo, sus configuraciones, la semilla central y la lectura <c>RegistroCentralDeValoresNacionales</c>) y de la ruta
/// del maestro, nadie nombra las entidades <c>Platform*</c> ni las tablas <c>ADM_NationalValues</c>/<c>ADM_RegulatoryDocuments</c>;
/// (2) del puerto <c>IRegistroCentralDeValoresNacionales</c> sólo dependen el núcleo de la réplica y quien lo compone (la semilla
/// del alta, Order 88, que corre el mismo núcleo).
/// </para>
/// </summary>
public class SoloElReplicadorLeeElRegistroCentral
{
    /// <summary>Carpetas revisadas (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        "Core/IngenIA365ERP.Application",
        "Presentation",
        "Infrastructure/IngenIA365ERP.ExternalChannels",
        "Infrastructure/IngenIA365ERP.ElectronicInvoicing",
        "Infrastructure/IngenIA365ERP.Identity",
        "Infrastructure/IngenIA365ERP.Audit",
    ];

    /// <summary>Prefijos exentos (relativos a la raíz): los comandos y consultas del maestro y su ruta.</summary>
    private static readonly string[] PrefijosExentos =
    [
        "src/Core/IngenIA365ERP.Application/Saas/NationalValues/",
        "src/Presentation/IngenIA365ERP.API/Endpoints/Saas/NationalValuesEndpoints.cs",
        // Declaran el contexto administrativo y el puerto de lectura de la central.
        "src/Core/IngenIA365ERP.Application/Common/Interfaces/IAdminDbContext.cs",
        "src/Core/IngenIA365ERP.Application/Common/NationalValues/IRegistroCentralDeValoresNacionales.cs",
    ];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bPlatformNationalValues?\b|\bPlatformRegulatoryDocuments?\b|\""ADM_(NationalValues|RegulatoryDocuments)", RegexOptions.Compiled), "lee el registro central"),
    ];

    /// <summary>Quién puede depender del puerto de lectura de la central (nombre de archivo).</summary>
    private static readonly string[] DependenDelPuerto =
    [
        "IRegistroCentralDeValoresNacionales.cs",
        "ReplicacionDeValoresNacionales.cs",
        "RegistroCentralDeValoresNacionales.cs",
        "NationalValuesReplicaSeeder.cs",
        "DependencyInjection.cs",
    ];

    [Fact]
    public void Solo_el_replicador_lee_la_central()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => PrefijosExentos.Any(p => relativo.StartsWith(p, StringComparison.Ordinal)));
        ReglaSobreFuente.SinInfracciones(infracciones, "Alguien lee el registro central fuera del replicador (I-3)");
    }

    [Fact]
    public void Solo_la_replica_depende_del_puerto_de_la_central()
    {
        var root = RepoPath.FindRepoRoot();
        var uso = new Regex(@"\bIRegistroCentralDeValoresNacionales\b", RegexOptions.Compiled);
        var usan = RepoPath.ProductionCSharpFiles()
            .Where(f => uso.IsMatch(FuenteSinComentarios.Leer(f)))
            .ToList();

        var infractores = usan.Where(f => !DependenDelPuerto.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Select(f => Path.GetRelativePath(root, f)).ToList();
        Assert.True(infractores.Count == 0,
            "Sólo la réplica lee la central por IRegistroCentralDeValoresNacionales (T39):\n  " + string.Join("\n  ", infractores));
        Assert.Contains(usan, f => Path.GetFileName(f) == "ReplicacionDeValoresNacionales.cs");

        // La implementación sólo lee: no guarda en la base administrativa.
        var implementacion = usan.Single(f => Path.GetFileName(f) == "RegistroCentralDeValoresNacionales.cs");
        Assert.DoesNotContain("SaveChanges", FuenteSinComentarios.Leer(implementacion), StringComparison.Ordinal);
    }
}
