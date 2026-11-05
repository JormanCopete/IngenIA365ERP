using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// T022 (feature 016, US1; constitución, Principio V): la plantilla de personas crea por <c>PersonFactory</c>, como el alta unitaria,
/// y nunca escribe las banderas derivadas <c>IsAssociate</c>, <c>IsEmployee</c> ni <c>IsSalesperson</c> —las escribe el handler que
/// crea la fila hija—. Una importación que las encendiera repetiría el defecto que la feature 008 cerró en <c>UpdatePersonCommand</c>.
/// </summary>
public class LaPlantillaDePersonasUsaElAltaUnitaria
{
    private static readonly string Importacion = Path.Combine(RepoPath.FindRepoRoot(),
        "src", "Core", "IngenIA365ERP.Application", "Core", "People", "Imports", "ImportPeopleCommand.cs");

    [Fact]
    public void No_escribe_las_banderas_derivadas()
    {
        var codigo = File.ReadAllText(Importacion);

        Assert.False(Regex.IsMatch(codigo, @"\b(IsAssociate|IsEmployee|IsSalesperson)\s*="),
            "La plantilla de personas escribe una bandera de asociado, empleado o vendedor: las escribe sólo el handler de su fila hija.");
    }

    [Fact]
    public void Crea_por_PersonFactory_y_no_arma_la_persona_a_mano()
    {
        var codigo = File.ReadAllText(Importacion);

        Assert.False(Regex.IsMatch(codigo, @"\bnew\s+Person\b"), "La persona nueva la arma PersonFactory, como el alta unitaria.");
        Assert.Contains("personas.Construir(", codigo);
        Assert.Contains("new PersonInputValidator()", codigo);
    }
}
