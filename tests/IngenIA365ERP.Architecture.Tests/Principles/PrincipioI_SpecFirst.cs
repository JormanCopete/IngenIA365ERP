using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio I — Spec-First (Constitution → Spec → Plan → Tasks).
///
/// Es un principio de proceso (no de código). El único check mecánico
/// posible es verificar que los artefactos `.specify/memory/constitution.md`
/// y al menos un `specs/*/spec.md` existen — si no existen, el flujo
/// Spec-Kit no se está siguiendo.
/// </summary>
public class PrincipioI_SpecFirst
{
    [Fact]
    public void Constitution_should_exist()
    {
        var root = RepoPath.FindRepoRoot();
        var constitution = Path.Combine(root, ".specify", "memory", "constitution.md");
        Assert.True(File.Exists(constitution), $"No se encontró la constitución en {constitution}");
    }

    [Fact]
    public void At_least_one_feature_spec_should_exist()
    {
        var root = RepoPath.FindRepoRoot();
        var specsRoot = Path.Combine(root, "specs");
        Assert.True(Directory.Exists(specsRoot), "No existe la carpeta specs/");
        var anySpec = Directory.EnumerateFiles(specsRoot, "spec.md", SearchOption.AllDirectories).Any();
        Assert.True(anySpec, "No se encontró ningún spec.md bajo specs/");
    }
}
