using Xunit;

namespace IngenIA365ERP.Load.Tests;

/// <summary>
/// Marca el proyecto como ejecutable por xUnit hasta que aterricen los escenarios reales (T131-T133).
/// El test es trivial y solo asegura que el runner no falle por falta de tests al ejecutar la suite.
/// </summary>
public sealed class PlaceholderFact
{
    [Fact]
    public void Suite_is_wired()
    {
        Assert.True(true);
    }
}
