using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-004 y SC-015 (DT §2.25, T62): toda consulta de Cartera con datos de deudores filtra por el
/// alcance de oficina y línea del usuario, y falla cerrado.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0006) y la llena cada historia con sus handlers de consulta; con la lista vacía la prueba pasa porque no hay nada que violar.</para>
///
/// <para>
/// US17 (T0259, T0273) la llena y la cierra: cada consulta de <c>Application/Lending</c> (un <c>record …Query</c>) o está en un archivo que
/// aplica el alcance (<see cref="Archivos"/>) o está en <see cref="Exceptuadas"/> con su motivo. Una consulta nueva que no está en ninguna de
/// las dos rompe la prueba: quien la escribe decide si filtra o por qué no.
/// </para>
/// </summary>
public class LasConsultasDeCarteraRespetanElAlcance
{
    /// <summary>Handlers de consulta de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // US17 (T0259): reclamos sobre el reporte por la oficina del crédito; la vista previa de la matriz por la línea; el alcance de un
        // usuario (las líneas que asigna quien asigna); la bandeja de alertas de Cartera (C68).
        "Core/IngenIA365ERP.Application/Lending/Collections/ListCreditReportClaimsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Attributions/AttributionMatrixCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Common/LendingUserScopeCommands.cs",
        "Core/IngenIA365ERP.Application/Common/Alerts/VisibilidadDeAlertas.cs",
        // US17 (T0273): la cola de mensajes al deudor y la vista previa de una plantilla con un crédito de ejemplo.
        "Core/IngenIA365ERP.Application/Lending/Collections/DebtorMessageQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Collections/MessageTemplateQueries.cs",
        // US1 (T0341): las líneas son el eje del alcance por línea. La lista filtra por PorLinea; el detalle, la versión, la consulta de usura
        // y el puntaje de una línea fuera del alcance responden el 404 de la línea (ConsultasDeLineas.LineaEnAlcanceAsync).
        "Core/IngenIA365ERP.Application/Lending/Configuration/CreditLineQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Configuration/CreditLineScoringCommands.cs",
        // US2 (bloque 6A): la simulación y su PDF por la línea del alcance (SimulacionEnAlcance → LineaEnAlcanceAsync); las solicitudes por
        // oficina y línea (la lista con PorOficinaYLinea; el detalle y el enrutamiento con SolicitudEnAlcance, el 404 de la solicitud).
        "Core/IngenIA365ERP.Application/Lending/Origination/SimulationQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/CreditApplicationQueries.cs",
        // US2 (bloque 6B): las consultas a centrales (las de una solicitud fuera del alcance no aparecen y su detalle es el 404), «Mis pendientes»
        // (solicitudes por oficina y línea, cupos por línea) y los cupos (por la línea del alcance; el consolidado y el rotativo sin línea son de
        // la persona).
        "Core/IngenIA365ERP.Application/Lending/Adapters/BureauQueryCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Attributions/ListPendingLendingDecisionsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/CreditLimits/CreditLimitQueries.cs",
        // US3 (bloque 7A, parte de T0428): los tramos y las órdenes de giro por la oficina y la línea de su crédito (la lista filtra en silencio;
        // el detalle responde el 404 de la orden).
        "Core/IngenIA365ERP.Application/Lending/Disbursements/ListLoanDisbursementsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Disbursements/ListDisbursementOrdersQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Disbursements/GetDisbursementOrderQuery.cs",
        // US3 (bloque 7B, parte de T0428): las garantías y los pagarés por la oficina y la línea de su solicitud o su crédito (la lista filtra en
        // silencio; el detalle responde el 404 de la garantía); los archivos de desembolsos que llevan alguna orden de un crédito del alcance.
        "Core/IngenIA365ERP.Application/Lending/Disbursements/Collaterals/ListCollateralsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Disbursements/Collaterals/GetCollateralQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Disbursements/PromissoryNotes/ListPromissoryNotesQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Disbursements/Files/ListDisbursementFilesQuery.cs",
        // US4 (bloque 8B, T0503, T0504): los créditos por la oficina y la línea (la lista con PorOficinaYLinea; el detalle, el plan, las
        // operaciones, el comprobante y el libro con CreditoEnAlcance, el 404 del crédito); la liquidación y el estado de cuenta con
        // LecturaDelCredito (el mismo 404 y Lending.Access.OutOfScope).
        "Core/IngenIA365ERP.Application/Lending/Loans/ListLoansQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Loans/GetLoanQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Loans/GetRepaymentScheduleQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Loans/ListRepaymentScheduleVersionsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Loans/ListLoanOperationsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Loans/GetLoanOperationQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Loans/ListLoanTransactionsQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Settlement/GetLoanSettlementQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Settlement/GetLoanStatementQuery.cs",
        // US5 (bloque 9B, T0552–T0554): la simulación de pantalla, la vista previa del reverso y la preferencia del crédito, por CreditoEnAlcance.
        "Core/IngenIA365ERP.Application/Lending/Payments/Queries/SimulateLoanPaymentQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Payments/Queries/PreviewLoanPaymentReversalQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Payments/Queries/GetPrepaymentPreferenceQuery.cs",
        // US6 (bloque 10A, T0595, T0596): los recibos de lo que llega a Cartera por la oficina y la línea de su crédito (uno sin crédito —rechazado o
        // no incorporado— no es de ninguna oficina; el detalle responde el 404 del recibo) y las reservas de número por las de su crédito.
        "Core/IngenIA365ERP.Application/Lending/Integration/Comercial/MensajesRecibidos.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/Queries/ListLoanNumberReservationsQuery.cs",
        // US7 (bloque B, T0658; parte de T0610): la conciliación contable por las oficinas del alcance (y una sucursal fuera de él es el 404), los
        // hechos contables por la sucursal de su sobre y una parte por su oficina (fuera, Lending.AccountingFactPart.NotFound).
        "Core/IngenIA365ERP.Application/Lending/Integration/Queries/LendingAccountingQueries.cs",
        // US9 (bloque 12B, T0674): la calificación informativa y la de un crédito (la lista por oficina y línea; el crédito por CreditoEnAlcance), las
        // calificaciones por análisis (por la oficina y la línea de su crédito), las líneas y el reporte de cambios de un cierre (PorOficinaYLinea),
        // el cuadre del cierre (la conciliación con el alcance de oficinas) y la completitud del historial de mora (los créditos del alcance).
        "Core/IngenIA365ERP.Application/Lending/Risk/RatingQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Risk/AnalysisRatingCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Risk/MonthlyCloseQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Risk/ImpairmentModelQueries.cs",
        // US13 (bloque 14A, T0785): la lista y el detalle de documentos emitidos filtran por los créditos que cubren (DocumentosEmitidos.Visibles).
        // La verificación no mira la oficina: devuelve sólo los datos esenciales a una persona de la cooperativa (D21).
        "Core/IngenIA365ERP.Application/Lending/Documents/IssuedDocumentQueries.cs",
        // US15 (bloque 14C, T0773, T0797): las siete vistas de K1 del centro de informes leen los créditos por AmbitoDeInformeDeCartera, que
        // aplica PorOficinaYLinea en la consulta; los mensajes por ProyeccionDeMensajesAlDeudor.ConAlcance; la conciliación de la migración, de
        // toda la cooperativa, sólo con el alcance total (si no, vacía).
        "Core/IngenIA365ERP.Application/Lending/Reports/AmbitoDeInformeDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/AgingReportQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/PlacementReportQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/ReceiptsReportQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/TransactionsReportQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/UsuryReportQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/DebtorMessagesReportQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/MigrationReconciliationReportQuery.cs",
        // K2 (US10, bloque B; T0818): los archivos de descuento y sus líneas (por los créditos de sus líneas, el 404 fuera), los no aplicados, lo
        // pendiente de giro y las dos vistas de la libranza.
        "Core/IngenIA365ERP.Application/Lending/Payroll/Files/PayrollDeductionFileQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Payroll/NotApplied/InstallmentNonDeductionCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Payroll/Agreements/PendingPayrollTransfersQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/PayrollReportQueries.cs",
        // K3 (US11, bloque B; T0868): las novedades y las condonaciones por la oficina y la línea de su crédito (la lista con PorOficinaYLinea; el
        // detalle y la simulación con NovedadesDeCartera.LeerAsync / CondonacionesDeCartera.LeerAsync, el 404 del asunto); las reclamaciones al
        // seguro por las de su crédito; la vista de reestructurados por AmbitoDeInformeDeCartera.
        "Core/IngenIA365ERP.Application/Lending/Novelties/NoveltyQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Novelties/SimulateLoanNoveltyQuery.cs",
        "Core/IngenIA365ERP.Application/Lending/Waivers/WaiverQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/DebtorEvents/InsuranceClaimAndInsolvencyCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/RestructuredReportQuery.cs",
        // K3 (US14, bloque B; T0901): los lotes de castigo (la lista sólo con los que tienen todos sus créditos en el alcance; el detalle con
        // CastigosDeCartera.LeerAsync, el 404 del lote), las evaluaciones de cartera (la lista con las que tienen algún crédito del alcance; el detalle
        // sólo con sus créditos del alcance) y la vista charge-offs por AmbitoDeInformeDeCartera.
        "Core/IngenIA365ERP.Application/Lending/WriteOffs/WriteOffBatchQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Risk/Evaluations/PortfolioEvaluationQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/ChargeOffsReportQuery.cs",
    ];

    /// <summary>
    /// US15 (T0773, DT §2.25): toda consulta del centro de informes de Cartera (<c>Application/Lending/Reports</c>) y la del tablero
    /// (<c>GetLendingDashboardQuery</c>, de K5: mientras no exista, la regla queda preparada) recibe <see cref="IngenIA365ERP.Application.Common.Interfaces.Security.IAlcanceDeCartera"/>
    /// en su handler y lee el alcance (<c>ObtenerAsync</c> o el ámbito de informe, que lo lee); ninguna se exceptúa: una vista sin alcance mostraría
    /// otra oficina.
    /// </summary>
    [Fact]
    public void Las_consultas_del_centro_de_informes_y_del_tablero_piden_el_alcance_en_su_handler()
    {
        var raiz = RepoPath.FindRepoRoot();
        var lending = Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending");
        var archivos = Directory.Exists(Path.Combine(lending, "Reports"))
            ? Directory.EnumerateFiles(Path.Combine(lending, "Reports"), "*.cs", SearchOption.AllDirectories).ToList()
            : [];
        archivos.AddRange(Directory.EnumerateFiles(lending, "*.cs", SearchOption.AllDirectories)
            .Where(a => FuenteSinComentarios.Leer(a).Contains("record GetLendingDashboardQuery", StringComparison.Ordinal)));

        var infracciones = new List<string>();
        var consultas = 0;
        foreach (var archivo in archivos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in Consulta.Matches(texto))
            {
                consultas++;
                var nombre = m.Groups[1].Value;
                if (Exceptuadas.ContainsKey(nombre)) infracciones.Add($"{nombre}: una vista del centro de informes no se exceptúa del alcance");
                if (!Regex.IsMatch(texto, $@"class {nombre}Handler\([^)]*IAlcanceDeCartera[^)]*\)[\s\S]*?(\.ObtenerAsync\(|AmbitoDeInformeDeCartera\.LeerAsync\()"))
                    infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {nombre}Handler no recibe IAlcanceDeCartera ni lee el alcance");
            }
        }
        Assert.True(consultas >= 7, $"Se esperaban al menos las siete vistas de K1 en Application/Lending/Reports y se encontraron {consultas}.");
        ReglaSobreFuente.SinInfracciones(infracciones, "Una consulta del centro de informes de Cartera no aplica el alcance (FR-004, SC-015)");
    }

    /// <summary>Consultas de <c>Application/Lending</c> que no filtran por oficina ni línea, con el motivo.</summary>
    private static readonly Dictionary<string, string> Exceptuadas = new(StringComparer.Ordinal)
    {
        // Catálogos y configuración sin oficina (T0259).
        ["ListAttributionMatricesQuery"] = "catálogo de la cooperativa: la matriz no tiene oficina",
        ["GetAttributionMatrixQuery"] = "catálogo de la cooperativa: la matriz no tiene oficina",
        ["ListGoverningBodiesQuery"] = "catálogo de la cooperativa: los órganos no tienen oficina",
        ["GetGoverningBodyQuery"] = "catálogo de la cooperativa: los órganos no tienen oficina",
        ["ListLendingTemplatesQuery"] = "registro de plantillas vacías: no trae datos",
        // US13 (bloque 14B, T0781): el logotipo y la firma de los documentos son de la cooperativa, no de una oficina.
        ["GetLendingDocumentImagesQuery"] = "configuración de la cooperativa: el logotipo y la imagen de la firma no tienen oficina",
        ["GetLendingTemplateQuery"] = "definición de una plantilla vacía: no trae datos",
        ["ListMessageTemplatesQuery"] = "plantillas de mensaje: textos de la cooperativa, sin deudores",
        ["GetMessageTemplateHistoryQuery"] = "plantillas de mensaje: textos de la cooperativa, sin deudores",
        // Actas y vinculados (T0259, decisión del bloque F): el acta es del órgano y los vinculados son personas por su vínculo con la
        // cooperativa; ninguno tiene oficina. Las dos lecturas quedan auditadas (LasLecturasSensiblesDeCarteraSeAuditan) y el alcance de cada
        // sujeto del acta lo aplica su historia cuando lo describe (IAsuntoDeActa).
        ["ListSessionsQuery"] = "el acta es del órgano, no de una oficina",
        ["GetSessionQuery"] = "el acta es del órgano, no de una oficina (lectura auditada)",
        ["ListSessionItemsQuery"] = "puntos de actas cerradas del órgano, no de una oficina",
        ["GetSessionMinutesPdfQuery"] = "el acta es del órgano, no de una oficina (lectura auditada)",
        ["ListRelatedPartiesQuery"] = "los vinculados son personas por su vínculo con la cooperativa, sin oficina (lectura auditada)",
        // Migración y activación (fase 3): de toda la cooperativa, con Lending.Migration.Load o Lending.Activation.Execute.
        ["ListMigrationLoadsQuery"] = "la carga de migración es de toda la cooperativa",
        ["GetMigrationLoadQuery"] = "la carga de migración es de toda la cooperativa",
        // US8 (bloque 13A): el cuadre y la conciliación de ventas de la carga son de toda la cooperativa (el cuadre sin datos de deudores, C85).
        ["GetMigrationBalanceCheckQuery"] = "el cuadre de la carga es de toda la cooperativa y no trae datos de deudores",
        ["GetMigrationSalesReconciliationQuery"] = "la conciliación de ventas de la carga es de toda la cooperativa",
        ["GetLendingActivationStatusQuery"] = "la activación es de toda la cooperativa",
        // US1 (T0341): lo nacional y lo de la cooperativa que no es de una línea.
        ["ListModalitiesQuery"] = "documento nacional CatalogoDeModalidades: sin oficina ni línea",
        ["ListCommercialTermEquivalencesQuery"] = "equivalencias de Comercial: son de la cooperativa, no de una línea",
        // US2 (T0393): los grupos conectados son personas por su vínculo económico o jurídico, sin oficina; las dos lecturas quedan auditadas.
        ["ListConnectedGroupsQuery"] = "los grupos conectados son personas por su vínculo, sin oficina (lectura auditada)",
        ["GetConnectedGroupQuery"] = "los grupos conectados son personas por su vínculo, sin oficina (lectura auditada)",
        // US2 (bloque 6B): el oficial de cumplimiento no es un rol de Cartera (C84): ve los resultados de listas con su permiso, sin el alcance de
        // oficina y línea; los bloqueos y los saldos de aportes son de la persona, sin oficina.
        ["ListScreeningResultsQuery"] = "el oficial de cumplimiento no lleva el alcance de Cartera: exige Compliance.RestrictiveLists.Release (C84)",
        ["GetScreeningResultQuery"] = "el oficial de cumplimiento no lleva el alcance de Cartera: exige Compliance.RestrictiveLists.Release (C84)",
        ["ListDebtorBlocksQuery"] = "los bloqueos y embargos son de la persona, no de una oficina",
        ["ListContributionBalancesQuery"] = "el saldo de aportes es de la persona, no de una oficina (D19)",
        // US3 (bloque 7A): la vista previa evalúa por RequisitosDelDesembolso, que asegura el alcance de la solicitud (el 404 fuera de él).
        ["PreviewDisbursementQuery"] = "la asegura RequisitosDelDesembolso con el alcance de la solicitud (AsegurarAsync, el 404 de la solicitud)",
        // US3 (bloque 7B): la vista previa del archivo toma las órdenes por PreparacionDeArchivoDeDesembolsos, que filtra por el alcance de su
        // crédito; las cuentas de origen son cuentas bancarias del plan (Contabilidad), sin deudores ni oficina.
        ["PreviewDisbursementFileQuery"] = "la filtra PreparacionDeArchivoDeDesembolsos: sólo órdenes de créditos del alcance (PorOficinaYLinea)",
        ["ListDisbursementSourceAccountsQuery"] = "cuentas bancarias del plan de cuentas por IContabilidadParaCartera: sin deudores ni oficina",
        ["ListTestTreasuryOrdersQuery"] ="doble de prueba de Tesorería fuera de producción (T20): ve lo que recibe la tesorería, no una oficina",
        // US4 (bloque 8B, T0499, T0505): las corridas son de toda la cooperativa y no traen datos del deudor (Lending.Processes.View); la
        // verificación del libro es de toda la cartera, del auditor (Lending.Integrity.Verify), y sólo devuelve números de crédito y cifras.
        ["ListLendingProcessRunsQuery"] = "las corridas son de toda la cooperativa y no traen datos del deudor (Processes.View)",
        ["GetLendingProcessRunQuery"] = "las corridas son de toda la cooperativa y no traen datos del deudor (Processes.View)",
        ["VerifyLendingLedgerIntegrityQuery"] = "la verificación del libro es de toda la cartera (Integrity.Verify, el auditor): número del crédito y cifras",
        // US5 (bloque 9B, T0556): el recaudador de prueba llama al contrato de obligaciones, que es un puerto sin el alcance de Cartera (FR-004).
        ["ListTestCollectorObligationsQuery"] = "doble de prueba de Recaudo fuera de producción (T20): llama al contrato, que no aplica el alcance (FR-004)",
        ["SimulateTestCollectorPaymentQuery"] = "doble de prueba de Recaudo fuera de producción (T20): llama al contrato, que no aplica el alcance (FR-004)",
        // US7: el catálogo sembrado de tipos de hecho (LND_FactTypes), de la cooperativa y sin deudores.
        ["ListLendingFactTypesQuery"] = "catálogo de la cooperativa: los tipos de hecho no tienen oficina ni deudores",
        ["ListTestCollectorReversalsQuery"] = "doble de prueba de Recaudo fuera de producción (T20): ve lo que recibe el recaudador, no una oficina",
        // US9 (bloque 12B): lo de toda la cooperativa, sin datos de deudores.
        ["GetRiskPolicyQuery"] = "la política SARC es del consejo y la norma es nacional: celdas y versiones, sin deudores ni oficina (Parameters.View)",
        ["GetMonthlyCloseReadinessQuery"] = "los requisitos del cierre son de toda la cooperativa: conteos y estados, sin deudores (MonthlyClose.Execute)",
        // K2 (US10, bloque B): los convenios son un catálogo de la cooperativa; el archivo de una pagaduría lleva todas sus obligaciones.
        ["ListPayrollAgreementsQuery"] = "catálogo de la cooperativa: un convenio de libranza no tiene oficina (contactos sin documento, C84)",
        ["GetPayrollAgreementQuery"] = "catálogo de la cooperativa: un convenio de libranza no tiene oficina (contactos sin documento, C84)",
        ["PreviewPayrollDeductionFileQuery"] =
            "el archivo de una pagaduría lleva todas las obligaciones del convenio, de cualquier oficina: lo prepara quien genera (PayrollDeductionFiles.Generate)",
        // K3 (US11, bloque B; T0868): los eventos del deudor se guardan como bloqueos de la persona y el proceso de insolvencia es de la persona.
        ["ListDebtorEventsQuery"] = "los eventos del deudor son bloqueos de la persona, no de una oficina (como ListDebtorBlocksQuery)",
        ["ListInsolvencyProceedingsQuery"] = "el proceso de insolvencia es de la persona y alcanza todos sus créditos, de cualquier oficina (FR-098)",
    };

    private static readonly Regex Requerido = new(@"AlcanceDeCartera", RegexOptions.Compiled);

    private static readonly Regex Consulta = new(@"public\s+sealed\s+record\s+(\w+Query)\b", RegexOptions.Compiled);

    [Fact]
    public void Cada_consulta_filtra_por_el_alcance()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), Requerido);
        ReglaSobreFuente.SinInfracciones(faltantes, "Una consulta de Cartera no filtra por el alcance (SC-015)");
    }

    /// <summary>
    /// US1 (T0341): la lista de líneas filtra en silencio por la línea (<c>PorLinea</c>) y el detalle, la versión, la consulta de usura y el
    /// puntaje pasan por la guarda que responde el 404 de la línea fuera del alcance.
    /// </summary>
    [Fact]
    public void Las_consultas_de_lineas_filtran_por_la_linea_o_responden_el_404_de_la_linea()
    {
        var raiz = RepoPath.FindRepoRoot();
        var consultas = FuenteSinComentarios.Leer(Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Configuration", "CreditLineQueries.cs"));
        var puntaje = FuenteSinComentarios.Leer(Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Configuration", "CreditLineScoringCommands.cs"));
        var comunes = FuenteSinComentarios.Leer(Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending", "Configuration", "CreditLineCommands.cs"));

        Assert.Matches(@"class ListCreditLinesQueryHandler[\s\S]*?\.PorLinea\(alcance", consultas);
        foreach (var handler in new[] { "GetCreditLineQueryHandler", "GetCreditLineVersionQueryHandler", "CheckCreditLineUsuryQueryHandler" })
            Assert.Matches($@"class {handler}[\s\S]*?LineaEnAlcanceAsync\(", consultas);
        Assert.Matches(@"class GetCreditLineScoringQueryHandler[\s\S]*?LineaEnAlcanceAsync\(", puntaje);
        Assert.Matches(@"LineaEnAlcanceAsync[\s\S]*?AsegurarAsync\(null, linea\.Id, ErroresDeLineasDeCredito\.LineaInexistente\(\)", comunes);
    }

    /// <summary>
    /// US7 (T0610): las tres consultas de la integración contable vistas desde Cartera piden el alcance en su handler —la conciliación por las
    /// oficinas (y una sucursal fuera de él es el 404), los hechos por la sucursal de su sobre, una parte por su oficina—.
    /// </summary>
    [Fact]
    public void Las_consultas_contables_de_Cartera_piden_el_alcance_en_su_handler()
    {
        var consultas = FuenteSinComentarios.Leer(Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Lending",
            "Integration", "Queries", "LendingAccountingQueries.cs"));
        foreach (var handler in new[] { "GetLendingAccountingReconciliationQueryHandler", "ListLendingAccountingFactsQueryHandler", "GetAccountingFactPartQueryHandler" })
            Assert.Matches($@"class {handler}\([^)]*IAlcanceDeCartera[\s\S]*?\.ObtenerAsync\(", consultas);
    }

    [Fact]
    public void Cada_consulta_de_Cartera_filtra_o_esta_exceptuada_con_motivo()
    {
        var raiz = RepoPath.FindRepoRoot();
        var conAlcance = Archivos.Select(a => Path.GetFullPath(Path.Combine(raiz, "src", a))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var carpeta = Path.Combine(raiz, "src", "Core", "IngenIA365ERP.Application", "Lending");

        var encontradas = new HashSet<string>(StringComparer.Ordinal);
        var infracciones = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories))
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in Consulta.Matches(texto))
            {
                var nombre = m.Groups[1].Value;
                encontradas.Add(nombre);
                if (conAlcance.Contains(Path.GetFullPath(archivo)) || Exceptuadas.ContainsKey(nombre)) continue;
                infracciones.Add($"{Path.GetRelativePath(raiz, archivo)}: {nombre} no aplica el alcance ni está exceptuada con motivo");
            }
        }

        foreach (var (nombre, motivo) in Exceptuadas)
        {
            if (string.IsNullOrWhiteSpace(motivo)) infracciones.Add($"{nombre}: exceptuada sin motivo");
            if (!encontradas.Contains(nombre)) infracciones.Add($"{nombre}: exceptuada pero ya no existe en Application/Lending (quitála de la lista)");
        }

        ReglaSobreFuente.SinInfracciones(infracciones, "Consultas de Cartera sin alcance ni excepción (FR-004, SC-015)");
    }
}
