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

    /// <summary>
    /// Los <c>.cs</c> de <c>src/</c>, sin <c>obj</c>/<c>bin</c> ni las fotos del modelo que genera EF en los
    /// proyectos de migraciones (<c>*.Designer.cs</c> y <c>*ModelSnapshot.cs</c>).
    /// <para>
    /// Esas fotos son ~375 de los ~383 MB de C# de los proyectos de migraciones (102 migraciones por proveedor,
    /// cada <c>Designer</c> de 2-3 MB) y ninguna regla las necesita por este recorrido: repiten el modelo de
    /// <c>Configurations/</c>, y el código de cada migración (<c>Up</c>/<c>Down</c>) se sigue recorriendo. Las
    /// reglas que miran las fotos a propósito (<c>LaCarteraHeredadaNoVuelve</c>, <c>LaTesoreriaHeredadaNoVuelve</c>,
    /// <c>LasMigracionesCongeladasNoSeRegeneran</c>…) abren su carpeta directamente. Hasta el 2026-11-14 entraban
    /// aquí: decenas de reglas en paralelo leían 383 MB cada una, cada foto es un string del montón de objetos
    /// grandes, y una corrida de esta suite llegaba a 20-37 GB (avisos de «memoria virtual insuficiente» de
    /// Windows) o se cortaba por el reloj de 45 min del CI.
    /// </para>
    /// </summary>
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
            if (EsFotoDelModelo(file))
                continue;
            yield return file;
        }
    }

    /// <summary>Un <c>*.Designer.cs</c> o <c>*ModelSnapshot.cs</c> de un proyecto de migraciones EF.</summary>
    public static bool EsFotoDelModelo(string archivo) =>
        archivo.Contains("Persistence.Migrations.", StringComparison.OrdinalIgnoreCase)
        && (archivo.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            || archivo.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Feature 013 (T0008): las pantallas <c>.razor</c> de producción, con las mismas exclusiones que
    /// <see cref="ProductionCSharpFiles"/>; un <c>catch</c> vacío en un bloque <c>@code</c> calla el error igual.
    /// </summary>
    public static IEnumerable<string> ProductionRazorFiles()
    {
        var root = FindRepoRoot();
        var src = Path.Combine(root, "src");
        if (!Directory.Exists(src)) yield break;

        foreach (var file in Directory.EnumerateFiles(src, "*.razor", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
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
