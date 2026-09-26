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
        "Entities/Approvals/Transactions"
    ];

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
