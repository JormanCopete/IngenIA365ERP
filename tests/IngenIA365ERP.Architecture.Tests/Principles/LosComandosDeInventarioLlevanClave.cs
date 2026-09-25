using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T13, §2.18; contracts/api.md §2.3), FR-016: toda
/// operación iniciada desde una pantalla o una integración lleva una clave de idempotencia. El
/// comando lo declara implementando <c>IOperacionIdempotente</c>; sin el marcador,
/// <c>IdempotencyBehavior</c> lo deja pasar y un doble clic confirma dos veces.
///
/// <para>
/// Llenada por la fase 2 (plataforma, T018) en dos partes: <see cref="ComandosConRuta"/>, los comandos de
/// plataforma de esta fase por nombre (tienen que existir), y el recorrido de
/// <see cref="CarpetasConClave"/>: todo <c>*Command</c> que implemente <c>IRequest</c> y que algún
/// archivo de <c>API/Endpoints</c> nombre (en código, no en comentarios) lleva la clave. Las consultas
/// —todo GET y los POST que son consultas de contracts/api.md §2.3 (<c>integrity/verify</c>,
/// <c>purchases/supplier-invoices/prefill</c>, <c>documents/{id}/prevalidate</c>,
/// <c>documents/{id}/cost-impact</c>, <c>sales/credit-evaluations</c>,
/// <c>/api/accounting/inventory/batches/preview</c>)— se nombran <c>*Query</c> y quedan fuera solas; si
/// alguna se nombra <c>*Command</c>, va en <see cref="ConsultasPorPost"/> con su ruta.
/// </para>
/// </summary>
public class LosComandosDeInventarioLlevanClave
{
    /// <summary>Comandos de plataforma con ruta que deben llevar clave (T018), por nombre de tipo.</summary>
    private static readonly string[] ComandosConRuta =
    [
        "AddParameterVersionCommand",
        "SaveApprovalPolicyCommand",
        "SetPermissionAmountLimitCommand",
        "DecideApprovalCommand",
        "AttendAlertCommand",
        "SaveAlertTypeCommand",
        "SetUserCommercialScopeCommand",
        // Fase 3 (T128): crear un rol desde un perfil sugerido, desde la pantalla de Roles.
        "CreateRoleFromTemplateCommand",
    ];

    /// <summary>Carpetas de <c>src/Core/IngenIA365ERP.Application</c> cuyos comandos con ruta llevan clave. Una que no existe todavía cuenta como vacía.</summary>
    private static readonly string[] CarpetasConClave =
    [
        "Inventory",
        "ElectronicInvoicing",
        Path.Combine("Core", "Taxes"),
        Path.Combine("Core", "PaymentMeans"),
    ];

    /// <summary>Consultas enviadas por POST que se llaman <c>*Command</c> (contracts/api.md §2.3), con su ruta. Hoy ninguna.</summary>
    private static readonly Dictionary<string, string> ConsultasPorPost = new(StringComparer.Ordinal);

    /// <summary>
    /// Comandos anteriores a la feature que la reescriben después, con la tarea que los reescribe: los de
    /// vendedores (se conservaron del módulo heredado, T036) los rehace US12 con <c>IOperacionIdempotente</c>
    /// (T424, T425), y en esa tarea salen de aquí. Si alguno ya lleva la clave, la prueba pide sacarlo.
    /// </summary>
    private static readonly Dictionary<string, string> PendientesDeReescritura = new(StringComparer.Ordinal)
    {
        ["CreateSalespersonCommand"] = "T424 (US12)",
        ["UpdateSalespersonCommand"] = "T425 (US12)",
        ["DeleteSalespersonCommand"] = "T425 (US12)",
    };

    private static readonly Regex DeclaracionDeComando = new(
        @"\b(record|class)\s+(?<nombre>\w+Command)\b(?<resto>[^{;]*)", RegexOptions.Compiled);

    [Fact]
    public void Cada_comando_con_ruta_implementa_IOperacionIdempotente()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var comando in ComandosConRuta)
        {
            // La declaración hasta la llave o el punto y coma: ahí van la base y las interfaces.
            var declaracion = new Regex($@"\b(record|class)\s+{Regex.Escape(comando)}\b[^{{;]*", RegexOptions.Compiled);
            var encontrada = fuentes
                .Select(f => (f.Archivo, Match: declaracion.Match(f.Texto)))
                .FirstOrDefault(x => x.Match.Success);

            if (encontrada.Archivo is null)
                infractores.Add($"{comando}: no se encontró su declaración (si se renombró, actualizá ComandosConRuta)");
            else if (!encontrada.Match.Value.Contains("IOperacionIdempotente", StringComparison.Ordinal))
                infractores.Add($"{Path.GetRelativePath(root, encontrada.Archivo)}: {comando} no implementa IOperacionIdempotente");
        }

        Assert.True(infractores.Count == 0,
            "Comandos con ruta sin clave de idempotencia (FR-016):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Cada_comando_con_ruta_de_inventario_y_sus_vecinos_implementa_IOperacionIdempotente()
    {
        var root = RepoPath.FindRepoRoot();
        var application = Path.Combine(root, "src", "Core", "IngenIA365ERP.Application");
        var endpoints = Path.Combine(root, "src", "Presentation", "IngenIA365ERP.API", "Endpoints");
        var codigoDeRutas = string.Join('\n', Directory.EnumerateFiles(endpoints, "*.cs", SearchOption.AllDirectories)
            .Select(FuenteSinComentarios.Leer));
        var infractores = new List<string>();
        var revisados = 0;
        var pendientesVistos = new HashSet<string>(StringComparer.Ordinal);

        foreach (var carpeta in CarpetasConClave)
        {
            var ruta = Path.Combine(application, carpeta);
            if (!Directory.Exists(ruta)) continue; // carpeta aún no creada: no tiene nada que violar

            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in DeclaracionDeComando.Matches(FuenteSinComentarios.Leer(archivo)))
                {
                    var nombre = m.Groups["nombre"].Value;
                    var resto = m.Groups["resto"].Value;
                    if (!resto.Contains("IRequest", StringComparison.Ordinal)) continue;
                    if (!Regex.IsMatch(codigoDeRutas, $@"\b{Regex.Escape(nombre)}\b")) continue; // sin ruta
                    if (ConsultasPorPost.ContainsKey(nombre)) continue;

                    revisados++;
                    var llevaClave = resto.Contains("IOperacionIdempotente", StringComparison.Ordinal);
                    var relativo = Path.GetRelativePath(root, archivo);
                    if (PendientesDeReescritura.TryGetValue(nombre, out var tarea))
                    {
                        pendientesVistos.Add(nombre);
                        if (llevaClave)
                            infractores.Add($"{relativo}: {nombre} ya lleva la clave; quitálo de PendientesDeReescritura ({tarea})");
                        continue;
                    }
                    if (!llevaClave)
                        infractores.Add($"{relativo}: {nombre} tiene ruta y no implementa IOperacionIdempotente");
                }
            }
        }

        foreach (var pendiente in PendientesDeReescritura.Keys.Where(p => !pendientesVistos.Contains(p)))
            infractores.Add($"{pendiente}: ya no es un comando con ruta en estas carpetas; quitálo de PendientesDeReescritura");

        Assert.True(revisados > 0, "El recorrido no encontró ningún comando con ruta: ¿cambió la forma de declararlos?");
        Assert.True(infractores.Count == 0,
            "Comandos con ruta sin clave de idempotencia (FR-016, contracts/api.md §2.3):\n  " + string.Join("\n  ", infractores));
    }
}
