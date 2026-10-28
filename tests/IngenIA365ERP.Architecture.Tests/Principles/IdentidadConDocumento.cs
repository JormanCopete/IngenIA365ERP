using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Common.Identidad;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 017 (research R2, R3, R5, R15): las reglas que hacen que el usuario de documento sea
/// inequívoco y que nadie lo esquive.
/// </summary>
public class IdentidadConDocumento
{
    private static IEnumerable<(string Relativo, string Texto)> Fuentes()
    {
        var root = RepoPath.FindRepoRoot();
        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
            if (relativo.Contains("/Migrations", StringComparison.Ordinal)) continue;
            yield return (relativo, FuenteSinComentarios.Leer(archivo));
        }
    }

    /// <summary>
    /// T007: el usuario se lee sin ambigüedad. El prefijo vacío (cédula) sólo admite dígitos; los demás
    /// empiezan por letra y ninguno es el comienzo de otro, así que «PA…» nunca se confunde con «PPT…».
    /// </summary>
    [Fact]
    public void LosPrefijosDelUsuarioNoSonPrefijoDeOtro()
    {
        var conPrefijo = TiposDeDocumento.Todos.Where(t => t.Prefijo.Length > 0).ToList();
        foreach (var a in conPrefijo)
        foreach (var b in conPrefijo)
        {
            if (ReferenceEquals(a, b)) continue;
            Assert.False(b.Prefijo.StartsWith(a.Prefijo, StringComparison.Ordinal),
                $"El prefijo {a.Prefijo} ({a.Codigo}) es el comienzo de {b.Prefijo} ({b.Codigo}).");
        }

        Assert.All(conPrefijo, t => Assert.True(char.IsAsciiLetterUpper(t.Prefijo[0]), $"{t.Codigo} debe empezar por letra."));
        var sinPrefijo = TiposDeDocumento.Todos.Where(t => t.Prefijo.Length == 0).ToList();
        Assert.Single(sinPrefijo);
        Assert.False(sinPrefijo[0].AdmiteLetras, "El tipo sin prefijo sólo puede llevar dígitos.");
    }

    /// <summary>
    /// T008: nadie fuera de <c>UsuarioDeDocumento</c> arma el usuario ni escribe <c>DocumentUsername</c>
    /// por su cuenta. Las migraciones repiten la regla en SQL y quedan fuera (las vigila su e2e).
    /// </summary>
    [Fact]
    public void ElUsuarioSeDerivaEnUnSoloSitio()
    {
        string[] escritoresDeLaCuenta =
        [
            "src/Core/IngenIA365ERP.Domain/Entities/Admin/CentralUser.cs",
            "src/Infrastructure/IngenIA365ERP.Identity/CentralIdentity/AspNetCoreIdentityProvider.cs",
        ];
        // Una asignación, no un parámetro con valor por defecto («string? DocumentUsername = null»), una
        // comparación ni un miembro de enum («DocumentUsername = 1»).
        var escribeCuenta = new Regex(@"(?<![?\w]\s)\bDocumentUsername\s*=(?!=)(?!\s*(null\b|\d))", RegexOptions.Compiled);
        // Concatenar el prefijo de un tipo del catálogo con un número (otros «Prefijo» del sistema no cuentan).
        var armaPrefijo = new Regex(@"\b[Tt]ipo\w*\.Prefijo\s*\+", RegexOptions.Compiled);

        var infractores = new List<string>();
        foreach (var (relativo, texto) in Fuentes())
        {
            if (escribeCuenta.IsMatch(texto) && !escritoresDeLaCuenta.Contains(relativo)
                && !relativo.EndsWith("/AspNetCoreIdentityProvider.cs", StringComparison.Ordinal))
                infractores.Add($"{relativo}: escribe DocumentUsername");
            if (armaPrefijo.IsMatch(texto) && !relativo.EndsWith("/UsuarioDeDocumento.cs", StringComparison.Ordinal))
                infractores.Add($"{relativo}: concatena el prefijo del tipo de documento");
        }

        Assert.True(infractores.Count == 0,
            "El usuario de documento se deriva sólo en UsuarioDeDocumento (feature 017):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// T032 (research R3): desde que CC 123456 y CE 123456 conviven, buscar o indexar personas por el
    /// número solo es un defecto (devuelve la equivocada o lanza al armar un diccionario). Se busca por
    /// <c>ResolutorDePersonaPorDocumento</c>. Quedan fuera las comparaciones que no son de personas
    /// (NIT de empresas, consignante de un extracto) y la propia fábrica de personas.
    /// </summary>
    [Fact]
    public void NadieBuscaPersonasPorNumeroSolo()
    {
        string[] autorizados =
        [
            "/ResolutorDePersonaPorDocumento.cs",
            "/PersonFactory.cs",
            "/UpdatePersonCommand.cs",
            // Empresas (COR_Companies), no personas.
            "/CreateCompanyCommand.cs",
            "/UpdateCompanyCommand.cs",
            // Compara también el tipo (DIAN), y los grupos de descuento por empresa empleadora.
            "/ConstructorDelCanonico.cs",
            "/LineasDeGrupoDeDescuento.cs",
        ];
        var compara = new Regex(@"\.TaxId\s*==|==\s*[\w.]*\.TaxId\b", RegexOptions.Compiled);
        var indexa = new Regex(@"\b(ToDictionary|ToLookup|GroupBy)\s*\(\s*\w+\s*=>\s*\w+\.TaxId\b", RegexOptions.Compiled);

        var infractores = new List<string>();
        foreach (var (relativo, texto) in Fuentes())
        {
            if (!relativo.StartsWith("src/Core/", StringComparison.Ordinal)
                && !relativo.StartsWith("src/Infrastructure/", StringComparison.Ordinal)) continue;
            if (autorizados.Any(a => relativo.EndsWith(a, StringComparison.Ordinal))) continue;
            if (compara.IsMatch(texto) || indexa.IsMatch(texto))
                infractores.Add(relativo);
        }

        Assert.True(infractores.Count == 0,
            "Las personas se buscan por documento con ResolutorDePersonaPorDocumento (feature 017, R3):\n  "
            + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// T040 (research R5): «Correo o usuario» se resuelve en un solo sitio, que exige el correo
    /// verificado. Buscar la cuenta por correo en otro lado saltaría esa regla. Lo que queda autorizado
    /// no es un ingreso: crear o anclar invitaciones (el correo es su destino), sembrar, el propio
    /// proveedor. Las excepciones temporales son las que retira la US7/US8 (T073, T080, T081).
    /// </summary>
    [Fact]
    public void NadieBuscaCuentasPorCorreoFueraDelResolutor()
    {
        string[] autorizados =
        [
            "/ResolutorDeIdentificador.cs",
            "/ICentralIdentityProvider.cs",
            "/AspNetCoreIdentityProvider.cs",
            "/IdentitySeedData.cs",
            "/MasterAdminSeeder.cs",
            "/EmisionDeInvitacion.cs",
            "/PreviewInvitationQueryHandler.cs",
            "/AcceptInvitationCommandHandler.cs",
            // Temporales: los retiran T073 (DisableUser), T080 (AdminResetPassword) y T081 (forgot).
            "/DisableUserCommandHandler.cs",
            "/AdminResetPasswordCommandHandler.cs",
            "/RequestPasswordResetCommandHandler.cs",
        ];
        var busca = new Regex(@"\bFindByEmailAsync\s*\(", RegexOptions.Compiled);

        var infractores = Fuentes()
            .Where(f => busca.IsMatch(f.Texto) && !autorizados.Any(a => f.Relativo.EndsWith(a, StringComparison.Ordinal)))
            .Select(f => f.Relativo)
            .ToList();

        Assert.True(infractores.Count == 0,
            "Las cuentas se buscan por «Correo o usuario» con ResolutorDeIdentificador (feature 017, R5):\n  "
            + string.Join("\n  ", infractores));
    }
}
