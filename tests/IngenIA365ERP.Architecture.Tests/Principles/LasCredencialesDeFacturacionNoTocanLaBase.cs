using System.Reflection;
using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 y T669 (decisiones-transversales T40, §2.18; contracts/dian.md §11), FR-063: las credenciales de facturación
/// electrónica (certificado de firma, su clave, el token del proveedor) viven en el Secret <c>erp-fe-credenciales</c> y se leen por
/// <c>CredencialesEnArchivo</c> con una ruta derivada sólo de la cooperativa resuelta y el canal. Nunca entran a la base: una copia de la
/// base no debe bastar para firmar a nombre de la cooperativa.
/// <list type="bullet">
/// <item>ninguna propiedad de <c>Domain/Entities/ElectronicInvoicing/**</c> se llama como un secreto (<c>Password</c>, <c>Token</c>,
/// <c>Secret</c>, <c>Pin</c>, <c>Certificate</c>);</item>
/// <item><c>CredentialKey</c> (el <b>nombre</b> de la clave del Secret) y <c>TechnicalKey</c> (la clave técnica de la resolución) llevan
/// <see cref="NoAuditarAttribute"/>: no viajan al diff de la auditoría;</item>
/// <item><c>CredencialesEnArchivo</c> arma la ruta con <c>CredencialesDeCanal.ClaveDe(cooperativa, canal)</c>, nunca con la
/// <c>CredentialKey</c> leída de la base (que sólo se compara).</item>
/// </list>
/// </summary>
public class LasCredencialesDeFacturacionNoTocanLaBase
{
    /// <summary>Fragmentos de nombre que delatan un secreto.</summary>
    private static readonly string[] NombresDeSecreto = ["Password", "Token", "Secret", "Pin", "Certificate"];

    private const string EspacioDeEntidades = "IngenIA365ERP.Domain.Entities.ElectronicInvoicing";

    private static IReadOnlyList<Type> Entidades() =>
        typeof(BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true } && t.Namespace is { } ns
                        && (ns == EspacioDeEntidades || ns.StartsWith(EspacioDeEntidades + ".", StringComparison.Ordinal)))
            .ToList();

    [Fact]
    public void Ninguna_entidad_de_facturacion_electronica_tiene_una_propiedad_de_secreto()
    {
        var entidades = Entidades();
        Assert.True(entidades.Count >= 7, "No se encontraron las entidades de facturación electrónica: ¿se movió el espacio de nombres?");

        var infractores = entidades
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => (Tipo: t, Propiedad: p)))
            .Where(x => NombresDeSecreto.Any(n => Regex.IsMatch(x.Propiedad.Name, $@"(^|[a-z]){n}([A-Z]|s?$)")))
            .Select(x => $"{x.Tipo.Name}.{x.Propiedad.Name}")
            .ToList();

        Assert.True(infractores.Count == 0,
            "Propiedades con nombre de secreto en las entidades de facturación electrónica (T40): las credenciales van en el Secret:\n  "
            + string.Join("\n  ", infractores));
    }

    [Theory]
    [InlineData("ElectronicEmissionSetting", "CredentialKey")]
    [InlineData("DianResolutionChannel", "TechnicalKey")]
    public void Las_claves_no_viajan_a_la_auditoria(string entidad, string propiedad)
    {
        var tipo = Entidades().SingleOrDefault(t => t.Name == entidad);
        Assert.NotNull(tipo);
        var info = tipo!.GetProperty(propiedad);
        Assert.NotNull(info);
        Assert.True(info!.IsDefined(typeof(NoAuditarAttribute), inherit: true), $"{entidad}.{propiedad} debe llevar [NoAuditar].");
    }

    [Fact]
    public void CredencialesEnArchivo_arma_la_ruta_solo_con_la_cooperativa_y_el_canal()
    {
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Infrastructure", "IngenIA365ERP.ElectronicInvoicing", "Credentials", "CredencialesEnArchivo.cs");
        Assert.True(File.Exists(archivo), "No existe CredencialesEnArchivo.cs: si se movió, actualizá la prueba.");
        var texto = FuenteSinComentarios.Leer(archivo);

        var combinaciones = Regex.Matches(texto, @"Path\.Combine\((?<args>[^;]*?)\)\s*\)?\s*;").Select(m => m.Groups["args"].Value).ToList();
        Assert.NotEmpty(combinaciones);

        var infractores = new List<string>();
        foreach (var args in combinaciones)
        {
            if (args.Contains("CredentialKey", StringComparison.Ordinal))
                infractores.Add($"Path.Combine({args}): usa la CredentialKey guardada");

            foreach (var identificador in Regex.Matches(args, @"\b[a-z_]\w*\b").Select(m => m.Value).Distinct())
            {
                var asignacion = Regex.Match(texto, $@"\bvar\s+{Regex.Escape(identificador)}\s*=(?<valor>[^;]*);", RegexOptions.Singleline);
                if (!asignacion.Success) continue;
                var valor = asignacion.Groups["valor"].Value;
                if (valor.Contains("CredentialKey", StringComparison.Ordinal))
                    infractores.Add($"{identificador}: sale de la CredentialKey de la base");
            }
        }

        Assert.Matches(@"\bvar\s+clave\s*=\s*CredencialesDeCanal\.ClaveDe\(", texto);
        Assert.Contains("ICurrentTenantService", texto);
        Assert.True(infractores.Count == 0,
            "La ruta de la credencial no se deriva sólo de la cooperativa y el canal (contracts/dian.md §11, T40):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Ninguna_credencial_de_facturacion_se_mapea_a_la_base()
    {
        // Los tipos que llevan secretos en memoria (Application: CredencialesDeCanal) nunca se mapean ni tienen DbSet.
        var tipos = new[] { nameof(Application.ElectronicInvoicing.Channels.CredencialesDeCanal) };
        var root = RepoPath.FindRepoRoot();
        var persistencia = RepoPath.ProductionCSharpFiles()
            .Where(f => f.Replace('\\', '/').Contains("/IngenIA365ERP.Persistence", StringComparison.Ordinal)
                     || f.Replace('\\', '/').Contains("/IngenIA365ERP.Domain/Entities/", StringComparison.Ordinal)
                     || Path.GetFileName(f) == "IApplicationDbContext.cs")
            .ToList();
        var infractores = new List<string>();

        foreach (var tipo in tipos)
        {
            var mapeo = new Regex(
                $@"DbSet<{Regex.Escape(tipo)}>|IEntityTypeConfiguration<{Regex.Escape(tipo)}>|Entity<{Regex.Escape(tipo)}>|\bclass\s+{Regex.Escape(tipo)}\b",
                RegexOptions.Compiled);
            foreach (var archivo in persistencia)
            {
                if (mapeo.IsMatch(File.ReadAllText(archivo)))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: {tipo} llega a la base");
            }
        }

        Assert.True(infractores.Count == 0,
            "Credenciales de facturación electrónica en la base (FR-063, T40):\n  " + string.Join("\n  ", infractores));
    }
}
