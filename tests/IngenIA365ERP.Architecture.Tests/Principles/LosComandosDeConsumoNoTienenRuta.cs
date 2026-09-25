using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T10, §2.18; contracts/api.md §17.3), FR-083: los
/// comandos que sólo corre el proceso de integración —registrar el resultado de una entrega,
/// contabilizar mensajes o un grupo de lote, levantar una alerta— no tienen ruta. Si una persona
/// pudiera llamarlos por HTTP se saltaría la bandeja de salida y el actor de proceso.
///
/// <para>
/// Llenada por la fase 2 (plataforma, T018) con <c>RegisterDeliveryResultCommand</c>,
/// <c>PostInventoryMessagesCommand</c>, <c>PostInventorySummaryGroupCommand</c> y
/// <c>RaiseAlertCommand</c>; los que todavía no existen (I2) quedan vigilados desde ya. Se busca en el
/// código sin comentarios: el resumen de <c>AlertsEndpoints</c> nombra <c>RaiseAlertCommand</c> para
/// decir justamente que no tiene ruta.
/// </para>
/// </summary>
public class LosComandosDeConsumoNoTienenRuta
{
    /// <summary>Nombres de tipo de los comandos de consumo (contracts/api.md §17.3).</summary>
    private static readonly string[] ComandosDeConsumo =
    [
        "RegisterDeliveryResultCommand",
        "PostInventoryMessagesCommand",
        "PostInventorySummaryGroupCommand",
        "RaiseAlertCommand",
    ];

    [Fact]
    public void Ningun_endpoint_menciona_un_comando_de_consumo()
    {
        var root = RepoPath.FindRepoRoot();
        var endpoints = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.API", "Endpoints");
        var archivos = Directory.Exists(endpoints)
            ? Directory.EnumerateFiles(endpoints, "*.cs", SearchOption.AllDirectories).ToList()
            : [];
        var infractores = new List<string>();

        foreach (var comando in ComandosDeConsumo)
        {
            var nombre = new Regex($@"\b{Regex.Escape(comando)}\b", RegexOptions.Compiled);
            foreach (var archivo in archivos)
            {
                if (nombre.IsMatch(FuenteSinComentarios.Leer(archivo)))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: expone {comando}");
            }
        }

        Assert.True(infractores.Count == 0,
            "Comandos de consumo con ruta (contracts/api.md §17.3):\n  " + string.Join("\n  ", infractores));
    }
}
