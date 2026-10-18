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
        // US2 (bloque 6C, T0408, T0393): el envío de la simulación, las solicitudes, las decisiones, las centrales, la liberación de listas,
        // los cupos, los bloqueos, los saldos de aportes y los grupos conectados.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/SimulationsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ApplicationsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/DecisionsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/BureauQueriesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ScreeningResultsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CreditLimitsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/BlocksEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ContributionBalancesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ConnectedGroupsEndpoints.cs",
        // US3 (bloque 7C, T0428, T0457): el desembolso, las órdenes de giro, la anulación del crédito pendiente de giro, el archivo de
        // desembolsos, las garantías, el pagaré y la tesorería de prueba.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/DisbursementsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/DisbursementOrdersEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/LoansEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/DisbursementFilesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CollateralsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/PromissoryNotesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/TestDoublesEndpoints.cs",
        // US4 (bloque 8C, T0506): ordenar y reintentar corridas. IntegrityEndpoints no está: su única ruta es una consulta por POST.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/ProcessRunsEndpoints.cs",
        // US5 (bloque 9C, T0558): el reverso por pantalla y la preferencia; la simulación y la vista previa del reverso son consultas por POST.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/PaymentsEndpoints.cs",
        // US6 (bloque 10B, T0577, T0597): marcar no reprocesable un recibo (y la confirmación por excepción, en LoansEndpoints).
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/InboundMessagesEndpoints.cs",
        // US9 (bloque 12C, T0712): la política SARC, la calificación por análisis y la orden y la reapertura del cierre mensual.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/RiskEndpoints.cs",
        // US13 (bloque 14B, T0790): emitir, enviar y anular un documento; el enlace del PDF guardado (reimprimir) es "/download-link", sin clave.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/DocumentsEndpoints.cs",
        // K2, US10 (bloque 15C, T0850): convenios y su plantilla, archivos de descuento, no aplicados y el traslado; las dos rutas del doble van
        // en TestDoublesEndpoints. Sin clave sólo la vista previa del archivo ("/preview") y su enlace ("/download-link").
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/PayrollAgreementsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/PayrollDeductionFilesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/PayrollNotAppliedEndpoints.cs",
        // K3, US11 (bloque C, T0890, T0868): novedades, condonaciones, eventos del deudor, reclamación al seguro e insolvencia. Sin clave sólo la
        // simulación de la novedad ("/simulate", una consulta por POST).
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/NoveltiesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/WaiversEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/DebtorEventsEndpoints.cs",
        // K3, US14 (bloque C, T0915): el lote de castigo con el reporte al supervisor. La evaluación de cartera (/risk/evaluations) vive en
        // RiskEndpoints.cs, que ya está en la lista.
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/WriteOffBatchesEndpoints.cs",
        // K4, US12 (bloque C, T0956): la cobranza —etapas, reparto, gestiones, acuerdos, jurídico, campañas, gastos, avisos previos y reclamos—. La vista
        // previa de la campaña es una consulta por POST (sin clave).
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CollectionStagesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CollectorAssignmentsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CollectionActionsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/PaymentAgreementsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/LegalProcessesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CampaignsEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CollectionFeesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/PriorNoticesEndpoints.cs",
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/CreditReportClaimsEndpoints.cs",
        // K5, US16 (fase 21, bloque 3, T1008): generar el SICSES y los archivos de las centrales y marcar cargado. Sin clave sólo validar ("/validate",
        // no guarda) y el enlace de descarga ("/download-link").
        "Presentation/IngenIA365ERP.API/Endpoints/Lending/RegulatoryEndpoints.cs",
    ];

    /// <summary>
    /// Las escrituras que van sin clave (api.md §1.5): las consultas por POST por el tamaño del cuerpo. Se nombran por el final de la ruta.
    /// US2 (bloque 6C): la simulación y su PDF (el envío sí lleva clave) y los enlaces firmados de 60 s (repetirlos emite otro). US3 (bloque 7C):
    /// la vista previa del desembolso y la del archivo (no escriben) y actualizar el estado del pagaré (sólo trae lo que el depósito ya sabe;
    /// repetirlo no cambia nada).
    /// </summary>
    private static readonly string[] ConsultasPorPost =
        ["\"/preview\"", "/usury-check\"", "\"/schedule\"", "\"/schedule/pdf\"", "/download-link\"", "/disbursement-preview\"", "/refresh\"",
            // US4 (bloque 8C): verificar el libro (api.md §16.5: no corrige nada).
            "\"/verify\"",
            // US5 (bloque 9C): simular un pago, la vista previa de su reverso y la simulación del recaudador de prueba (no escriben).
            "/payment-simulations\"", "/reversal-preview\"", "\"/collector/simulations\"",
            // K3 (bloque C, T0890): simular la novedad (api.md §11.1, «sin clave»: no escribe).
            "/simulate\"",
            // K4 (bloque C, T0956): la vista previa de una campaña (api.md §14.9: «sin clave», no escribe).
            "/{id:guid}/preview\"",
            // K5 (fase 21, bloque 3, T1008): validar el SICSES o el archivo de una central (api.md §18, «no guarda; sin clave»).
            "/validate\""];

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
        // US3 (bloque 7C, T0428): el desembolso, el giro y su anulación, el archivo de desembolsos, las garantías, el pagaré y la tesorería de
        // prueba. Sin clave sólo RefreshPromissoryNoteStatusCommand (consulta al depósito, ConsultasPorPost) y el enlace de descarga.
        typeof(IngenIA365ERP.Application.Lending.Disbursements.ExecuteLoanDisbursementCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.VoidDisbursementOrderCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.ReissueDisbursementOrderCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.VoidPendingLoanCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.Files.GenerateLoanDisbursementFileCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.Files.VoidLoanDisbursementFileCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.Collaterals.RegisterCollateralCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.Collaterals.UpdateCollateralCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.Collaterals.ChangeCollateralStatusCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.Collaterals.ReleaseCollateralCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes.CreatePromissoryNoteCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes.RegisterPhysicalPromissoryNoteCustodyCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes.EndorsePromissoryNoteCommand),
        typeof(IngenIA365ERP.Application.Lending.Disbursements.PromissoryNotes.VoidPromissoryNoteCommand),
        typeof(IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba.EmitTestDisbursementResultCommand),
        // US4 (bloque 8C, T0506): ordenar y reintentar una corrida del ciclo diario.
        typeof(IngenIA365ERP.Application.Lending.Processes.RequestLendingProcessRunCommand),
        typeof(IngenIA365ERP.Application.Lending.Processes.RetryLendingProcessRunCommand),
        // US5 (bloque 9B): el reverso por pantalla, la preferencia del crédito y el recaudador de prueba. Los métodos del contrato no son
        // IOperacionIdempotente (T21): su clave es la de negocio del recaudador.
        typeof(IngenIA365ERP.Application.Lending.Payments.Commands.ReverseLoanPaymentCommand),
        typeof(IngenIA365ERP.Application.Lending.Payments.Commands.ChangePrepaymentPreferenceCommand),
        typeof(IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba.ApplyTestCollectorPaymentCommand),
        typeof(IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba.ReverseTestCollectorPaymentCommand),
        typeof(IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba.ResolveTestCollectorCreditBalanceCommand),
        // US6 (bloque 10B, T0577): confirmar por excepción una validación fallida y marcar no reprocesable un recibo.
        typeof(IngenIA365ERP.Application.Lending.Loans.Commands.ConfirmValidationFailureCommand),
        typeof(IngenIA365ERP.Application.Lending.Integration.Comercial.MarkLendingInboundMessageNotRetryableCommand),
        // US9 (bloque 12B, T0674): ordenar y reabrir el cierre (202), poner y levantar la calificación por análisis, y registrar y validar la
        // política SARC. RiskEndpoints.cs se suma a Archivos con sus rutas (bloque 12C).
        typeof(IngenIA365ERP.Application.Lending.Risk.OrderMonthlyCloseCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.ReopenMonthlyCloseCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.SetAnalysisRatingCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.LiftAnalysisRatingCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.AddRiskPolicyVersionCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.ValidateRiskPolicyVersionCommand),
        // US8 (bloque 13C, T0728): la carga de migración por partes, su aplicación, las marcas de ventas, la aceptación de diferencias, la
        // revisión de cobros provisionales (deja el adjunto) y la activación.
        typeof(IngenIA365ERP.Application.Lending.Migration.CreateMigrationLoadCommand),
        typeof(IngenIA365ERP.Application.Lending.Migration.ImportMigrationPartCommand),
        typeof(IngenIA365ERP.Application.Lending.Migration.ApplyMigrationLoadCommand),
        typeof(IngenIA365ERP.Application.Lending.Migration.MarkMigrationSaleCommand),
        typeof(IngenIA365ERP.Application.Lending.Migration.AcceptMigrationDifferencesCommand),
        typeof(IngenIA365ERP.Application.Lending.Activation.ReviewProvisionalCollectionsCommand),
        typeof(IngenIA365ERP.Application.Lending.Activation.ActivateLendingCommand),
        // US13 (bloque 14A, T0784–T0786): emitir, enviar y anular un documento. El enlace de descarga (reimprimir) es una lectura por POST sin clave,
        // como el del archivo de desembolsos (ConsultasPorPost "/download-link"): repetirlo emite otro enlace del mismo PDF y cada uno se audita
        // como reimpresión (decisión del bloque 14B, T0768 ajustada).
        typeof(IngenIA365ERP.Application.Lending.Documents.IssueLendingDocumentCommand),
        typeof(IngenIA365ERP.Application.Lending.Documents.SendLendingDocumentCommand),
        typeof(IngenIA365ERP.Application.Lending.Documents.VoidLendingDocumentCommand),
        // K2 (US10, bloque B; T0818): convenios y su plantilla, el archivo de descuentos, el traslado, los no aplicados y el recaudador de prueba de
        // la libranza. Sin clave sólo la vista previa del archivo (una consulta por POST) y su enlace de descarga (/download-link). Sus archivos de rutas
        // (PayrollAgreementsEndpoints, PayrollDeductionFilesEndpoints, PayrollNotAppliedEndpoints) están en Archivos desde el bloque C (T0850).
        typeof(IngenIA365ERP.Application.Lending.Payroll.Agreements.CreatePayrollAgreementCommand),
        typeof(IngenIA365ERP.Application.Lending.Payroll.Agreements.AddPayrollAgreementVersionCommand),
        typeof(IngenIA365ERP.Application.Lending.Payroll.Agreements.SetPayrollAgreementContactsCommand),
        typeof(IngenIA365ERP.Application.Lending.Imports.ImportPayrollAgreementsCommand),
        typeof(IngenIA365ERP.Application.Lending.Payroll.Files.GeneratePayrollDeductionFileCommand),
        typeof(IngenIA365ERP.Application.Lending.Payroll.Files.VoidPayrollDeductionFileCommand),
        typeof(IngenIA365ERP.Application.Lending.Payroll.Transfers.TransferLoanPayrollCommand),
        typeof(IngenIA365ERP.Application.Lending.Payroll.NotApplied.ResolveInstallmentNonDeductionCommand),
        typeof(IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba.ConfirmTestPayrollFileSentCommand),
        typeof(IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba.EmitTestPayrollDeductionResultCommand),
        // K3 (US11, bloque B; T0868): novedades (radicar, enviar, consentimiento, aplicar, anular), condonaciones (pedir, aplicar, anular), eventos del
        // deudor, reclamación al seguro e insolvencia. La simulación de la novedad es una consulta por POST (sin clave). Sus archivos de rutas
        // (NoveltiesEndpoints, WaiversEndpoints, DebtorEventsEndpoints) están en Archivos desde el bloque C (T0890).
        typeof(IngenIA365ERP.Application.Lending.Novelties.RequestLoanNoveltyCommand),
        typeof(IngenIA365ERP.Application.Lending.Novelties.SubmitLoanNoveltyCommand),
        typeof(IngenIA365ERP.Application.Lending.Novelties.RegisterNoveltyConsentCommand),
        typeof(IngenIA365ERP.Application.Lending.Novelties.ApplyLoanNoveltyCommand),
        typeof(IngenIA365ERP.Application.Lending.Novelties.CancelLoanNoveltyCommand),
        typeof(IngenIA365ERP.Application.Lending.Waivers.RequestWaiverCommand),
        typeof(IngenIA365ERP.Application.Lending.Waivers.ApplyWaiverCommand),
        typeof(IngenIA365ERP.Application.Lending.Waivers.CancelWaiverCommand),
        typeof(IngenIA365ERP.Application.Lending.DebtorEvents.RegisterDebtorEventCommand),
        typeof(IngenIA365ERP.Application.Lending.DebtorEvents.ChangeInsuranceClaimStatusCommand),
        typeof(IngenIA365ERP.Application.Lending.DebtorEvents.OpenInsolvencyProceedingCommand),
        typeof(IngenIA365ERP.Application.Lending.DebtorEvents.RecordInsolvencyFailureCommand),
        // K3 (US14, bloque B; T0901): el lote de castigo (armar, agregar y quitar créditos, enviar, aplicar, el reporte al supervisor) y la evaluación de
        // cartera (preparar y aplicar). Sus archivos de rutas (WriteOffBatchesEndpoints y /risk/evaluations en RiskEndpoints) están en Archivos desde el bloque C (T0915).
        typeof(IngenIA365ERP.Application.Lending.WriteOffs.CreateWriteOffBatchCommand),
        typeof(IngenIA365ERP.Application.Lending.WriteOffs.AddWriteOffBatchItemCommand),
        typeof(IngenIA365ERP.Application.Lending.WriteOffs.RemoveWriteOffBatchItemCommand),
        typeof(IngenIA365ERP.Application.Lending.WriteOffs.SubmitWriteOffBatchCommand),
        typeof(IngenIA365ERP.Application.Lending.WriteOffs.ApplyWriteOffBatchCommand),
        typeof(IngenIA365ERP.Application.Lending.WriteOffs.RegisterWriteOffSupervisorReportCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.Evaluations.PreparePortfolioEvaluationCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.Evaluations.ApplyPortfolioEvaluationCommand),
        // K3, bloque D (decisiones del dueño del 2026-10-15): la excepción de cobertura que aprueba otra persona, anular el lote y descartar la evaluación.
        typeof(IngenIA365ERP.Application.Lending.Novelties.ApproveNoveltyCoverageExceptionCommand),
        typeof(IngenIA365ERP.Application.Lending.WriteOffs.VoidWriteOffBatchCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.Evaluations.DiscardPortfolioEvaluationCommand),
        // K4 (US12, bloques B y C; T0932): la cobranza. La vista previa de la campaña (consulta) y las listas no son comandos.
        typeof(IngenIA365ERP.Application.Lending.Collections.Stages.AddCollectionStageVersionCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Assignments.CreateCollectorAssignmentRuleCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Assignments.CloseCollectorAssignmentRuleCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Assignments.AssignCollectorCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Assignments.DistributeCollectionsCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Actions.RecordCollectionActionCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Actions.VoidCollectionActionCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Agreements.CreatePaymentAgreementCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Agreements.ApprovePaymentAgreementCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Agreements.RejectPaymentAgreementCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Agreements.CancelPaymentAgreementCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Legal.OpenLegalProcessCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Legal.RecordLegalProcessActionCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Legal.CloseLegalProcessCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Fees.CauseCollectionFeesCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Campaigns.CreateMessageCampaignCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Campaigns.ScheduleMessageCampaignCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Campaigns.CancelMessageCampaignCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.PriorNotices.SendPriorNoticeCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.PriorNotices.RegisterPriorNoticeEvidenceCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Claims.RegisterCreditReportClaimCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Claims.ExtendCreditReportClaimCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Claims.AnswerCreditReportClaimCommand),
        typeof(IngenIA365ERP.Application.Lending.Collections.Claims.WithdrawCreditReportClaimCommand),
        // K5 (US16, fase 21, T1004): generar el SICSES y el archivo de una central, y marcar cargado. Validar y el enlace de descarga no son comandos
        // con clave.
        typeof(IngenIA365ERP.Application.Lending.Regulatory.GenerateSicsesFileCommand),
        typeof(IngenIA365ERP.Application.Lending.Regulatory.GenerateCreditBureauFileCommand),
        typeof(IngenIA365ERP.Application.Lending.Regulatory.MarkRegulatoryFileUploadedCommand),
        // K6a (fase 22, bloque 3, T1038): la adopción o el desistimiento voluntario del modelo y la carga complementaria del historial de mora (sus
        // rutas viven en RiskEndpoints.cs, que ya está en Archivos).
        typeof(IngenIA365ERP.Application.Lending.Risk.RegisterImpairmentModelElectionCommand),
        typeof(IngenIA365ERP.Application.Lending.Risk.ImportArrearsHistoryCommand),
    ];

    /// <summary>
    /// Fase 24 (T1062): <c>MapPlantilla(</c> cuenta, porque <c>RutasDePlantilla</c> pone <c>.ConClaveDeOperacion()</c> en la importación. Con los
    /// comentarios fuera del recorrido (<see cref="ReglaSobreFuente.Faltantes"/>), <c>ContributionBalancesEndpoints</c> —cuya única escritura es la
    /// plantilla— pasaba sólo porque un comentario decía «Idempotency-Key».
    /// </summary>
    private static readonly Regex Requerido = new(@"Idempotency|ConClave|RequiereClave|\.MapPlantilla\(", RegexOptions.Compiled);

    /// <summary>
    /// US5 (T0526, T21): el contrato de obligaciones no es una escritura de pantalla: su clave es la de negocio del recaudador (<c>NOM:</c>,
    /// <c>TST:</c>, <c>CAR:</c>…) y la idempotencia la resuelve el contrato por <c>(CollectorCode, IdempotencyKey)</c>. Ni sus solicitudes ni su
    /// implementación son <c>IOperacionIdempotente</c>, que exigiría la <c>Idempotency-Key</c> de pantalla.
    /// </summary>
    [Fact]
    public void Los_metodos_del_contrato_no_son_operaciones_idempotentes_de_pantalla()
    {
        var idempotente = typeof(IngenIA365ERP.Application.Common.Behaviors.IOperacionIdempotente);
        Type[] delContrato =
        [
            typeof(IngenIA365ERP.Application.Common.Integration.Lending.SolicitudDeAplicacion),
            typeof(IngenIA365ERP.Application.Common.Integration.Lending.SolicitudDeReverso),
            typeof(IngenIA365ERP.Application.Common.Integration.Lending.SolicitudDeResolucion),
            typeof(IngenIA365ERP.Application.Common.Integration.Lending.SolicitudDeSimulacion),
            typeof(IngenIA365ERP.Application.Common.Integration.Lending.SolicitudDeListado),
            typeof(IngenIA365ERP.Application.Lending.Integration.ContratoDeObligacionesDeCartera),
        ];
        var infractores = delContrato.Where(idempotente.IsAssignableFrom).Select(t => t.Name).ToList();
        ReglaSobreFuente.SinInfracciones(infractores, "El contrato de obligaciones usa la clave de pantalla en vez de la de negocio (T21)");
    }

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
