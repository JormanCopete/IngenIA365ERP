using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 014, US16 (T164; FR-003; api.md §4): las lecturas sensibles de captaciones y aportes —el saldo, el extracto, el libro y las operaciones de la
/// cuenta de un titular, y los saldos o movimientos de una persona— se auditan, cada una con su método de <see cref="LendingAuditEmitter"/>. La lista de
/// eventos es propia (<see cref="LendingAuditEmitter.LecturasSensiblesDeCaptaciones"/>) y no toca la cerrada de la 013. Las exportaciones de las vistas de
/// captaciones las audita el centro de informes de la Cartera (<c>LendingReportsEndpoints</c>, <c>Lending.Report.Exported</c>) y los documentos al titular,
/// la emisión y la reimpresión de la 013 (<c>Lending.Document.*</c>). (nuevo)
/// </summary>
public class LasLecturasSensiblesDeCaptacionesSeAuditan
{
    private const string Deposits = "Core/IngenIA365ERP.Application/Lending/Deposits/";

    /// <summary>Cada lectura sensible: su archivo, su consulta y el método del emisor que la audita.</summary>
    private static readonly (string Archivo, string Consulta, string Metodo)[] Lecturas =
    [
        (Deposits + "Accounts/MemberAccountStatementQueries.cs", "GetMemberAccountBalanceQuery", nameof(LendingAuditEmitter.EmitirSaldoDeCuentaVistoAsync)),
        (Deposits + "Accounts/MemberAccountStatementQueries.cs", "GetMemberAccountStatementQuery", nameof(LendingAuditEmitter.EmitirExtractoDeCuentaVistoAsync)),
        (Deposits + "Accounts/MemberAccountQueries.cs", "GetMemberAccountQuery", nameof(LendingAuditEmitter.EmitirSaldoDeCuentaVistoAsync)),
        (Deposits + "Accounts/MemberAccountQueries.cs", "ListMemberAccountsQuery", nameof(LendingAuditEmitter.EmitirSaldosDePersonaVistosAsync)),
        (Deposits + "Accounts/CloseMemberAccountCommand.cs", "PreviewMemberAccountClosingQuery", nameof(LendingAuditEmitter.EmitirSaldoDeCuentaVistoAsync)),
        (Deposits + "Contributions/ContributionQueries.cs", "GetContributionsSummaryQuery", nameof(LendingAuditEmitter.EmitirSaldosDePersonaVistosAsync)),
        (Deposits + "Operations/MemberAccountOperationCommands.cs", "ListMemberAccountOperationsQuery",
            nameof(LendingAuditEmitter.EmitirMovimientosDeCuentaVistosAsync)),
        (Deposits + "Operations/MemberAccountOperationCommands.cs", "GetMemberAccountOperationQuery",
            nameof(LendingAuditEmitter.EmitirMovimientosDeCuentaVistosAsync)),
        (Deposits + "Operations/MemberAccountOperationCommands.cs", "ListMemberAccountTransactionsQuery",
            nameof(LendingAuditEmitter.EmitirMovimientosDeCuentaVistosAsync)),
        (Deposits + "Operations/MemberAccountOperationCommands.cs", "GetGmfMonthlyUsageQuery",
            nameof(LendingAuditEmitter.EmitirMovimientosDePersonaVistosAsync)),
        (Deposits + "Accrual/InterestSettlementCommands.cs", "ListInterestSettlementsQuery", nameof(LendingAuditEmitter.EmitirMovimientosDeCuentaVistosAsync)),
        (Deposits + "Accrual/InterestSettlementCommands.cs", "SimulateInterestSettlementQuery",
            nameof(LendingAuditEmitter.EmitirMovimientosDeCuentaVistosAsync)),
        // US11 (T189): los embargos —la lista, el detalle con lo retenido y la explicación del límite, el borrador de la respuesta al juzgado—.
        (Deposits + "Holds/GarnishmentCommands.cs", "ListGarnishmentsQuery", nameof(LendingAuditEmitter.EmitirEmbargoVistoAsync)),
        (Deposits + "Holds/GarnishmentCommands.cs", "GetGarnishmentQuery", nameof(LendingAuditEmitter.EmitirEmbargoVistoAsync)),
        (Deposits + "Holds/GarnishmentCommands.cs", "GetGarnishmentResponseDraftQuery", nameof(LendingAuditEmitter.EmitirEmbargoVistoAsync)),
    ];

    [Fact]
    public void Cada_lectura_sensible_de_captaciones_llama_a_su_metodo_del_emisor()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var (archivo, consulta, metodo) in Lecturas)
        {
            var camino = Path.Combine(raiz, "src", archivo.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(camino)) { infracciones.Add($"{archivo}: no existe"); continue; }
            var texto = FuenteSinComentarios.Leer(camino);
            var inicio = texto.IndexOf($"class {consulta}Handler", StringComparison.Ordinal);
            if (inicio < 0) { infracciones.Add($"{archivo}: no tiene {consulta}Handler"); continue; }
            var siguiente = texto.IndexOf("\npublic ", inicio + 1, StringComparison.Ordinal);
            var cuerpo = siguiente < 0 ? texto[inicio..] : texto[inicio..siguiente];
            if (!cuerpo.Contains($".{metodo}(", StringComparison.Ordinal))
                infracciones.Add($"{archivo}: {consulta}Handler no llama a LendingAuditEmitter.{metodo}");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Lecturas sensibles de captaciones sin su evento (FR-003)");
    }

    [Fact]
    public void Los_eventos_son_los_de_api_md_y_no_tocan_la_lista_cerrada_de_la_013()
    {
        Assert.Equal(
            new[] { "Lending.Garnishment.Viewed", "Lending.Savings.BalanceViewed", "Lending.Savings.StatementViewed", "Lending.Savings.TransactionsViewed" },
            LendingAuditEmitter.LecturasSensiblesDeCaptaciones.Order(StringComparer.Ordinal));
        Assert.Equal("Lending.Savings.BalanceViewed", AuditEventTypes.LendingSavingsBalanceViewed);
        Assert.Equal("Lending.Savings.StatementViewed", AuditEventTypes.LendingSavingsStatementViewed);
        Assert.Empty(LendingAuditEmitter.LecturasSensibles.Intersect(LendingAuditEmitter.LecturasSensiblesDeCaptaciones));
    }

    [Fact]
    public void Las_rutas_del_saldo_y_del_extracto_exigen_leer_cuentas()
    {
        var rutas = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Lending", "Deposits",
            "MemberAccountsEndpoints.cs"));
        Assert.Matches(@"MapGet\(""/\{id:guid\}/balance""[^;]*?RequirePermission\(Leer\)", rutas);
        Assert.Matches(@"MapGet\(""/\{id:guid\}/statement""[^;]*?RequirePermission\(Leer\)", rutas);
    }
}
