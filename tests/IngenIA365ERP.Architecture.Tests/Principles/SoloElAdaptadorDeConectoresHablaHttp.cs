using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 020 (T150; R16, R17): la única salida HTTP de los conectores es el adaptador <c>EjecutorHttpDeConector</c> de
/// <c>ExternalChannels</c>, con la guarda de direcciones en cada conexión, sin redirecciones y sin reintentos propios. Si Application, la
/// API o Transporte armaran su propio <c>HttpClient</c> para un conector, se saltarían la defensa contra SSRF y el circuito.
/// </summary>
public class SoloElAdaptadorDeConectoresHablaHttp
{
    private static readonly string[] Prohibidos = ["HttpClient", "HttpRequestMessage", "SocketsHttpHandler", "HttpMessageHandler", "WebRequest"];

    [Fact]
    public void Ni_Application_ni_la_API_hablan_HTTP_por_un_conector()
    {
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        string[] carpetas =
        [
            Path.Combine(src, "Core", "IngenIA365ERP.Application", "Core", "Connectors"),
            Path.Combine(src, "Core", "IngenIA365ERP.Application", "Platform", "Connectors"),
            Path.Combine(src, "Core", "IngenIA365ERP.Application", "Transport"),
        ];
        string[] sueltos =
        [
            Path.Combine(src, "Presentation", "IngenIA365ERP.API", "Endpoints", "Core", "ConnectorsEndpoints.cs"),
            Path.Combine(src, "Presentation", "IngenIA365ERP.API", "Endpoints", "Saas", "ConnectorsEndpoints.cs"),
        ];
        var archivos = carpetas.Where(Directory.Exists)
            .SelectMany(c => Directory.EnumerateFiles(c, "*.cs", SearchOption.AllDirectories))
            .Concat(sueltos.Where(File.Exists))
            .ToList();
        Assert.NotEmpty(archivos);

        var infractores = archivos
            .Select(f => (Relativo: Path.GetRelativePath(src, f).Replace('\\', '/'), Fuente: FuenteSinComentarios.Leer(f)))
            .SelectMany(x => Prohibidos.Where(p => x.Fuente.Contains(p, StringComparison.Ordinal)).Select(p => $"{x.Relativo}: {p}"))
            .ToList();

        Assert.True(infractores.Count == 0, "Habla HTTP fuera del adaptador de conectores:\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_adaptador_implementa_la_salida_real()
    {
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        var implementaciones = RepoPath.ProductionCSharpFiles()
            // Una clase que lo implementa (en su lista de bases, después del constructor primario si lo tiene), no quien lo recibe.
            .Where(f => Regex.IsMatch(FuenteSinComentarios.Leer(f), @"class\s+\w+\s*(\([^)]*\))?\s*:[^{;]*\bIEjecutorDeConector\b", RegexOptions.Singleline))
            .Select(f => Path.GetRelativePath(src, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        // El defecto sin red (SinSalidaHttpDeConector) vive junto al puerto; la salida real, sólo en el adaptador.
        Assert.Equal(
            [
                "Core/IngenIA365ERP.Application/Core/Connectors/IEjecutorDeConector.cs",
                "Infrastructure/IngenIA365ERP.ExternalChannels/Connectors/EjecutorHttpDeConector.cs",
            ],
            implementaciones);
    }
}
