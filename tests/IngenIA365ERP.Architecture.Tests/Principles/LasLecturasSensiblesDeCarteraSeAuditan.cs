using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-003 y SC-014 (DT §2.25, T67, C89): las lecturas sensibles de la lista cerrada se auditan.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0006) y la llena cada historia con los handlers de su lista (research.md §13); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
///
/// <para>
/// US17 (T0260) fija la lista cerrada de los diez eventos de <see cref="LendingAuditEmitter.LecturasSensibles"/> y las tres lecturas de
/// esta fase con el método del emisor que tienen que llamar: el acta (<c>GetSessionQuery</c> y <c>GetSessionMinutesPdfQuery</c>, los dos con
/// <c>EmitirActaVistaAsync</c>) y la lista de vinculados (<c>ListRelatedPartiesQuery</c>, <c>EmitirVinculadosVistosAsync</c>). Las historias
/// siguientes suman la liquidación, el informe de centrales, la reimpresión, la verificación y las exportaciones.
/// </para>
/// </summary>
public class LasLecturasSensiblesDeCarteraSeAuditan
{
    /// <summary>Handlers de lecturas sensibles (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        "Core/IngenIA365ERP.Application/Lending/Sessions/SessionQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Sessions/ActaDeSesionEnPdf.cs",
        "Core/IngenIA365ERP.Application/Lending/Attributions/RelatedPartyCommands.cs",
        // US2 (T0393): los grupos conectados, como los vinculados.
        "Core/IngenIA365ERP.Application/Lending/Origination/ConnectedGroups/ConnectedGroupCommands.cs",
        // US2 (bloque 6B): el informe de centrales (detalle y enlace) y la liberación de una coincidencia en listas.
        "Core/IngenIA365ERP.Application/Lending/Adapters/BureauQueryCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/ScreeningResultCommands.cs",
        // US4 (T0480): la liquidación y el estado de cuenta (lo que cuesta cancelar).
        "Core/IngenIA365ERP.Application/Lending/Settlement/GetLoanSettlementQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Settlement/GetLoanStatementQuery.cs",
    ];

    /// <summary>Cada lectura sensible: su archivo, su consulta y el método de <see cref="LendingAuditEmitter"/> que la audita.</summary>
    private static readonly (string Archivo, string Consulta, string Metodo)[] Lecturas =
    [
        ("Core/IngenIA365ERP.Application/Lending/Sessions/SessionQueries.cs", "GetSessionQuery", nameof(LendingAuditEmitter.EmitirActaVistaAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Sessions/ActaDeSesionEnPdf.cs", "GetSessionMinutesPdfQuery", nameof(LendingAuditEmitter.EmitirActaVistaAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Attributions/RelatedPartyCommands.cs", "ListRelatedPartiesQuery", nameof(LendingAuditEmitter.EmitirVinculadosVistosAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Origination/ConnectedGroups/ConnectedGroupCommands.cs", "ListConnectedGroupsQuery",
            nameof(LendingAuditEmitter.EmitirVinculadosVistosAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Origination/ConnectedGroups/ConnectedGroupCommands.cs", "GetConnectedGroupQuery",
            nameof(LendingAuditEmitter.EmitirVinculadosVistosAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Adapters/BureauQueryCommands.cs", "GetBureauQueryQuery",
            nameof(LendingAuditEmitter.EmitirInformeDeCentralesVistoAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Adapters/BureauQueryCommands.cs", "IssueBureauReportDownloadLinkCommand",
            nameof(LendingAuditEmitter.EmitirInformeDeCentralesVistoAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Origination/ScreeningResultCommands.cs", "ReleaseScreeningMatchCommand",
            nameof(LendingAuditEmitter.EmitirCoincidenciaLiberadaAsync)),
        // US4 (T0480): la liquidación a (D, R) y el estado de cuenta auditan Lending.Payoff.Viewed con D, R y la huella.
        ("Core/IngenIA365ERP.Application/Lending/Settlement/GetLoanSettlementQuery.cs", "GetLoanSettlementQuery",
            nameof(LendingAuditEmitter.EmitirLiquidacionVistaAsync)),
        ("Core/IngenIA365ERP.Application/Lending/Settlement/GetLoanStatementQuery.cs", "GetLoanStatementQuery",
            nameof(LendingAuditEmitter.EmitirLiquidacionVistaAsync)),
    ];

    /// <summary>La lista cerrada de research §13 y C89 (los diez eventos de T0256).</summary>
    private static readonly string[] ListaCerrada =
    [
        "Lending.Payoff.Viewed",
        "Lending.CreditBureauReport.Viewed",
        "Lending.Document.Reprinted",
        "Lending.Document.Verified",
        "Lending.Report.Exported",
        "Lending.Session.Viewed",
        "Lending.RelatedParty.Viewed",
        "Lending.CreditStatus.Queried",
        "Lending.Access.OutOfScope",
        "Lending.ScreeningMatch.Released",
    ];

    private static readonly Regex Requerido = new(@"\bAudit\w*\(|\bAuditar\w*\(|LendingAuditEmitter", RegexOptions.Compiled);

    [Fact]
    public void Cada_lectura_sensible_se_audita()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Una lectura sensible de Cartera no se audita (C89)");
    }

    [Fact]
    public void La_lista_de_lecturas_sensibles_es_la_cerrada()
    {
        Assert.Equal(ListaCerrada.Order(StringComparer.Ordinal), LendingAuditEmitter.LecturasSensibles.Order(StringComparer.Ordinal));
        Assert.Contains(AuditEventTypes.LendingSessionViewed, ListaCerrada);
        Assert.Contains(AuditEventTypes.LendingRelatedPartyViewed, ListaCerrada);
    }

    [Fact]
    public void Cada_lectura_de_esta_fase_llama_a_su_metodo_del_emisor()
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
        ReglaSobreFuente.SinInfracciones(infracciones, "Lecturas sensibles de Cartera sin su evento (C89)");
    }
}
