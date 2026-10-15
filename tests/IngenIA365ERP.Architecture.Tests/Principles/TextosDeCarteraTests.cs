using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §7.6): ningún texto visible de Cartera muestra códigos técnicos ni el nombre del sistema
/// anterior. Molde: <c>TextosDeInventarioTests</c>.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0007) y la llena cada historia con sus archivos de textos y pantallas (<c>Services/Cartera/TextosDeCartera.cs</c>, <c>Pages/Cartera</c>); con la lista vacía la prueba pasa porque no hay nada que violar.</para>
/// </summary>
public class TextosDeCarteraTests
{
    /// <summary>Archivos de textos y pantallas de Cartera (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // US2 (T0410–T0413): los textos de la originación.
        "Presentation/IngenIA365ERP.Shared/Services/Cartera/TextosDeOriginacion.cs",
        // US4 (T0508–T0510): los textos de los créditos, la liquidación, el libro y las corridas.
        "Presentation/IngenIA365ERP.Shared/Services/Cartera/TextosDeCreditos.cs",
        // US6 (T0602, T0603): lo que llega a Cartera y las reservas de número (la pantalla nombra permisos en sus PermissionGate, como las demás).
        "Presentation/IngenIA365ERP.Shared/Services/Cartera/TextosDeIntegracion.cs",
        // US10 (T0852): los textos de la libranza.
        "Presentation/IngenIA365ERP.Shared/Services/Cartera/TextosDeLibranza.cs",
    ];

    private static readonly (Regex Patron, string Que)[] Prohibidos =
    [
        (new Regex("SOLIDO", RegexOptions.Compiled | RegexOptions.IgnoreCase), "nombra el sistema anterior"),
        (new Regex("\"Lending\\.[A-Z]\\w*\\.[A-Z]\\w*\"", RegexOptions.Compiled), "muestra un código de error como texto"),
    ];

    /// <summary>
    /// US17 (T0265): los archivos de errores de esta fase. Ahí los códigos son el contrato (<c>Lending.Session.QuorumNotMet</c>), así que la
    /// regla mira los demás textos —los mensajes que lee la persona—: ninguno lleva un código ni el nombre del sistema anterior.
    /// </summary>
    private static readonly string[] ArchivosDeErrores =
    [
        "Core/IngenIA365ERP.Application/Lending/Common/ErroresDeGobierno.cs",
        "Core/IngenIA365ERP.Application/Lending/Collections/ErroresDeMensajesAlDeudor.cs",
        // US1 (T0341): líneas, usura, puntaje y equivalencias; y lo que alerta la réplica.
        "Core/IngenIA365ERP.Application/Lending/Configuration/ErroresDeLineasDeCredito.cs",
        "Core/IngenIA365ERP.Application/Lending/Configuration/EfectoDeReplicacionUsuraEnLineas.cs",
        "Core/IngenIA365ERP.Domain/Lending/Scoring/ModeloDePuntaje.cs",
        // US2 (bloque 6A): la originación, la concentración, el simulador y los grupos conectados; y la explicación del puntaje.
        "Core/IngenIA365ERP.Application/Lending/Origination/ErroresDeOriginacion.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/ValidacionDeConcentracion.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/SimulationQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/ConnectedGroups/ConnectedGroupCommands.cs",
        "Core/IngenIA365ERP.Domain/Lending/Scoring/MotorDePuntaje.cs",
        // US2 (bloque 6B): decisiones, cupos, bloqueos, saldos de aportes, listas, re-escaneo y centrales.
        "Core/IngenIA365ERP.Application/Lending/Origination/ErroresDeDecisionesYCupos.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/DecisionSobreSolicitud.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/AsuntoDeActaDeSolicitud.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/AsuntoDeActaDeCupo.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/ConsultaDeListasDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/ScreeningResultCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/ReEscaneoDeListas.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/CreditLimits/CreditLimitCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/CreditLimits/DecisionSobreCupo.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/CreditLimits/RevalidacionDeCupos.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/CreditLimits/DebtorBlockCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Origination/CreditLimits/ContributionBalanceCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Adapters/BureauQueryCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Attributions/RecordCreditDecisionCommand.cs",
        // US8 (bloque 13A, T0745): la carga de la migración, sus plantillas, cuadres, conciliación y aceptación.
        "Core/IngenIA365ERP.Application/Lending/Migration/MigrationErrors.cs",
        "Core/IngenIA365ERP.Application/Lending/Activation/ErroresDeActivacion.cs",
        "Core/IngenIA365ERP.Application/Lending/Migration/ImportMigrationPartCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Migration/ApplyMigrationLoadCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Migration/AcceptMigrationDifferencesCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Migration/ConciliacionDeVentasDeMigracion.cs",
        "Core/IngenIA365ERP.Application/Lending/Migration/CargaVigenteDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/Imports/DefinicionesDeMigracion.cs",
        // US8 (bloque 13B, T0747–T0752): los requisitos, los cobros provisionales, el nacimiento de lo del deudor y la activación.
        "Core/IngenIA365ERP.Application/Lending/Activation/RequisitosDeActivacion.cs",
        "Core/IngenIA365ERP.Application/Lending/Activation/ValidacionDeCobrosProvisionales.cs",
        "Core/IngenIA365ERP.Application/Lending/Activation/ReviewProvisionalCollectionsCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Activation/NacimientoDeLoDelDeudorMigrado.cs",
        "Core/IngenIA365ERP.Application/Lending/Activation/ActivateLendingCommand.cs",
        // US13 (bloque 14A, T0788): los documentos al deudor, sus leyendas y lo que dice la entrega por correo.
        "Core/IngenIA365ERP.Application/Lending/Documents/DocumentErrors.cs",
        "Core/IngenIA365ERP.Application/Lending/Documents/IDocumentoDeCarteraRenderer.cs",
        "Core/IngenIA365ERP.Application/Lending/Documents/ConstructorDeDocumentosDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/Documents/SendLendingDocumentCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Documents/AnulacionDeDocumentosPorReverso.cs",
        // K2 (US10, bloque B; T0848): los errores de la libranza y los textos que lee la persona (avisos, alertas, plantilla, informes).
        "Core/IngenIA365ERP.Application/Lending/Payroll/ErroresDeLibranza.cs",
        "Core/IngenIA365ERP.Application/Lending/Payroll/Results/SeguimientoDeNoAplicados.cs",
        "Core/IngenIA365ERP.Application/Lending/Payroll/Results/ConsumoDeLibranza.cs",
        "Core/IngenIA365ERP.Application/Lending/Payroll/Tasks/TareasDeLibranza.cs",
        "Core/IngenIA365ERP.Application/Lending/Payroll/Transfers/TransferLoanPayrollCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Imports/PayrollAgreementsTemplate.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/PayrollReportQueries.cs",
        // K3 (US11, bloque B; T0888): los errores de novedades, condonaciones y eventos del deudor y los textos que lee la persona (motivos,
        // efectos, asuntos de acta, el informe de reestructurados).
        "Core/IngenIA365ERP.Application/Lending/Novelties/ErroresDeNovedades.cs",
        "Core/IngenIA365ERP.Application/Lending/Novelties/RequestLoanNoveltyCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Novelties/ApplyLoanNoveltyCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Novelties/DecisionSobreNovedad.cs",
        "Core/IngenIA365ERP.Application/Lending/Waivers/CondonacionesDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/Waivers/RequestWaiverCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Waivers/DecisionSobreCondonacion.cs",
        "Core/IngenIA365ERP.Application/Lending/Waivers/WaiverQueries.cs",
        "Core/IngenIA365ERP.Application/Lending/DebtorEvents/ErroresDeEventosDelDeudor.cs",
        "Core/IngenIA365ERP.Application/Lending/DebtorEvents/DebtorEventCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/DebtorEvents/InsuranceClaimAndInsolvencyCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/RestructuredReportQuery.cs",
        "Core/IngenIA365ERP.Domain/Entities/Lending/Operations/LoanNovelty.cs",
        "Core/IngenIA365ERP.Domain/Entities/Lending/Operations/Waiver.cs",
        "Core/IngenIA365ERP.Domain/Lending/Replay/NovedadEnLaReconstruccion.cs",
        // K3 (US14, bloque B; T0914): los errores y los textos del castigo, de la evaluación de cartera y de la vista de la cartera castigada.
        "Core/IngenIA365ERP.Application/Lending/WriteOffs/ErroresDeCastigo.cs",
        "Core/IngenIA365ERP.Application/Lending/WriteOffs/CastigosDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/WriteOffs/WriteOffBatchCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/WriteOffs/AsuntoDeLoteDeCastigo.cs",
        "Core/IngenIA365ERP.Application/Lending/WriteOffs/ApplyWriteOffBatchCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/WriteOffs/RegisterWriteOffSupervisorReportCommand.cs",
        "Core/IngenIA365ERP.Application/Lending/Risk/Evaluations/EvaluacionesDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/Risk/Evaluations/PortfolioEvaluationCommands.cs",
        "Core/IngenIA365ERP.Application/Lending/Risk/Evaluations/AsuntoDeEvaluacionDeCartera.cs",
        "Core/IngenIA365ERP.Application/Lending/Reports/Vistas/ChargeOffsReportQuery.cs",
        "Core/IngenIA365ERP.Domain/Entities/Lending/Operations/WriteOffBatch.cs",
        "Core/IngenIA365ERP.Domain/Entities/Lending/Risk/PortfolioEvaluation.cs",
        "Core/IngenIA365ERP.Domain/Lending/Risk/RequisitosDeCastigo.cs",
    ];

    private static readonly Regex Literal = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

    private static readonly Regex SoloCodigo = new(@"^[A-Za-z]+(\.[A-Za-z0-9]+)+$", RegexOptions.Compiled);

    private static readonly Regex CodigoEnTexto = new(@"\b(Lending|Approvals|Generic|Catalogo|Core)\.[A-Z]\w*\.[A-Z]\w*", RegexOptions.Compiled);

    [Fact]
    public void Los_mensajes_de_error_de_Cartera_no_muestran_codigos_ni_el_sistema_anterior()
    {
        var infracciones = new List<string>();
        foreach (var archivo in ReglaSobreFuente.ArchivosNombrados(ArchivosDeErrores))
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (Match m in Literal.Matches(texto))
            {
                var valor = m.Groups[1].Value;
                if (SoloCodigo.IsMatch(valor)) continue;
                if (valor.Contains("SOLIDO", StringComparison.OrdinalIgnoreCase)) infracciones.Add($"{Path.GetFileName(archivo)}: «{valor}» nombra el sistema anterior");
                if (CodigoEnTexto.IsMatch(valor)) infracciones.Add($"{Path.GetFileName(archivo)}: «{valor}» muestra un código");
            }
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Mensajes de error de Cartera con códigos o el nombre del sistema anterior (§7.6)");
    }

    [Fact]
    public void Los_textos_de_Cartera_no_muestran_codigos_ni_el_sistema_anterior()
    {
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosNombrados(Archivos), Prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Textos de Cartera con códigos técnicos o el nombre del sistema anterior (§7.6)");
    }
}
