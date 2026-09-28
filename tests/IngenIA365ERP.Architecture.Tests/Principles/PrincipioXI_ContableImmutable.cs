using IngenIA365ERP.Architecture.Tests.Helpers;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio XI — Inmutabilidad contable. Cuando aterricen los namespaces
/// de movimientos contables (módulos Contabilidad, Cartera, Nómina), ningún
/// handler puede invocar <c>Remove()</c> ni <c>RemoveRange()</c> sobre
/// entidades de transacciones contables.
///
/// Vacuum gate: en Fase 0 no hay movimientos contables. El test pasa
/// vacuamente y se activa cuando aparezcan los namespaces relevantes
/// (módulos futuros).
/// </summary>
public class PrincipioXI_ContableImmutable
{
    private static readonly string[] ProtectedNamespaces =
    [
        "Entities/Accounting/Transactions",
        "Entities/Lending/Transactions",
        "Entities/Payroll/Transactions",
        // Feature 012 (T019; decisiones-transversales §2.18, T7, T33): los mensajes de integración y sus
        // entregas, y las solicitudes y decisiones de aprobación, son hechos: no se borran.
        "Entities/Integration/Transactions",
        "Entities/Approvals/Transactions",
        // Feature 012, I4 (T670; decisiones-transversales §2.18): las versiones y las transmisiones de un documento
        // electrónico son la bitácora ante la DIAN: no se borran.
        "Entities/ElectronicInvoicing/Transactions",
        // Feature 012, I5 (T825): el consumo de una capa PEPS es un hecho. Sólo esta entidad del espacio de nombres: el resto de
        // Entities/Inventory/Transactions lo vigilan LosHechosInmutablesNoSeModifican y NadieEscribeElKardexFueraDelRegistro.
        "Entities/Inventory/Transactions/LayerConsumption"
    ];

    /// <summary>
    /// I5, T825: los consumos de capa también por su conjunto, porque el recorrido de arriba sólo ve los archivos que nombran el espacio
    /// de nombres completo. Nadie borra un consumo: una salida anulada devuelve lo consumido con otro consumo negativo.
    /// </summary>
    [Fact]
    public void Nadie_borra_consumos_de_capa()
    {
        var root = RepoPath.FindRepoRoot();
        var borrado = new Regex(@"\.LayerConsumptions\s*\.\s*(Remove|RemoveRange|ExecuteDelete|ExecuteDeleteAsync)\b", RegexOptions.Compiled);
        var offenders = RepoPath.ProductionCSharpFiles()
            .Where(f => borrado.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(offenders.Count == 0, "Consumos de capa borrados (Principio XI, T825):\n  " + string.Join("\n  ", offenders));
    }

    private static readonly Regex RemoveCall = new(
        @"\.(Remove|RemoveRange)\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void Handlers_must_not_call_Remove_on_transaction_entities()
    {
        var root = RepoPath.FindRepoRoot();
        var offenders = new List<string>();

        foreach (var file in RepoPath.ProductionCSharpFiles())
        {
            var content = File.ReadAllText(file);
            if (!RemoveCall.IsMatch(content)) continue;

            // Solo nos interesa si el archivo hace referencia a alguno de los namespaces protegidos.
            foreach (var ns in ProtectedNamespaces)
            {
                var nsDot = ns.Replace('/', '.');
                if (content.Contains(nsDot, StringComparison.Ordinal))
                {
                    offenders.Add(file.Replace(root, string.Empty));
                    break;
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Llamada a Remove/RemoveRange sobre namespace contable inmutable:\n  "
            + string.Join("\n  ", offenders));
    }
}
