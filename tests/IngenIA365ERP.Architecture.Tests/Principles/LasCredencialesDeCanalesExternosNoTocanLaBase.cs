using System.Reflection;
using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T72; contracts/adaptadores-y-archivos.md §3.4): las credenciales de los canales externos van en el Secret
/// <c>erp-canales-externos</c> montado en archivo; la base sólo guarda la clave que las nombra. Molde:
/// <c>LasCredencialesDeFacturacionNoTocanLaBase</c>.
/// <list type="bullet">
/// <item>ninguna propiedad <c>Password</c>, <c>Token</c>, <c>Secret</c>, <c>ApiKey</c> o <c>Pin</c> en las entidades
/// <c>COR_ExternalChannel*</c> ni en las de Cartera (<c>LND_*</c>), ni en sus configuraciones;</item>
/// <item><c>ExternalChannelSetting.CredentialKey</c> lleva <see cref="NoAuditarAttribute"/>;</item>
/// <item><c>CredencialesDeCanalExternoEnArchivo</c> arma la ruta con <c>CredencialDeCanalExterno.ClaveDe(cooperativa, canal)</c> y la
/// cooperativa resuelta, nunca con la <c>CredentialKey</c> leída de la base (que sólo se compara);</item>
/// <item><c>CredencialDeCanalExterno</c> nunca se mapea a la base.</item>
/// </list>
/// Lista llenada por T0038 (adaptadores): la entidad y la configuración de T0080 y la carpeta de entidades de Cartera, que crece con cada
/// fase.
/// </summary>
public class LasCredencialesDeCanalesExternosNoTocanLaBase
{
    /// <summary>Carpetas de entidades y configuraciones de canales y de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        "Core/IngenIA365ERP.Domain/Entities/Lending",
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Lending",
    ];

    /// <summary>Archivos de canales externos fuera de esas carpetas (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        "Core/IngenIA365ERP.Domain/Entities/Core/ExternalChannelSetting.cs",
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Core/ExternalChannelSettingConfiguration.cs",
    ];

    /// <summary>
    /// Archivos exentos (rutas relativas a la raíz, con barras normales). Estaba <c>Cashier</c> (su <c>PasswordTimeout</c> es un
    /// tiempo de espera de la caja, no un secreto): <c>LND_Cashiers</c> se conservó en la feature 013 (T0184) fuera de Cartera, así
    /// que salió de aquí, y la feature 014 la retiró (T007, R28).
    /// </summary>
    private static readonly string[] Exentos = [];

    /// <summary>Los tipos de esos archivos, para la prueba por reflexión.</summary>
    private static readonly string[] TiposExentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bpublic\s+\w+\??\s+\w*(Password|Token|Secret|ApiKey|Pin)\w*\s*\{", RegexOptions.Compiled), "una propiedad de secreto"),
    ];

    [Fact]
    public void Ninguna_entidad_de_canales_guarda_un_secreto()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).Concat(ReglaSobreFuente.ArchivosNombrados(Archivos)).ToList();
        Assert.NotEmpty(archivos);
        var infracciones = ReglaSobreFuente.Infracciones(archivos, Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Una credencial de canal externo toca la base (T72)");
    }

    [Fact]
    public void Ninguna_entidad_de_canales_externos_tiene_una_propiedad_de_secreto_por_reflexion()
    {
        var tipos = typeof(BaseEntity).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, Namespace: not null }
                        && (t.Name.StartsWith("ExternalChannel", StringComparison.Ordinal)
                            || t.Namespace.StartsWith("IngenIA365ERP.Domain.Entities.Lending", StringComparison.Ordinal))
                        && !TiposExentos.Contains(t.Name))
            .ToList();
        Assert.Contains(tipos, t => t.Name == "ExternalChannelSetting");

        var infractores = tipos
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => (Tipo: t, Propiedad: p)))
            .Where(x => new[] { "Password", "Token", "Secret", "ApiKey" }.Any(n => Regex.IsMatch(x.Propiedad.Name, $@"(^|[a-z]){n}([A-Z]|s?$)")))
            .Select(x => $"{x.Tipo.Name}.{x.Propiedad.Name}")
            .ToList();

        Assert.True(infractores.Count == 0, "Propiedades con nombre de secreto en entidades de canales o de Cartera (T72):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void La_clave_de_la_credencial_no_viaja_a_la_auditoria()
    {
        var propiedad = typeof(Domain.Entities.Core.ExternalChannelSetting).GetProperty(nameof(Domain.Entities.Core.ExternalChannelSetting.CredentialKey));

        Assert.NotNull(propiedad);
        Assert.True(propiedad!.IsDefined(typeof(NoAuditarAttribute), inherit: true), "ExternalChannelSetting.CredentialKey debe llevar [NoAuditar].");
    }

    [Fact]
    public void CredencialesDeCanalExternoEnArchivo_arma_la_ruta_solo_con_la_cooperativa_y_el_canal()
    {
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Infrastructure", "IngenIA365ERP.ExternalChannels", "Credentials", "CredencialesDeCanalExternoEnArchivo.cs");
        Assert.True(File.Exists(archivo), "No existe CredencialesDeCanalExternoEnArchivo.cs: si se movió, actualizá la prueba.");
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
                if (asignacion.Groups["valor"].Value.Contains("CredentialKey", StringComparison.Ordinal))
                    infractores.Add($"{identificador}: sale de la CredentialKey de la base");
            }
        }

        Assert.Matches(@"\bvar\s+clave\s*=\s*CredencialDeCanalExterno\.ClaveDe\(", texto);
        Assert.Contains("CooperativaDelCanal.Resuelta(tenant)", texto);
        Assert.True(infractores.Count == 0,
            "La ruta de la credencial no se deriva sólo de la cooperativa y el canal (T72):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Ninguna_credencial_de_canal_externo_se_mapea_a_la_base()
    {
        var tipo = nameof(Application.Common.ExternalChannels.CredencialDeCanalExterno);
        var mapeo = new Regex($@"DbSet<{tipo}>|IEntityTypeConfiguration<{tipo}>|Entity<{tipo}>|\bclass\s+{tipo}\b", RegexOptions.Compiled);
        var root = RepoPath.FindRepoRoot();
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => f.Replace('\\', '/').Contains("/IngenIA365ERP.Persistence", StringComparison.Ordinal)
                        || f.Replace('\\', '/').Contains("/IngenIA365ERP.Domain/Entities/", StringComparison.Ordinal)
                        || Path.GetFileName(f) == "IApplicationDbContext.cs")
            .Where(f => mapeo.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(infractores.Count == 0, "Credenciales de canales externos en la base (T72):\n  " + string.Join("\n  ", infractores));
    }
}
