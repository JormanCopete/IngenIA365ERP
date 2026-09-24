using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Ninguna contraseña escrita en el código de producción. Hasta el 2026-09-23 la fábrica de diseño de
/// PostgreSQL (<c>DesignTimeFactories.cs</c>) traía, como cadena por defecto, la de la base de desarrollo:
/// quedó en el historial de git y cualquiera con acceso al repositorio la tenía. Las cadenas con
/// contraseña vienen de la configuración o del entorno (<c>DB_DESIGN_CONNSTR</c>, los Secrets del
/// clúster), nunca de un literal.
///
/// <para>
/// Mira sólo lo que está <b>dentro de un literal de cadena</b> con la forma de una cadena de conexión
/// (<c>Password=algo</c>, <c>Pwd=algo</c>): una asignación <c>Password = request.Password</c> no es una
/// contraseña, ni <c>MustChangePassword=true</c>, ni un hueco interpolado (<c>Password={clave}</c>).
/// </para>
/// </summary>
public class NoHayContrasenasEnElCodigo
{
    internal static readonly Regex ContrasenaEnUnLiteral = new(
        @"""[^""\n]*\b(?:Password|Pwd)\s*=\s*(?![;""{])[^;""\n]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void Ningun_literal_de_src_lleva_una_contrasena()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infractores = RepoPath.ProductionCSharpFiles()
            .SelectMany(f => File.ReadLines(f).Select((linea, i) => (Archivo: f, Linea: i + 1, Texto: linea)))
            .Where(x => ContrasenaEnUnLiteral.IsMatch(x.Texto))
            .Select(x => $"{Path.GetRelativePath(raiz, x.Archivo)}:{x.Linea}")
            .ToList();

        Assert.True(infractores.Count == 0,
            "Contraseñas escritas en el código de producción: van en la configuración o en el entorno, nunca en un literal.\n  " +
            string.Join("\n  ", infractores));
    }

    [Theory]
    [InlineData("var c = \"Host=localhost;Username=ingenia;Password=unaclave\";", true)]
    [InlineData("var c = \"Server=x;User Id=sa;Pwd=otra;\";", true)]
    [InlineData("var c = \"Host=localhost;Database=erp;Username=ingenia\";", false)]
    [InlineData("usuario.Password = request.Password;", false)]
    [InlineData("\"MustChangePassword=true\"", false)]
    [InlineData("$\"Host=h;Password={clave}\"", false)]
    [InlineData("\"Password=;\"", false)]
    public void El_detector_distingue_una_contrasena_de_lo_que_se_le_parece(string linea, bool esContrasena) =>
        Assert.Equal(esContrasena, ContrasenaEnUnLiteral.IsMatch(linea));
}
