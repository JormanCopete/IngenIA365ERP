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
        // Feature 014 (T029; data-model §0.1): el libro de cuentas de captación y aportes y su hecho primario.
        "Entities/Lending/Deposits/Transactions",
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

    /// <summary>
    /// Feature 013, T0144: la regla se activa sola con <c>Entities/Lending/Transactions</c>, donde vive <c>LoanTransaction</c> (el libro de
    /// Cartera), y nadie borra ni actualiza sus filas (se corrigen con un espejo, FR-002).
    /// </summary>
    [Fact]
    public void El_libro_de_Cartera_esta_en_el_espacio_protegido_y_nadie_lo_borra_ni_lo_actualiza()
    {
        var root = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(root, "src", "Core", "IngenIA365ERP.Domain", "Entities", "Lending", "Transactions");
        Assert.True(File.Exists(Path.Combine(carpeta, "LoanTransaction.cs")), "LoanTransaction vive en Entities/Lending/Transactions (data-model §0.1).");
        Assert.Equal("IngenIA365ERP.Domain.Entities.Lending.Transactions", typeof(IngenIA365ERP.Domain.Entities.Lending.Transactions.LoanTransaction).Namespace);

        var escritura = new Regex(@"\.LoanTransactions\s*\.\s*(Remove|RemoveRange|Update|UpdateRange|ExecuteDelete|ExecuteDeleteAsync|ExecuteUpdate|ExecuteUpdateAsync)\b", RegexOptions.Compiled);
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => escritura.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();
        Assert.True(infractores.Count == 0, "Filas del libro de Cartera borradas o actualizadas (Principio XI):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// Feature 013, US4 (T0480): la posición diaria (se versiona, nunca se sobrescribe: D13), las partes de los hechos contables y las operaciones
    /// tampoco se borran ni se actualizan en bloque; la regla de arriba sólo ve el libro.
    /// </summary>
    [Fact]
    public void Las_posiciones_las_partes_y_las_operaciones_de_Cartera_no_se_borran()
    {
        var root = RepoPath.FindRepoRoot();
        var escritura = new Regex(@"\.(LoanDailyPositions|AccountingFactParts|LoanOperations)\s*\.\s*(Remove|RemoveRange|ExecuteDelete|ExecuteDeleteAsync|ExecuteUpdate|ExecuteUpdateAsync)\b",
            RegexOptions.Compiled);
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => escritura.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();
        Assert.True(infractores.Count == 0, "Posiciones, partes u operaciones de Cartera borradas o actualizadas en bloque (Principio XI):\n  "
            + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// Feature 014 (T029; data-model §0.1, §4.1–§4.3): el libro de cuentas (<c>MemberAccountTransaction</c>) vive en
    /// <c>Entities/Lending/Deposits/Transactions</c>, dentro del espacio protegido, y nadie borra ni actualiza en bloque sus filas, sus operaciones
    /// ni sus posiciones diarias (se corrigen con un espejo o una versión nueva, FR-002).
    /// </summary>
    [Fact]
    public void El_libro_de_cuentas_esta_en_el_espacio_protegido_y_nadie_lo_borra_ni_lo_actualiza()
    {
        var root = RepoPath.FindRepoRoot();
        var carpeta = Path.Combine(root, "src", "Core", "IngenIA365ERP.Domain", "Entities", "Lending", "Deposits", "Transactions");
        Assert.True(File.Exists(Path.Combine(carpeta, "MemberAccountTransaction.cs")), "MemberAccountTransaction vive en Entities/Lending/Deposits/Transactions.");
        Assert.Equal("IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions",
            typeof(IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions.MemberAccountTransaction).Namespace);
        Assert.Contains("Entities/Lending/Deposits/Transactions", ProtectedNamespaces);

        var escritura = new Regex(
            @"\.(MemberAccountTransactions|MemberAccountOperations|MemberAccountDailyPositions)\s*\.\s*(Remove|RemoveRange|Update|UpdateRange|ExecuteDelete|ExecuteDeleteAsync|ExecuteUpdate|ExecuteUpdateAsync)\b",
            RegexOptions.Compiled);
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => escritura.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();
        Assert.True(infractores.Count == 0, "Filas del libro de cuentas borradas o actualizadas (Principio XI):\n  " + string.Join("\n  ", infractores));
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
