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
        // T446 (I2): el ciclo de los lotes que sólo corre el despachador (contracts/contabilidad.md §5.4).
        "ScheduleIntegrationBatchesCommand",
        "StartIntegrationBatchCommand",
        "CloseIntegrationBatchCommand",
    ];

    [Fact]
    public void Los_comandos_de_consumo_existen_y_no_llevan_marcador_de_permiso()
    {
        // T446: un comando de consumo no se protege con permiso porque nadie lo pide por HTTP; si llevara un marcador de
        // permiso, sería la señal de que alguien pensó en exponerlo. Y todos existen: la lista no puede vigilar fantasmas
        // (RaiseAlertCommand y los de I2 ya están en Application).
        var tipos = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes();
        var infractores = new List<string>();
        foreach (var comando in ComandosDeConsumo)
        {
            var tipo = tipos.FirstOrDefault(t => t.Name == comando);
            if (tipo is null)
            {
                infractores.Add($"{comando}: no existe en Application (si se renombró, actualizá la lista)");
                continue;
            }
            var marcadores = tipo.GetInterfaces().Select(i => i.Name)
                .Concat(tipo.GetCustomAttributes(inherit: true).Select(a => a.GetType().Name))
                .Where(n => n.Contains("Permis", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (marcadores.Count > 0)
                infractores.Add($"{comando}: lleva {string.Join(", ", marcadores)}");
        }

        Assert.True(infractores.Count == 0, "Comandos de consumo mal declarados (api.md §26):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_despachador_envia_el_ciclo_de_los_lotes()
    {
        // T446: fuera de Application (sus handlers, validadores y la documentación), los comandos del ciclo de lote sólo los
        // nombra el despachador de la API. Una pantalla, un endpoint u otro trabajo de fondo que los enviara abriría otra
        // puerta al lote.
        var root = RepoPath.FindRepoRoot();
        var presentacion = Path.Combine(root, "src", "Presentation");
        var despachador = Path.Combine(presentacion, "IngenIA365ERP.API", "Integration", "DespachadorDeMensajes.cs");
        Assert.True(File.Exists(despachador), "No existe API/Integration/DespachadorDeMensajes.cs (T527).");

        string[] delCiclo = ["ScheduleIntegrationBatchesCommand", "StartIntegrationBatchCommand", "CloseIntegrationBatchCommand", "RegisterDeliveryResultCommand"];
        var textoDelDespachador = FuenteSinComentarios.Leer(despachador);
        Assert.All(delCiclo, c => Assert.Contains(c, textoDelDespachador, StringComparison.Ordinal));

        var infractores = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(presentacion, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(archivo, despachador, StringComparison.OrdinalIgnoreCase)) continue;
            if (archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) continue;
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (var comando in delCiclo)
            {
                if (Regex.IsMatch(texto, $@"\b{comando}\b"))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: nombra {comando}");
            }
        }

        Assert.True(infractores.Count == 0, "Sólo DespachadorDeMensajes envía el ciclo de los lotes (T527):\n  " + string.Join("\n  ", infractores));
    }

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
