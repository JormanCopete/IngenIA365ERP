using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 016, US3 (constitución, Principio V): la plantilla de asociados afilia por <c>AssociateRegistrar</c>, como el alta unitaria. La
/// bandera <c>IsAssociate</c> la escribe quien crea la fila hija, y esa fila no se arma a mano ni se escribe una persona desde aquí.
/// </summary>
public class LaPlantillaDeAsociadosUsaElAltaUnitaria
{
    private static readonly string Importacion = Path.Combine(RepoPath.FindRepoRoot(),
        "src", "Core", "IngenIA365ERP.Application", "Core", "Associates", "Imports", "ImportAssociatesCommand.cs");

    [Fact]
    public void No_escribe_la_bandera_ni_arma_la_afiliacion_a_mano()
    {
        var codigo = File.ReadAllText(Importacion);

        Assert.False(Regex.IsMatch(codigo, @"\bIs(Associate|Employee|Salesperson)\s*="),
            "La plantilla de asociados escribe una bandera de rol: la escribe AssociateRegistrar al crear la fila hija.");
        Assert.False(Regex.IsMatch(codigo, @"\bnew\s+Associate\b"), "La afiliación nueva la arma AssociateRegistrar, como el alta unitaria.");
        Assert.Contains("asociados.Construir(", codigo);
        Assert.Contains("new AssociateInputValidator()", codigo);
    }

    [Fact]
    public void Nunca_crea_personas()
    {
        var codigo = File.ReadAllText(Importacion);

        Assert.False(Regex.IsMatch(codigo, @"\bnew\s+Person\b|PersonFactory|People\.Add"), "Esta plantilla no crea personas.");
    }

    [Fact]
    public void El_retiro_de_un_vigente_se_decide_con_la_regla_del_alta()
    {
        Assert.Contains("AssociateRegistrar.RetiroQueEsDeCartera(", File.ReadAllText(Importacion));
    }
}
