namespace IngenIA365ERP.Architecture.Tests.Helpers;

/// <summary>
/// Resuelve la raíz del repositorio desde el directorio del runner de tests
/// (<c>bin/Release/net10.0/</c>) buscando hacia arriba el archivo
/// <c>IngenIA365ERP.slnx</c>. Sirve para los tests que escanean código
/// fuente (no IL) — empty catch, raw DELETE, idempotencia de migraciones.
/// </summary>
internal static class RepoPath
{
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IngenIA365ERP.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("No se encontró IngenIA365ERP.slnx al subir desde " + AppContext.BaseDirectory);
    }

    public static IEnumerable<string> ProductionCSharpFiles()
    {
        var root = FindRepoRoot();
        var src = Path.Combine(root, "src");
        if (!Directory.Exists(src)) yield break;

        foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
        {
            // Excluir artefactos generados.
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            // Excluir el proyecto Legacy — código importado del VB.NET original con sus propias heurísticas.
            if (file.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Legacy{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return file;
        }
    }

    public static IEnumerable<string> MigrationSqlFiles()
    {
        var root = FindRepoRoot();
        var mig = Path.Combine(root, "database", "migration");
        if (!Directory.Exists(mig)) yield break;
        foreach (var file in Directory.EnumerateFiles(mig, "*.sql"))
            yield return file;
    }
}
