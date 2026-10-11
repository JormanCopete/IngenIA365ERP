using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-009 y FR-149 (DT §2.25, T21): toda escritura de pantalla de Cartera lleva <c>Idempotency-Key</c>;
/// cada archivo de rutas de escritura de Cartera la exige.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0002) y la llena cada historia con sus archivos de rutas en <c>API/Endpoints/Lending</c> (los de hoy son de la cartera heredada); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class LosComandosDeCarteraLlevanClave
{
    /// <summary>Archivos de rutas de escritura de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // Fase 3 (T0234, C107): la migración y la activación en su forma mínima; US8 suma sus demás rutas.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/MigrationEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ActivationEndpoints.cs",
        // US17 (T0265, T0273): gobierno, configuración y la cola de mensajes al deudor.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/AttributionsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/GoverningBodiesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/SessionsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/RelatedPartiesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ScopesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/AlertsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ParametersEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/DebtorMessagesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/MessageTemplatesEndpoints.cs",
        // US1 (T0341): líneas, versiones, cierre, reapertura, borrado, puntaje, equivalencias e importación.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CreditLinesEndpoints.cs",
    ];

    /// <summary>
    /// Las escrituras que van sin clave (api.md §1.5): las consultas por POST por el tamaño del cuerpo. Se nombran por el final de la ruta.
    /// </summary>
    private static readonly string[] ConsultasPorPost = ["\"/preview\"", "/usury-check\""];

    /// <summary>
    /// Los comandos de esta fase que llegan por pantalla (US17, T0265, T0273): cada uno es <c>IOperacionIdempotente</c>, así
    /// <c>IdempotencyBehavior</c> exige la clave aunque alguien olvide el filtro de la ruta.
    /// </summary>
    private static readonly Type[] ComandosDeLaFase =
    [
        typeof(IngenIA365ERP.Application.Lending.Attributions.AddAttributionMatrixVersionCommand),
        typeof(IngenIA365ERP.Application.Lending.Attributions.RegisterRelatedPartyCommand),
        typeof(IngenIA365ERP.Application.Lending.Attributions.EndRelatedPartyCommand),
        typeof(IngenIA365ERP.Application.Lending.Imports.ImportRelatedPartiesCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.CreateGoverningBodyCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.AddGoverningBodyRulesVersionCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.AddGoverningBodyMemberCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.EndGoverningBodyMemberCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.DeactivateGoverningBodyCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.CreateSessionCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.RecordSessionAttendanceCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.AddSessionItemCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.UpdateSessionItemCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.RemoveSessionItemCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.CreateSessionPresenceChallengeCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.RecordSessionVoteCommand),
        typeof(IngenIA365ERP.Application.Lending.Sessions.CloseSessionCommand),
        typeof(IngenIA365ERP.Application.Lending.Common.SetUserCreditLineScopesCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.SendDebtorMessageCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.CancelDebtorMessageCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.CreateMessageTemplateVersionCommand),
        // US1 (T0341).
        typeof(IngenIA365ERP.Application.Lending.Configuration.CreateCreditLineCommand),
        typeof(IngenIA365ERP.Application.Lending.Configuration.AddCreditLineVersionCommand),
        typeof(IngenIA365ERP.Application.Lending.Configuration.CloseCreditLineCommand),
        typeof(IngenIA365ERP.Application.Lending.Configuration.ReopenCreditLineCommand),
        typeof(IngenIA365ERP.Application.Lending.Configuration.DeleteCreditLineCommand),
        typeof(IngenIA365ERP.Application.Lending.Configuration.SetCreditLineScoringCommand),
        typeof(IngenIA365ERP.Application.Lending.Configuration.SetCommercialTermEquivalencesCommand),
        typeof(IngenIA365ERP.Application.Lending.Imports.ImportCreditLinesCommand),
        // US2 (bloque 6A): el envío de la simulación, la solicitud y los grupos conectados.
        typeof(IngenIA365ERP.Application.Lending.Origination.SendScheduleSimulationCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreateCreditApplicationCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.UpdateCreditApplicationCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.AttachCreditApplicationDocumentCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.RemoveCreditApplicationDocumentCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.SubmitCreditApplicationCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.RecordCreditApplicationAnalysisCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.RerunCreditApplicationChecksCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CancelCreditApplicationCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.ConnectedGroups.RegisterConnectedGroupCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.ConnectedGroups.AddConnectedGroupMemberCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.ConnectedGroups.EndConnectedGroupMemberCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.ConnectedGroups.EndConnectedGroupCommand),
        // US2 (bloque 6B): la decisión, las centrales, la liberación de listas, los cupos, los bloqueos y los saldos de aportes.
        typeof(IngenIA365ERP.Application.Lending.Attributions.RecordCreditDecisionCommand),
        typeof(IngenIA365ERP.Application.Lending.Adapters.QueryCreditBureauCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.ReleaseScreeningMatchCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreditLimits.AssignCreditLimitCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreditLimits.EndCreditLimitCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreditLimits.RevalidateCreditLimitCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreditLimits.ChangePrepaymentPreferenceCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreditLimits.RegisterDebtorBlockCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreditLimits.LiftDebtorBlockCommand),
        typeof(IngenIA365ERP.Application.Lending.Origination.CreditLimits.ImportContributionBalancesCommand),
    ];

    private static readonly Regex Requerido = new(@"Idempotency|ConClave|RequiereClave", RegexOptions.Compiled);

    [Fact]
    public void Cada_escritura_de_Cartera_exige_la_clave_salvo_las_consultas_por_POST()
    {
        // US17 (T0265): ruta por ruta, no sólo por archivo. Un tramo de Map(Post|Put|Delete|Patch) sin ConClaveDeOperacion sólo se admite
        // para las consultas por POST de api.md §1.5.
        var carpeta = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Lending");
        var infracciones = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs"))
        {
            foreach (var tramo in LosEndpointsProtegidosExigenPermiso.Tramos(File.ReadAllText(archivo)))
            {
                if (!Regex.IsMatch(tramo, @"^\.Map(Post|Put|Delete|Patch)\(")) continue;
                if (tramo.Contains(".ConClaveDeOperacion()", StringComparison.Ordinal)) continue;
                if (ConsultasPorPost.Any(c => tramo.Contains(c, StringComparison.Ordinal))) continue;
                var plano = Regex.Replace(tramo, @"\s+", " ");
                infracciones.Add($"{Path.GetFileName(archivo)}: {plano[..Math.Min(90, plano.Length)]}");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Una escritura de Cartera sin Idempotency-Key (FR-009)");
    }

    [Fact]
    public void Los_comandos_de_la_fase_son_idempotentes()
    {
        var sinClave = ComandosDeLaFase
            .Where(t => !typeof(IngenIA365ERP.Application.Common.Behaviors.IOperacionIdempotente).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();
        ReglaSobreFuente.SinInfracciones(sinClave, "Un comando de Cartera sin IOperacionIdempotente (FR-009)");
    }

    [Fact]
    public void Cada_archivo_de_rutas_de_escritura_exige_la_clave()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Un archivo de rutas de Cartera no exige Idempotency-Key (FR-009)");
    }
}
