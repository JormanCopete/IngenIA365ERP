using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 020 (T031; R1; FR-002, SC-007): toda ruta de un módulo habilitable cuelga de un grupo con la puerta del módulo
/// (<c>RequireModulo</c>, el <c>FiltroDeModuloHabilitado</c>), así que con el módulo apagado responde 403 <c>Module.NotEnabled</c> para todos,
/// el maestro incluido. Un archivo de <c>Endpoints/Transport</c> o el centro de informes de Transporte que arme su grupo con
/// <c>MapGroup</c> a mano y sin la puerta abriría el módulo a una cooperativa que no lo tiene. Es una prueba sobre el fuente porque una
/// ruta sin puerta no rompe ninguna prueba de unidad.
/// </summary>
public class LosModulosHabilitablesTienenSuFiltro
{
    private static string Api => Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API");

    internal static IEnumerable<string> Archivos()
    {
        var transporte = Path.Combine(Api, "Endpoints", "Transport");
        if (Directory.Exists(transporte))
        {
            foreach (var f in Directory.EnumerateFiles(transporte, "*.cs").OrderBy(f => f, StringComparer.Ordinal))
                yield return Path.GetRelativePath(Api, f);
        }

        var informes = Path.Combine(Api, "Endpoints", "Reports");
        if (Directory.Exists(informes))
        {
            foreach (var f in Directory.EnumerateFiles(informes, "Transport*.cs").OrderBy(f => f, StringComparer.Ordinal))
                yield return Path.GetRelativePath(Api, f);
        }
    }

    public static TheoryData<string> LosArchivos => [.. Archivos()];

    [Theory]
    [MemberData(nameof(LosArchivos))]
    public void Cada_ruta_de_Transporte_pasa_por_la_puerta_del_modulo(string archivo)
    {
        var fuente = FuenteSinComentarios.Leer(Path.Combine(Api, archivo));
        var publicaRutas = fuente.Contains(".MapGet(", StringComparison.Ordinal) || fuente.Contains(".MapPost(", StringComparison.Ordinal)
                           || fuente.Contains(".MapPut(", StringComparison.Ordinal) || fuente.Contains(".MapDelete(", StringComparison.Ordinal)
                           || fuente.Contains("MapPlantilla(", StringComparison.Ordinal);
        if (!publicaRutas) return;

        var usaElGrupo = fuente.Contains("TransportGroup.Grupo(", StringComparison.Ordinal);
        var ponePuerta = fuente.Contains(".RequireModulo(", StringComparison.Ordinal);
        Assert.True(usaElGrupo || ponePuerta,
            $"{archivo} publica rutas de Transporte: tienen que colgar de TransportGroup.Grupo o de un grupo con RequireModulo(\"TRN\")");

        if (fuente.Contains("app.MapGroup(", StringComparison.Ordinal) && !Path.GetFileName(archivo).Equals("TransportGroup.cs", StringComparison.Ordinal))
        {
            Assert.True(ponePuerta, $"{archivo} arma un grupo propio con MapGroup: tiene que llevar .RequireModulo(\"TRN\")");
        }
    }

    [Fact]
    public void El_grupo_base_de_Transporte_lleva_la_puerta()
    {
        var fuente = FuenteSinComentarios.Leer(Path.Combine(Api, "Endpoints", "Transport", "TransportGroup.cs"));
        Assert.Contains(".RequireModulo(ModulosHabilitables.Transporte)", fuente, StringComparison.Ordinal);
    }
}
