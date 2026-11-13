using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 023 (research R5; plan, Principio II): RabbitMQ vive detrás del puerto <c>IColaDeTrabajos</c>. Sólo el proyecto
/// <c>src/Infrastructure/IngenIA365ERP.Messaging</c> referencia <c>RabbitMQ.Client</c>; Domain y Application no conocen ni el adaptador ni la
/// librería; la API lo nombra sólo en <c>Program.cs</c> (raíz de composición). Cambiar de broker es cambiar el adaptador. (nuevo)
/// </summary>
public class SoloElAdaptadorDeMensajeriaConoceAlBroker
{
    private const string EnsambladoDelAdaptador = "IngenIA365ERP.Messaging";
    private const string Libreria = "RabbitMQ.Client";

    [Fact]
    public void El_ensamblado_del_adaptador_es_el_que_se_vigila()
    {
        Assert.Equal(EnsambladoDelAdaptador, typeof(IngenIA365ERP.Messaging.ColaRabbitMq).Assembly.GetName().Name);
    }

    [Fact]
    public void Ni_Domain_ni_Application_referencian_el_adaptador_ni_la_libreria_del_broker()
    {
        var internas = new[]
        {
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.Common.Mensajeria.IColaDeTrabajos).Assembly,
        };
        var infractores = internas
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => new { Capa = a.GetName().Name, Ref = r.Name ?? string.Empty }))
            .Where(x => x.Ref.StartsWith(EnsambladoDelAdaptador, StringComparison.OrdinalIgnoreCase)
                        || x.Ref.StartsWith("RabbitMQ", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.Capa} referencia {x.Ref}")
            .ToList();

        Assert.True(infractores.Count == 0, "El broker se filtró a una capa interna:\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_adaptador_usa_la_libreria_y_solo_Program_conoce_al_adaptador()
    {
        var root = RepoPath.FindRepoRoot();
        var usaLibreria = new Regex(@"\bRabbitMQ\.", RegexOptions.Compiled);
        var conoceAdaptador = new Regex($@"\b(?<!\.){Regex.Escape(EnsambladoDelAdaptador)}(\.|;|\b)", RegexOptions.Compiled);
        var infractores = new List<string>();

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            var relativo = Path.GetRelativePath(root, archivo).Replace('\\', '/');
            var esAdaptador = relativo.Contains("/IngenIA365ERP.Messaging/", StringComparison.Ordinal);
            var fuente = FuenteSinComentarios.Leer(archivo);
            if (!esAdaptador && usaLibreria.IsMatch(fuente)) infractores.Add($"{relativo}: usa {Libreria}");
            var esProgramDeLaApi = relativo.EndsWith("/IngenIA365ERP.API/Program.cs", StringComparison.Ordinal);
            if (!esAdaptador && !esProgramDeLaApi && conoceAdaptador.IsMatch(fuente)) infractores.Add($"{relativo}: conoce {EnsambladoDelAdaptador}");
        }

        Assert.True(infractores.Count == 0, "Sólo el adaptador conoce al broker y sólo API/Program.cs al adaptador:\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_proyecto_del_adaptador_referencia_el_paquete()
    {
        var root = RepoPath.FindRepoRoot();
        var proyectos = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Where(p => File.ReadAllText(p).Contains($"Include=\"{Libreria}\"", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .ToList();

        Assert.True(proyectos.Count == 1 && proyectos[0] == "src/Infrastructure/IngenIA365ERP.Messaging/IngenIA365ERP.Messaging.csproj",
            $"{Libreria} sólo en el adaptador. Lo referencian:\n  " + string.Join("\n  ", proyectos));
    }
}
