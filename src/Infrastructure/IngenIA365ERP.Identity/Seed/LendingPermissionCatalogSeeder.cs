using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 013 (T60, T0220; decisiones-transversales §2.14; contracts/api.md §2.1; C85) — los 123 permisos <c>Lending.*</c> de la Cartera
/// nueva para <c>SEC_Permissions</c>, hermano de <see cref="InventoryPermissionCatalogSeeder"/>: misma convención
/// <c>Resource.Action</c>, misma idempotencia (inserta los que faltan y pone al día la descripción). Lo invoca
/// <see cref="PhaseZeroSecuritySeeder"/> antes de <see cref="BuiltInRolesSeeder"/>, nunca el DbMigrator.
///
/// <para>
/// Se siembra <b>entero en K1</b>, también lo de K2 a K6b, para que las plantillas de rol (<c>PerfilesSugeridos</c>) no cambien de
/// forma entre entregas. Lectura con datos del deudor = <c>Read</c>; <c>View</c> sólo para catálogos, parámetros, tableros y
/// bandejas sin datos personales (los roles integrados Operator, ReadOnly y Auditor reciben todo <c>*.View</c> por su glob y nada
/// más del módulo; <c>LosCodigosViewDeCarteraNoTienenDatosPersonales</c>). Crear, decidir, ejecutar y anular son acciones distintas
/// (segregación). A <c>CompanyAdmin</c> se le excluyen <c>Lending.Decisions.*</c> y <c>Lending.Sessions.Vote</c>
/// (<see cref="BuiltInRolesSeeder"/>, T61): administrar no es aprobar crédito; desde la feature 014 también
/// <c>Lending.NegotiatedRates.Approve</c> (decisión del dueño del 2026-10-18).
/// </para>
///
/// <para>
/// Feature 014 (T018; contracts/api.md §2; research §0.7): suma los <b>39</b> de captaciones y aportes, enteros desde K7 también los de K8 a
/// K11 —162 en total; 163 desde la decisión del dueño del 2026-10-18 (D44), con <c>Lending.MembershipWithdrawals.ReverseAfterPayment</c>—. Misma convención: <c>Lending.Accounts.Read</c> trae datos del titular; <c>Lending.Accounts.View</c>, sólo conteos y
/// saldos agregados.
/// </para>
/// </summary>
public static class LendingPermissionCatalogSeeder
{
    public const string Prefijo = "Lending.";

    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        // ------------------------------------------------------------------------------------------------- configuración --
        ("Lending.CreditLines",         "View",                "Ver líneas de crédito, sus versiones, modalidades y equivalencias de Comercial"),
        ("Lending.CreditLines",         "Manage",              "Crear, versionar, cerrar e importar líneas de crédito"),
        ("Lending.Parameters",          "View",                "Ver los parámetros de Cartera y la política SARC efectiva"),
        ("Lending.Parameters",          "Manage",              "Registrar vigencias de los parámetros de Cartera"),
        ("Lending.Scoring",             "View",                "Ver el bloque de puntaje de una línea"),
        ("Lending.Scoring",             "Manage",              "Versionar el bloque de puntaje de una línea"),
        ("Lending.Attributions",        "View",                "Ver la matriz de atribuciones y los montos de las atribuciones operativas"),
        ("Lending.Attributions",        "Manage",              "Publicar la matriz de atribuciones y fijar montos de las atribuciones operativas"),
        ("Lending.Segregation",         "Manage",              "Registrar vigencias de las incompatibilidades de segregación"),
        ("Lending.GoverningBodies",     "View",                "Ver órganos de decisión, reglas de quórum y mayoría y miembros"),
        ("Lending.GoverningBodies",     "Manage",              "Administrar órganos de decisión, sus reglas y sus miembros"),
        ("Lending.RelatedParties",      "Read",                "Ver personas vinculadas y grupos conectados (lectura auditada)"),
        ("Lending.RelatedParties",      "Manage",              "Registrar y terminar personas vinculadas y grupos conectados"),
        ("Lending.RiskPolicy",          "Manage",              "Registrar una versión de la política SARC del consejo"),
        ("Lending.RiskPolicy",          "Validate",            "Validar una versión de la política SARC con su punto de acta"),
        ("Lending.Scopes",              "Manage",              "Asignar líneas de crédito a los usuarios"),
        ("Lending.Scope",               "AllOffices",          "Alcance sobre todas las oficinas sin filas de asignación"),
        ("Lending.Scope",               "AllLines",            "Alcance sobre todas las líneas sin filas de asignación"),
        ("Lending.Templates",           "View",                "Ver y descargar las plantillas vacías de Cartera"),
        ("Lending.MessageTemplates",    "View",                "Ver las plantillas de mensaje al deudor y sus versiones"),
        ("Lending.MessageTemplates",    "Manage",              "Versionar las plantillas de mensaje al deudor"),

        // --------------------------------------------------------------------------------------------------- originación --
        ("Lending.Simulations",         "Run",                 "Usar el simulador de plan de pagos"),
        ("Lending.Applications",        "Read",                "Ver solicitudes de crédito"),
        ("Lending.Applications",        "Create",              "Radicar solicitudes y editarlas en borrador"),
        ("Lending.Applications",        "Submit",              "Enviar solicitudes a análisis"),
        ("Lending.Applications",        "Analyze",             "Analizar solicitudes y volver a correr sus validaciones"),
        ("Lending.Applications",        "Cancel",              "Anular solicitudes o registrar el desistimiento"),
        ("Lending.PrepaymentPreferences", "Manage",            "Cambiar la preferencia del deudor para abonos a capital"),
        ("Lending.Decisions",           "Analyst",             "Decidir en el nivel de analista de la matriz de atribuciones"),
        ("Lending.Decisions",           "Coordinator",         "Decidir en el nivel de coordinación de la matriz de atribuciones"),
        ("Lending.Decisions",           "Management",          "Decidir en el nivel de gerencia de la matriz de atribuciones"),
        ("Lending.Decisions",           "Exceptions",          "Decidir en el nivel de excepción (puntaje bajo, alertas)"),
        ("Lending.CreditBureau",        "Query",               "Pedir una consulta a centrales de riesgo"),
        ("Lending.CreditBureau",        "ReadReports",         "Ver el resultado de una consulta a centrales (lectura auditada)"),
        ("Lending.Sessions",            "Read",                "Ver sesiones y actas (lectura auditada)"),
        ("Lending.Sessions",            "Manage",              "Convocar sesiones, registrar asistencia y puntos"),
        ("Lending.Sessions",            "RecordVotes",         "Registrar los votos de los miembros (secretaría)"),
        ("Lending.Sessions",            "Vote",                "Votar en una sesión"),
        ("Lending.Sessions",            "Close",               "Cerrar el acta de una sesión"),
        ("Lending.Guarantees",          "Read",                "Ver garantías"),
        ("Lending.Guarantees",          "Manage",              "Registrar garantías y cambiar su estado"),
        ("Lending.Guarantees",          "Release",             "Liberar garantías tras la firmeza del pago"),
        ("Lending.PromissoryNotes",     "Manage",              "Crear, consultar, endosar y anular pagarés"),
        ("Lending.CreditLimits",        "Read",                "Ver cupos, disponible y saldos de aportes cargados"),
        ("Lending.CreditLimits",        "Assign",              "Otorgar y terminar cupos y cargar saldos de aportes"),
        ("Lending.Blocks",              "Manage",              "Registrar y levantar bloqueos y embargos manuales"),
        ("Lending.NumberReservations",  "Read",                "Ver las reservas de número de las ventas de Comercial"),

        // ------------------------------------------------------------------------------------------------ crédito y pagos --
        ("Lending.Disbursements",       "Execute",             "Desembolsar y emitir un giro nuevo"),
        ("Lending.Disbursements",       "Void",                "Anular un crédito pendiente de giro"),
        ("Lending.DisbursementOrders",  "Void",                "Anular una orden de giro emitida"),
        ("Lending.DisbursementFiles",   "Generate",            "Ver, generar, descargar y anular archivos de desembolso"),
        ("Lending.Loans",               "Read",                "Ver créditos, estado de cuenta, plan, operaciones y transacciones"),
        ("Lending.Payoffs",             "Read",                "Liquidar un crédito a una fecha (lectura auditada)"),
        ("Lending.Payments",            "Simulate",            "Simular un pago"),
        ("Lending.Payments",            "Reverse",             "Reversar un pago que no sea de Nómina"),
        ("Lending.Payments",            "BackdateBeforeClose", "Registrar un pago con fecha valor anterior a un cierre"),
        ("Lending.ValidationFailures",  "Confirm",             "Confirmar por excepción una obligación con validación fallida"),
        ("Lending.Integrity",           "Verify",              "Verificar el libro de Cartera contra la reconstrucción"),

        // -------------------------------------------------------------------------------------------- integración y procesos --
        ("Lending.Messages",            "Read",                "Ver la bandeja de mensajes de Cartera, los recibos y los hechos con su lista por crédito"),
        ("Lending.Messages",            "Reprocess",           "Reprocesar mensajes rechazados o marcarlos no reprocesables"),
        ("Lending.Messages",            "SendNotApplicable",   "Enviar después los hechos que no pasaron a contabilidad"),
        ("Lending.Processes",           "View",                "Ver las corridas del proceso de Cartera"),
        ("Lending.Processes",           "Run",                 "Ordenar o reintentar corridas del ciclo diario"),
        ("Lending.Reconciliation",      "View",                "Ver la conciliación contable y el cuadre de una carga"),
        ("Lending.TestDoubles",         "Operate",             "Operar el recaudador y la tesorería de prueba (sólo fuera de producción)"),

        // ---------------------------------------------------------------------------------------------------------- riesgo --
        ("Lending.Ratings",             "Read",                "Ver calificaciones, cierres, cambios y modelo por segmento"),
        ("Lending.Ratings",             "Override",            "Registrar y levantar la calificación por análisis"),
        ("Lending.MonthlyClose",        "Execute",             "Ordenar el cierre mensual de Cartera"),
        ("Lending.MonthlyClose",        "Reopen",              "Reabrir el último cierre mensual"),
        ("Lending.ImpairmentModel",     "Manage",              "Adoptar o desistir de la pérdida esperada"),
        ("Lending.ArrearsHistory",      "Import",              "Cargar el historial complementario de mora"),
        ("Lending.PortfolioEvaluations", "Prepare",            "Preparar la evaluación de cartera"),
        ("Lending.PortfolioEvaluations", "Apply",              "Aplicar la evaluación de cartera con su acta"),

        // -------------------------------------------------------------------------------------------------------- libranza --
        ("Lending.PayrollAgreements",   "View",                "Ver pagadurías y convenios de libranza"),
        ("Lending.PayrollAgreements",   "Manage",              "Administrar pagadurías y convenios de libranza"),
        ("Lending.PayrollDeductionFiles", "Read",              "Ver archivos de descuento, sus líneas y los no aplicados"),
        ("Lending.PayrollDeductionFiles", "Generate",          "Generar y anular archivos de descuento"),
        ("Lending.PayrollNotApplied",   "Manage",              "Resolver un no aplicado de pagaduría"),
        ("Lending.PayrollTransfers",    "Manage",              "Trasladar una libranza a otra pagaduría o a la forma alterna"),

        // ------------------------------------------------------------------------------------------------------- novedades --
        ("Lending.Novelties",           "Read",                "Ver novedades de crédito"),
        ("Lending.Novelties",           "Request",             "Solicitar y simular novedades y registrar consentimientos"),
        ("Lending.Novelties",           "Apply",               "Aplicar novedades aprobadas"),
        ("Lending.Condonations",        "Request",             "Solicitar una condonación"),
        ("Lending.Condonations",        "Apply",               "Aplicar una condonación aprobada"),
        ("Lending.WriteOffs",           "Prepare",             "Armar el lote de castigo"),
        ("Lending.WriteOffs",           "Apply",               "Aplicar el lote de castigo con su acta"),
        ("Lending.DebtorEvents",        "Manage",              "Registrar fallecimiento, retiro y embargo del deudor y la reclamación al seguro"),
        ("Lending.Insolvency",          "Manage",              "Registrar y tramitar procesos de insolvencia"),

        // --------------------------------------------------------------------------------- mensajes al deudor y cobranza --
        ("Lending.DebtorMessages",      "Read",                "Ver la cola de mensajes al deudor y su historia"),
        ("Lending.DebtorMessages",      "Send",                "Enviar o programar mensajes al deudor"),
        ("Lending.DebtorMessages",      "Cancel",              "Cancelar un mensaje al deudor no enviado"),
        ("Lending.Campaigns",           "Manage",              "Administrar campañas de mensajes"),
        ("Lending.Collections",         "Read",                "Ver etapas, agenda, gestiones, promesas, acuerdos y jurídico"),
        ("Lending.Collections",         "Assign",              "Asignar y repartir la cartera entre gestores"),
        ("Lending.Collections",         "RecordActivity",      "Registrar gestiones de cobranza"),
        ("Lending.CollectionStages",    "Manage",              "Versionar las etapas de cobranza"),
        ("Lending.PaymentAgreements",   "Create",              "Proponer acuerdos de pago"),
        ("Lending.PaymentAgreements",   "Approve",             "Aprobar acuerdos de pago"),
        ("Lending.LegalProcesses",      "Manage",              "Administrar el cobro jurídico"),
        ("Lending.PriorNotices",        "Read",                "Ver avisos previos al reporte negativo"),
        ("Lending.PriorNotices",        "Send",                "Enviar avisos previos"),
        ("Lending.PriorNotices",        "RegisterEvidence",    "Registrar la constancia manual de un aviso previo"),
        ("Lending.CreditReportClaims",  "Read",                "Ver reclamos y suplantaciones sobre el reporte a centrales"),
        ("Lending.CreditReportClaims",  "Manage",              "Tramitar reclamos y suplantaciones sobre el reporte a centrales"),

        // ------------------------------------------------------------------------------------------- documentos e informes --
        ("Lending.Documents",           "Read",                "Ver documentos emitidos de Cartera"),
        ("Lending.Documents",           "Issue",               "Emitir y enviar documentos de Cartera"),
        ("Lending.Documents",           "Reprint",             "Descargar de nuevo el PDF guardado de un documento"),
        ("Lending.Documents",           "Void",                "Anular un documento emitido"),
        ("Lending.Documents",           "Verify",              "Verificar el código de un documento emitido"),
        ("Lending.Reports",             "View",                "Ver los informes del centro de Cartera"),
        ("Lending.Reports",             "Export",              "Exportar informes de Cartera"),
        ("Lending.Reports",             "ExportPersonalData",  "Exportar informes de Cartera con datos del deudor"),
        ("Lending.Dashboard",           "View",                "Ver el tablero de Cartera"),
        ("Lending.RegulatoryFiles",     "Read",                "Ver y descargar archivos del SICSES y de centrales"),
        ("Lending.RegulatoryFiles",     "MarkUploaded",        "Marcar cargado un archivo regulatorio con su radicado"),
        ("Lending.RegulatoryReports",   "Generate",            "Validar y generar el formato de cartera del SICSES"),
        ("Lending.CreditBureauFiles",   "Generate",            "Validar y generar archivos para centrales de riesgo"),

        // --------------------------------------------------------------------------------- migración, activación y alertas --
        ("Lending.Migration",           "Load",                "Crear cargas de migración, importar partes, aplicarlas y marcar ventas"),
        ("Lending.Migration",           "AcceptDifferences",   "Aceptar diferencias de la carga con acta"),
        ("Lending.Activation",          "Execute",             "Ver la preparación, revisar cobros provisionales y activar Cartera"),
        ("Lending.Alerts",              "View",                "Ver la bandeja de alertas de Cartera"),
        ("Lending.Alerts",              "Attend",              "Atender alertas de Cartera"),
        ("Lending.Alerts",              "Manage",              "Configurar los tipos de alerta de Cartera"),

        // ------------------------------------------------------------ captaciones y aportes (feature 014, T018; api.md §2) --
        ("Lending.DepositLines",        "View",                "Ver líneas de captación, sus versiones, tramos, cargos y reglas de tasa negociada"),
        ("Lending.DepositLines",        "Manage",              "Crear, versionar, cerrar, reabrir e importar líneas de captación"),
        ("Lending.ContributionLines",   "View",                "Ver la línea de aportes y sus versiones"),
        ("Lending.ContributionLines",   "Manage",              "Crear, versionar, cerrar, reabrir e importar la línea de aportes"),
        ("Lending.Accounts",            "View",                "Ver conteos, saldos agregados y vencimientos de cuentas, sin titular"),
        ("Lending.Accounts",            "Read",                "Ver cuentas, saldos, disponible, libro, operaciones, cuotas y condiciones (lectura auditada)"),
        ("Lending.Accounts",            "Open",                "Abrir cuentas y aprobar una apertura excepcional"),
        ("Lending.Accounts",            "Close",               "Cancelar cuentas"),
        ("Lending.Accounts",            "Deposit",             "Consignar en efectivo por el contrato hasta el monto atribuido"),
        ("Lending.Accounts",            "Withdraw",            "Retirar por orden de giro y por el contrato hasta el monto atribuido"),
        ("Lending.Accounts",            "Transfer",            "Trasladar entre cuentas desde la pantalla"),
        ("Lending.Accounts",            "Reverse",             "Reversar una operación de cuenta registrada por la Cartera"),
        ("Lending.Accounts.Parties",    "Manage",              "Administrar titulares, firmantes, representantes y beneficiarios de las cuentas"),
        ("Lending.TermDeposits",        "Constitute",          "Simular y constituir CDAT"),
        ("Lending.TermDeposits",        "Renew",               "Renovar un CDAT o cambiar su instrucción al vencimiento"),
        ("Lending.TermDeposits",        "Redeem",              "Redimir un CDAT hasta el monto atribuido"),
        ("Lending.TermDeposits",        "PayThirdParty",       "Pagar los intereses de un CDAT a la cuenta de otra persona, con la instrucción escrita del titular"),
        ("Lending.NegotiatedRates",     "Request",             "Pedir una tasa negociada"),
        ("Lending.NegotiatedRates",     "Approve",             "Decidir una tasa negociada según la matriz de atribuciones"),
        ("Lending.Contributions",       "Manage",              "Registrar devoluciones sin retiro y cesiones de aportes y aprobarlas"),
        ("Lending.Holds",               "Manage",              "Registrar y levantar bloqueos de cuentas"),
        ("Lending.Garnishments",        "Register",            "Registrar embargos, sus respuestas y las consignaciones al juzgado"),
        ("Lending.Garnishments",        "Release",             "Desembargar cuentas"),
        ("Lending.DirectDebits",        "Manage",              "Administrar autorizaciones de débito automático"),
        ("Lending.GmfExemptions",       "Manage",              "Marcar y desmarcar la cuenta exenta de GMF"),
        ("Lending.Charges",             "Manage",              "Registrar y reversar cargos de cuentas"),
        ("Lending.Charges",             "Waive",               "Condonar cargos y exonerar cuentas"),
        ("Lending.Interest",            "Settle",              "Liquidar a mano un período de intereses de captaciones"),
        ("Lending.MembershipWithdrawals", "File",              "Radicar y desistir el retiro del asociado"),
        ("Lending.MembershipWithdrawals", "Approve",           "Aprobar o negar el retiro del asociado con acta y aprobar la devolución"),
        ("Lending.MembershipWithdrawals", "Settle",            "Liquidar el retiro del asociado hasta el monto atribuido"),
        // Decisión del dueño del 2026-10-18 (D44): el nivel superior que reversa un pago del cruce con el retiro ya pagado y las cuentas canceladas.
        ("Lending.MembershipWithdrawals", "ReverseAfterPayment", "Reversar un pago del cruce al retiro con el retiro ya pagado: reabre la cuenta"),
        ("Lending.Assembly",            "Register",            "Registrar y simular revalorización, excedentes y amortización"),
        ("Lending.Assembly",            "Approve",             "Aprobar, aplicar y reversar una distribución de la asamblea"),
        ("Lending.DeceasedCases",       "Register",            "Registrar el fallecimiento de un titular y la entrega"),
        ("Lending.DeceasedCases",       "Approve",             "Aprobar la entrega de un titular fallecido"),
        ("Lending.Unclaimed",           "Manage",              "Trasladar saldos a no reclamados y pagarlos"),
        ("Lending.LiquidityFund",       "View",                "Ver la base, el requerido y el constituido del fondo de liquidez"),
        ("Lending.LiquidityFund",       "Manage",              "Registrar el constituido y las instantáneas del fondo de liquidez"),
        ("Lending.Deposits",            "Migration",           "Cargar la migración de captaciones y aportes y sus cifras de referencia"),
        ("Lending.Deposits",            "Activate",            "Ver la preparación y activar aportes o captaciones"),
    ];

    /// <summary>
    /// Los que se excluyen del glob <c>*</c> de <c>CompanyAdmin</c> y de la plantilla <c>cartera.administrador</c> (T61, C86). Feature 014 (decisión
    /// del dueño del 2026-10-18, research §13 D17): tampoco aprueba tasas negociadas —administrar no es aprobar tasa—.
    /// </summary>
    public static readonly string[] ExcluidosDelAdministrador = ["Lending.Decisions.*", "Lending.Sessions.Vote", "Lending.NegotiatedRates.Approve"];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith(Prefijo))
            .ToListAsync();

        var existingKeys = existing.Select(p => $"{p.Resource}.{p.Action}").ToHashSet(StringComparer.OrdinalIgnoreCase);

        var descripciones = Catalog.ToDictionary(p => $"{p.Resource}.{p.Action}", p => p.Description, StringComparer.OrdinalIgnoreCase);
        var actualizadas = 0;
        foreach (var p in existing)
        {
            if (descripciones.TryGetValue($"{p.Resource}.{p.Action}", out var descripcion) && p.Description != descripcion)
            {
                p.Description = descripcion;
                p.UpdatedBy = "Seed";
                actualizadas++;
            }
        }

        var toInsert = Catalog
            .Where(p => !existingKeys.Contains($"{p.Resource}.{p.Action}"))
            .Select(p => new Permission
            {
                Resource = p.Resource,
                Action = p.Action,
                Description = p.Description,
                CreatedBy = "Seed",
                UpdatedBy = "Seed",
            })
            .ToList();

        if (toInsert.Count == 0 && actualizadas == 0)
        {
            logger.LogDebug("Permisos de Cartera ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de Cartera: {Inserted} nuevos de {Total}, {Updated} descripciones puestas al día.",
            toInsert.Count, Catalog.Length, actualizadas);
    }
}
