using IngenIA365ERP.Architecture.Tests.Helpers;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio V — Person centralizada (<c>COR_People</c>). Las demás
/// entidades del dominio referencian a <c>PersonId</c> en vez de
/// duplicar nombre, documento, dirección, etc. Esta versión del check
/// detecta entidades NUEVAS (creadas después de la línea base) que declaren
/// simultáneamente <c>FirstName</c>/<c>LastName</c>/<c>IdentificationNumber</c>
/// — un olor típico a duplicación de datos personales.
/// </summary>
public class PrincipioV_PersonCentralizada
{
    private static readonly Regex FirstName = new(@"\bstring\??\s+FirstName\b", RegexOptions.Compiled);
    private static readonly Regex LastName = new(@"\bstring\??\s+LastName\b", RegexOptions.Compiled);
    private static readonly Regex Identification = new(@"\bstring\??\s+IdentificationNumber\b", RegexOptions.Compiled);

    // Entidades de baseline conocidas que mantienen estos campos por
    // razones legítimas (Person misma, Associates legacy, etc.).
    private static readonly string[] Allowlist =
    [
        "Domain/Entities/Core/Person.cs",
        "Domain/Entities/Security/User.cs",
        // Más allowlist se añade aquí cuando aparezca una excepción aprobada.
    ];

    [Fact]
    public void No_new_entity_should_duplicate_full_person_fields()
    {
        var root = RepoPath.FindRepoRoot();
        var offenders = RepoPath.ProductionCSharpFiles()
            .Where(f => f.Contains($"{Path.DirectorySeparatorChar}Domain{Path.DirectorySeparatorChar}Entities{Path.DirectorySeparatorChar}",
                                   StringComparison.OrdinalIgnoreCase))
            .Where(f =>
            {
                var rel = f.Replace(root, string.Empty).Replace('\\', '/').TrimStart('/');
                return !Allowlist.Any(a => rel.EndsWith(a, StringComparison.OrdinalIgnoreCase));
            })
            .Where(f =>
            {
                var content = File.ReadAllText(f);
                return FirstName.IsMatch(content)
                    && LastName.IsMatch(content)
                    && Identification.IsMatch(content);
            })
            .Select(f => f.Replace(root, string.Empty))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Entidades nuevas duplican datos de Person (deberían apuntar a PersonId):\n  "
            + string.Join("\n  ", offenders));
    }
}
