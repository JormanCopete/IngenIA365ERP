using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 028 (contracts/canal-conecta.md §3 y §11; plan.md D4): el <c>clientSecret</c> de Conecta sólo viaja a un host de la lista blanca de
/// <c>ElectronicInvoicing:Channels:{canal}:Endpoints</c>, que fija GitOps; nunca a una URL leída de la base. Por eso el adaptador:
/// <list type="bullet">
/// <item>no conoce la base (ni <c>IApplicationDbContext</c> ni las entidades de configuración): lo que necesita de la vigencia le llega en
/// <c>ContextoDeCanal.Canal</c>, como clave del extremo;</item>
/// <item>convierte texto en URL en un solo sitio, y ese texto sale de <c>Opciones.Endpoints</c>;</item>
/// <item>nunca registra el secreto ni el token.</item>
/// </list>
/// </summary>
public class ElSecretoDeConectaSoloViajaALaListaBlanca
{
    private static IReadOnlyList<(string Relativo, string Fuente)> FuentesDelAdaptador()
    {
        var root = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(root, "src", "Infrastructure", "IngenIA365ERP.ElectronicInvoicing", "Channels", "Conecta");
        Assert.True(Directory.Exists(carpeta), $"No está la carpeta del adaptador: {carpeta}");
        return Directory.GetFiles(carpeta, "*.cs", SearchOption.AllDirectories)
            .Select(a => (Path.GetRelativePath(root, a).Replace('\\', '/'), FuenteSinComentarios.Leer(a)))
            .ToList();
    }

    [Fact]
    public void El_adaptador_no_lee_la_base()
    {
        var prohibidos = new Regex(@"\b(IApplicationDbContext|DbContext|ElectronicEmissionSetting|DbSet<)\b", RegexOptions.Compiled);

        var infractores = FuentesDelAdaptador().Where(f => prohibidos.IsMatch(f.Fuente)).Select(f => f.Relativo).ToList();

        Assert.True(infractores.Count == 0, "El adaptador de Conecta no lee la base:\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_un_sitio_convierte_texto_en_URL_y_lo_toma_de_la_lista_blanca()
    {
        var conversiones = FuentesDelAdaptador()
            .SelectMany(f => Regex.Matches(f.Fuente, @"Uri\.TryCreate\(|new Uri\(\s*""|new Uri\(\s*[a-z]\w*\s*\)").Select(m => (f.Relativo, f.Fuente, m.Index)))
            .ToList();

        Assert.Single(conversiones);
        var (relativo, fuente, indice) = conversiones[0];
        Assert.EndsWith("CanalConecta.cs", relativo);
        var antes = fuente[Math.Max(0, indice - 400)..indice];
        Assert.Contains("Opciones.Endpoints", antes);
    }

    [Fact]
    public void Nunca_registra_el_secreto_ni_el_token()
    {
        var registro = new Regex(@"_logger\.Log\w*\([^;]*(ClientSecret|Secret|Token|clientSecret)", RegexOptions.Compiled | RegexOptions.Singleline);

        var infractores = FuentesDelAdaptador().Where(f => registro.IsMatch(f.Fuente)).Select(f => f.Relativo).ToList();

        Assert.True(infractores.Count == 0, "El adaptador registra un secreto:\n  " + string.Join("\n  ", infractores));
    }
}
