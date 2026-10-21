using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T71, T4; contracts/adaptadores-y-archivos.md §2): los clientes de proveedores externos (depósito de valores,
/// centrales, SMS, WhatsApp, listas) viven sólo en <c>IngenIA365ERP.ExternalChannels</c>; fuera de ese proyecto sólo <c>Program.cs</c> lo
/// nombra, y sólo él llama <c>AddExternalChannels</c>. Domain y Application no lo referencian. Molde:
/// <c>ElProveedorTecnologicoSoloLoConoceSuAdaptador</c>. Corre de verdad desde el Setup sobre todo el fuente de producción.
///
/// <para>
/// Listas (T0038, adaptadores): <see cref="SdksDeProveedores"/> está vacía hasta que se contrate un proveedor (SMS, WhatsApp, depósito,
/// central): el adaptador real agrega aquí el espacio de nombres de su SDK, si lo trae. <see cref="AdaptadoresConocidos"/> ata la prueba a los
/// tipos reales del proyecto, para que un renombre no la deje sin mirar nada.
/// </para>
/// </summary>
public class LosProveedoresExternosSoloLosConoceSuAdaptador
{
    /// <summary>El ensamblado de los adaptadores (<c>src/Infrastructure/IngenIA365ERP.ExternalChannels</c>).</summary>
    private const string EnsambladoDeAdaptadores = "IngenIA365ERP.ExternalChannels";

    /// <summary>Espacios de nombres (y ensamblados) de los SDK de los proveedores. Vacía hasta que se contrate uno (I1).</summary>
    private static readonly string[] SdksDeProveedores = [];

    /// <summary>Los adaptadores del proyecto (T0081; el de <c>CreditBureaus/</c> desde US2; los de <c>Securities/</c> se suman en US3).</summary>
    private static readonly Type[] AdaptadoresConocidos =
    [
        typeof(IngenIA365ERP.ExternalChannels.Messaging.MensajeriaSimulada),
        typeof(IngenIA365ERP.ExternalChannels.RestrictiveLists.ListasSimuladas),
        typeof(IngenIA365ERP.ExternalChannels.RestrictiveLists.CanalDeListasCargadas),
        typeof(IngenIA365ERP.ExternalChannels.Credentials.CredencialesDeCanalExternoEnArchivo),
        typeof(IngenIA365ERP.ExternalChannels.Circuit.CircuitoDeCanalExterno),
        // US2 (T0401, T0382): la central de riesgo simulada; las reales se suman con su contrato.
        typeof(IngenIA365ERP.ExternalChannels.CreditBureaus.CentralDeRiesgoSimulada),
        // US3 (T0428, T0437): el depósito de valores simulado y su memoria por cooperativa.
        typeof(IngenIA365ERP.ExternalChannels.Securities.DepositoDeValoresSimulado),
        typeof(IngenIA365ERP.ExternalChannels.Securities.MemoriaDeDepositoSimulado),
    ];

    private static readonly Regex UsaElProyecto = new(@"\bIngenIA365ERP\.ExternalChannels\b", RegexOptions.Compiled);

    private static IEnumerable<string> EspaciosDeProveedores => new[] { EnsambladoDeAdaptadores }.Concat(SdksDeProveedores);

    [Fact]
    public void Los_adaptadores_conocidos_viven_en_el_ensamblado_que_se_vigila()
    {
        Assert.All(AdaptadoresConocidos, t => Assert.Equal(EnsambladoDeAdaptadores, t.Assembly.GetName().Name));
    }

    [Fact]
    public void Nadie_fuera_del_proyecto_de_canales_lo_usa()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            RepoPath.ProductionCSharpFiles(),
            [(UsaElProyecto, "usa IngenIA365ERP.ExternalChannels")],
            relativo => relativo.Contains("/IngenIA365ERP.ExternalChannels/", StringComparison.Ordinal)
                        || relativo.EndsWith("IngenIA365ERP.API/Program.cs", StringComparison.Ordinal));
        ReglaSobreFuente.SinInfracciones(infracciones, "Un proveedor externo se conoce fuera de su adaptador (T71)");
    }

    [Fact]
    public void Ni_Domain_ni_Application_referencian_el_adaptador_ni_un_SDK_de_proveedor()
    {
        var internas = new[]
        {
            typeof(Domain.Common.BaseEntity).Assembly,
            typeof(Application.Common.ExternalChannels.ICredencialesDeCanalExterno).Assembly,
        };

        var infractores = internas
            .SelectMany(a => a.GetReferencedAssemblies().Select(r => new { Capa = a.GetName().Name, Ref = r.Name ?? string.Empty }))
            .Where(x => EspaciosDeProveedores.Any(e => x.Ref.Equals(e, StringComparison.OrdinalIgnoreCase)
                                                     || x.Ref.StartsWith(e + ".", StringComparison.OrdinalIgnoreCase)))
            .Select(x => $"{x.Capa} referencia {x.Ref}")
            .ToList();

        Assert.True(infractores.Count == 0, "Un proveedor externo se filtró a una capa interna (T71):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_Program_de_la_API_llama_AddExternalChannels()
    {
        var root = RepoPath.FindRepoRoot();
        var llamada = new Regex(@"\.AddExternalChannels\s*\(", RegexOptions.Compiled);
        var llamadores = RepoPath.ProductionCSharpFiles()
            .Where(f => llamada.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .ToList();

        Assert.True(llamadores.Count == 1 && llamadores[0] == "src/Presentation/IngenIA365ERP.API/Program.cs",
            "AddExternalChannels se llama sólo desde API/Program.cs (T71). Lo llaman:\n  " + string.Join("\n  ", llamadores));
    }

    [Fact]
    public void Program_compone_los_canales_despues_de_Application()
    {
        var programa = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Program.cs"));
        var application = programa.IndexOf(".AddApplicationServices(", StringComparison.Ordinal);
        var canales = programa.IndexOf(".AddExternalChannels(", StringComparison.Ordinal);

        Assert.True(application >= 0 && canales > application,
            "AddExternalChannels va después de AddApplicationServices: reemplaza sus defectos (T71, §2).");
    }
}
