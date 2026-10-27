using System.Text.RegularExpressions;

namespace IngenIA365ERP.Application.Security.Roles.Plantillas;

/// <summary>
/// Una plantilla de rol: no es un rol integrado, es una propuesta. Crear un rol desde ella
/// (<see cref="CreateRoleFromTemplateCommand"/>) produce un rol normal y editable con la intersección entre
/// <see cref="Patrones"/> y el catálogo sembrado de la cooperativa.
/// </summary>
/// <param name="Key">Clave estable (<c>inventario.bodeguero</c>).</param>
/// <param name="Module">Módulo que la ofrece (<c>Inventory</c>): filtro de <c>GET /api/admin/roles/templates?module=</c>.</param>
/// <param name="Actor">A quién le sirve, en español.</param>
/// <param name="Description">Qué hace esa persona con el rol.</param>
/// <param name="Patrones">
/// Códigos exactos (<c>Inventory.Costs.Read</c>) o patrones con <c>*</c> (<c>Inventory.*.View</c>,
/// <c>Inventory.Catalog.*</c>) que se expanden contra el catálogo.
/// </param>
/// <param name="Notes">Alcance y montos: lo que la plantilla no puede dar y hay que asignar aparte.</param>
/// <param name="Excluidos">
/// Feature 013 (C86): códigos o patrones que se quitan de lo que los <paramref name="Patrones"/> alcanzan
/// (<c>cartera.administrador</c> = <c>Lending.*</c> salvo <c>Lending.Decisions.*</c> y <c>Lending.Sessions.Vote</c>).
/// </param>
public sealed record PerfilSugerido(
    string Key,
    string Module,
    string Actor,
    string Description,
    IReadOnlyList<string> Patrones,
    string Notes,
    IReadOnlyList<string>? Excluidos = null);

/// <summary>
/// Los perfiles sugeridos del inventario comercial (feature 012, T127; FR-093, T48; contracts/api.md §1.4). Viven en
/// código, no en la base: son una propuesta de reparto, y el rol que se crea desde uno es de la cooperativa y se edita
/// como cualquier otro. Lo que en la propuesta de seguridad era <c>Inventory.Taxes</c>, <c>Inventory.PaymentMethods</c>
/// e <c>Inventory.Dian</c> hoy es <c>Core.Taxes</c>, <c>Core.PaymentMeans</c> y <c>ElectronicInvoicing</c>.
///
/// <para>
/// Las acciones sensibles se listan una a una; sólo las lecturas van por el patrón <c>Inventory.*.View</c>, que no
/// alcanza a <c>ViewAll</c>, <c>Read</c> ni <c>Export</c>. Un código que el catálogo de la cooperativa no tiene no se
/// inventa: vuelve en <c>omitted</c>.
/// </para>
/// </summary>
public static class PerfilesSugeridos
{
    public const string ModuloInventario = "Inventory";

    /// <summary>
    /// El módulo de las plantillas de Cartera (feature 013, T0221; DT §2.14; contracts/api.md §2.5): las trece del crédito y, desde la feature 014
    /// (T018; api.md §2; research E07), las cuatro de captaciones y aportes. Desde la decisión del dueño del 2026-10-18 (research §13 D18) los perfiles
    /// van **separados por módulo**: la activación de créditos exige sólo los trece del crédito (<see cref="DeCreditos"/>, <c>LendingRolesCreated</c>) y
    /// la de aportes o captaciones sólo sus cuatro (<see cref="DeCaptaciones"/>, <c>DepositRolesCreated</c>).
    /// </summary>
    public const string ModuloCartera = "Lending";

    /// <summary>Las claves de las cuatro plantillas de captaciones y aportes (feature 014, T018; decisión del dueño del 2026-10-18, D18).</summary>
    public static IReadOnlyList<string> ClavesDeCaptaciones { get; } =
        ["cartera.jefe-captaciones", "cartera.asesor-captaciones", "cartera.analista-aportes", "cartera.tesorero-liquidez"];

    public static IReadOnlyList<PerfilSugerido> Todos { get; } =
    [
        new("inventario.administrador", ModuloInventario, "Administrador del módulo",
            "Parametriza y opera todo el inventario comercial, los impuestos, los medios de pago y la facturación electrónica.",
            ["Inventory.*", "Core.Taxes.*", "Core.PaymentMeans.*", "ElectronicInvoicing.*"],
            "Alcance total (trae Inventory.Scope.AllWarehouses y AllPointsOfSale); sin límite de monto."),

        new("inventario.jefe", ModuloInventario, "Jefe de inventario",
            "Administra catálogo y bodegas, costeo y períodos; registra, confirma y aprueba ajustes, traslados, conteos y el saldo inicial.",
            [
                "Inventory.Catalog.*", "Inventory.Warehouses.*", "Inventory.Stock.View", "Inventory.Costs.Read",
                "Inventory.Costing.Manage", "Inventory.Periods.*", "Inventory.Adjustments.*", "Inventory.Transfers.*",
                "Inventory.Counts.*", "Inventory.OpeningBalance.*", "Inventory.Integrity.Verify", "Inventory.Reports.View",
                "Inventory.Reports.Export", "Inventory.Alerts.*", "Inventory.Approvals.View", "Inventory.Approvals.Supervisor",
                "Inventory.Documents.View", "Inventory.DocumentTypes.View", "Inventory.Parameters.View",
                "Inventory.Scope.AllWarehouses",
            ],
            "Todas las bodegas; límite de monto en Inventory.Adjustments.Confirm si la cooperativa lo fija."),

        new("inventario.bodeguero", ModuloInventario, "Bodeguero",
            "Recibe compras, despacha y recibe traslados, cuenta y registra ajustes; no confirma ni aprueba.",
            [
                "Inventory.Stock.View", "Inventory.Catalog.View", "Inventory.Warehouses.View", "Inventory.DocumentTypes.View",
                "Inventory.Documents.View", "Inventory.Transfers.View", "Inventory.Transfers.Create",
                "Inventory.Transfers.Dispatch", "Inventory.Transfers.Receive", "Inventory.Counts.View",
                "Inventory.Counts.Capture", "Inventory.Adjustments.View", "Inventory.Adjustments.Create",
                "Inventory.Purchases.View", "Inventory.Purchases.Create", "Inventory.Alerts.View",
            ],
            "Sus bodegas: hay que asignárselas en Alcance comercial; sin filas de alcance no ve ninguna."),

        new("inventario.comprador", ModuloInventario, "Comprador",
            "Registra y confirma compras, facturas y notas del proveedor y sus eventos RADIAN; da de alta proveedores.",
            [
                "Inventory.Purchases.View", "Inventory.Purchases.Create", "Inventory.Purchases.Confirm",
                "Inventory.Purchases.Void", "Inventory.Purchases.RegisterRadianEvent", "Inventory.Purchases.EmitRadianEvent",
                "Inventory.Catalog.View", "Inventory.Warehouses.View", "Inventory.DocumentTypes.View",
                "Inventory.Documents.View", "Inventory.Stock.View", "Inventory.Prices.View", "Inventory.Costs.Read",
                "Inventory.Reports.View", "Inventory.Alerts.View", "Core.People.Create",
            ],
            "Sus bodegas (filas de alcance); monto máximo en Inventory.Purchases.Confirm."),

        new("inventario.cajero", ModuloInventario, "Vendedor o cajero",
            "Vende en el punto de venta, abre y cierra su caja, registra movimientos y reimprime; pide aprobaciones presenciales.",
            [
                "Inventory.Sales.View", "Inventory.Sales.Create", "Inventory.Sales.Confirm", "Inventory.Sales.SellOnCredit",
                "Inventory.Pos.Sell", "Inventory.CashSessions.View", "Inventory.CashSessions.Open",
                "Inventory.CashSessions.Close", "Inventory.CashMovements.Create", "Inventory.PointsOfSale.View",
                "Inventory.Catalog.View", "Inventory.Prices.View", "Inventory.Stock.View", "Inventory.Documents.View",
                "Inventory.Documents.Reprint", "Inventory.Approvals.View", "Inventory.Alerts.View", "Core.People.Create",
            ],
            "Sus puntos de venta (y la bodega de su caja), por filas de alcance; monto máximo en Inventory.Sales.SellOnCredit."),

        new("inventario.aprobador", ModuloInventario, "Aprobador",
            "Aprueba lo que otros registran y confirman; ve todo el módulo pero no crea ni confirma.",
            [
                "Inventory.Adjustments.Approve", "Inventory.Transfers.Approve", "Inventory.Counts.Approve",
                "Inventory.Purchases.Approve", "Inventory.Sales.Approve", "Inventory.CashMovements.Approve",
                "Inventory.CashDifferences.Approve", "Inventory.OpeningBalance.Approve", "Inventory.Discounts.Authorize",
                "Inventory.Approvals.View", "Inventory.Approvals.Supervisor", "Inventory.*.View",
            ],
            "Las bodegas y puntos de venta que aprueba: el aprobador también necesita alcance sobre ellos."),

        new("inventario.contador", ModuloInventario, "Contador (sólo consulta)",
            "Consulta existencias, costos, informes y la conciliación con contabilidad; no escribe en el inventario.",
            [
                "Inventory.*.View", "Inventory.Costs.Read", "Inventory.Reports.View", "Inventory.Reports.Export",
                "Inventory.Reconciliation.View", "Inventory.Scope.AllWarehouses",
            ],
            "Todas las bodegas. La matriz y los lotes contables (Accounting.InventoryRules.*, Accounting.InventoryBatches.*) van en su rol contable, no aquí."),

        new("inventario.auditor", ModuloInventario, "Auditor (sólo lectura)",
            "Lee todo el módulo con costos, verifica la integridad del kardex y de la auditoría; no escribe.",
            [
                "Inventory.*.View", "Inventory.Costs.Read", "Inventory.Reports.View", "Inventory.Integrity.Verify",
                "Inventory.CashSessions.ViewAll", "Inventory.Scope.AllWarehouses", "Inventory.Scope.AllPointsOfSale",
                "AuditLog.View", "AuditLog.Export", "AuditLog.VerifyIntegrity",
            ],
            "Todo, sin escritura: verificar la integridad levanta alertas pero no cambia datos."),

        // ----------------------------------------------------------------- Cartera (feature 013, T0221; api.md §2.5; C85, C86) --
        new("cartera.administrador", ModuloCartera, "Administrador de Cartera",
            "Parametriza y opera toda la Cartera, pero no decide créditos ni vota en sesiones: administrar no es aprobar crédito.",
            // K3, bloque D (decisión del dueño del 2026-10-15): Attachments.Upload, porque sube los soportes de novedades, eventos del deudor y castigo, y
            // Attachments.Download, porque elegir un soporte ya subido lista los adjuntos del dueño (la lectura sigue sujeta a AdjuntosDeModulo).
            ["Lending.*", "Attachments.Upload", "Attachments.Download"],
            "Todas las oficinas y líneas (trae Lending.Scope.AllOffices y AllLines); sin límite de monto.",
            // Feature 014 (T018; api.md §2): administrar no es aprobar una tasa negociada. Feature 015 (T0307): Lending.TestDoubles.Operate se dio de baja
            // con los dobles de prueba y ya no hace falta excluirlo.
            ["Lending.Decisions.*", "Lending.Sessions.Vote", "Lending.NegotiatedRates.Approve"]),

        new("cartera.asesor", ModuloCartera, "Asesor de crédito",
            "Consulta las líneas, simula, radica y envía solicitudes, consulta centrales, registra garantías y pagarés, emite documentos y escribe al deudor.",
            [
                // Lending.CreditLines.View: respuesta del dueño del 2026-10-14 (sólo consultar; cada cooperativa lo quita o lo agrega en Seguridad › Roles).
                "Lending.CreditLines.View", "Lending.Simulations.Run", "Lending.Applications.Read", "Lending.Applications.Create", "Lending.Applications.Submit",
                "Lending.PrepaymentPreferences.Manage", "Lending.CreditBureau.Query", "Lending.Guarantees.Read", "Lending.Guarantees.Manage",
                "Lending.PromissoryNotes.Manage", "Lending.CreditLimits.Read", "Lending.Loans.Read", "Lending.Payoffs.Read",
                "Lending.Documents.Read", "Lending.Documents.Issue", "Lending.DebtorMessages.Read", "Lending.DebtorMessages.Send",
                "Core.People.Create", "Compliance.HabeasData.RecordConsent",
            ],
            "Sus oficinas y líneas: hay que asignárselas (alcance de Cartera); sin filas de alcance no ve ninguna."),

        new("cartera.analista", ModuloCartera, "Analista de crédito",
            "Analiza solicitudes, lee centrales y decide en el nivel de analista de la matriz.",
            [
                "Lending.Applications.Read", "Lending.Applications.Analyze", "Lending.Decisions.Analyst", "Lending.CreditBureau.Query",
                "Lending.CreditBureau.ReadReports", "Lending.Guarantees.Read", "Lending.CreditLimits.Read", "Lending.Loans.Read",
                "Lending.Payoffs.Read", "Lending.Ratings.Read",
                // K3, bloque D (decisión del dueño del 2026-10-15): decide novedades en su nivel, y la ruta de decisiones de novedades exige verlas.
                "Lending.Novelties.Read",
            ],
            "Sus oficinas y líneas."),

        new("cartera.jefe", ModuloCartera, "Jefe de cartera",
            "Decide en el nivel de coordinación, asigna cupos, desembolsa, reversa pagos, ordena el cierre y opera procesos, bandeja y migración.",
            [
                "Lending.Decisions.Coordinator", "Lending.Applications.Read", "Lending.Applications.Cancel", "Lending.PrepaymentPreferences.Manage",
                "Lending.CreditLimits.Read", "Lending.CreditLimits.Assign", "Lending.Blocks.Manage", "Lending.Disbursements.Execute",
                "Lending.Disbursements.Void", "Lending.DisbursementOrders.Void", "Lending.DisbursementFiles.Generate", "Lending.Loans.Read",
                "Lending.Payoffs.Read", "Lending.Payments.Simulate", "Lending.Payments.Reverse", "Lending.MonthlyClose.Execute",
                "Lending.Ratings.Read", "Lending.Novelties.Read", "Lending.Novelties.Request", "Lending.WriteOffs.Prepare",
                "Lending.Sessions.Read", "Lending.Sessions.Manage", "Lending.Messages.*", "Lending.Processes.*", "Lending.Reconciliation.View",
                "Lending.Migration.Load", "Lending.PayrollNotApplied.Manage", "Lending.Collections.Read", "Lending.Collections.Assign",
                "Lending.CollectionStages.Manage", "Lending.Alerts.*", "Lending.Reports.View", "Lending.Reports.Export",
                "Lending.Parameters.View", "Lending.Templates.View", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
                // K3, bloque D (decisión del dueño del 2026-10-15): sube los soportes de novedades (consentimientos) y del lote de castigo.
                "Attachments.Upload", "Attachments.Download",
                // K4, bloque D (decisión del dueño del 2026-10-16): aprueba acuerdos de pago (no los propone: la segregación la verifica el comando),
                // lleva el cobro jurídico y las campañas, y envía y registra los avisos previos.
                "Lending.PaymentAgreements.Approve", "Lending.LegalProcesses.Manage", "Lending.Campaigns.Manage", "Lending.PriorNotices.Send",
                "Lending.PriorNotices.RegisterEvidence",
            ],
            "Todas las oficinas y líneas; límite de monto en Lending.Disbursements.Execute y Lending.CreditLimits.Assign si la cooperativa lo fija."),

        new("cartera.comite-credito", ModuloCartera, "Secretaría del comité de crédito",
            "Convoca las sesiones del comité, registra asistencia, puntos y votos y cierra el acta.",
            [
                "Lending.Sessions.Read", "Lending.Sessions.Manage", "Lending.Sessions.RecordVotes", "Lending.Sessions.Close",
                "Lending.Applications.Read", "Lending.RelatedParties.Read", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
            ],
            "Todas las oficinas y líneas."),

        new("cartera.comite-riesgos", ModuloCartera, "Comité de riesgos",
            "Prepara la evaluación de cartera, lee calificaciones y lleva las sesiones del comité.",
            [
                "Lending.PortfolioEvaluations.Prepare", "Lending.Ratings.Read", "Lending.Sessions.Read", "Lending.Sessions.Manage",
                "Lending.Sessions.RecordVotes", "Lending.Sessions.Close", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
            ],
            "Todas las oficinas y líneas."),

        new("cartera.gerente", ModuloCartera, "Gerente",
            "Decide en los niveles de gerencia y de excepción, confirma validaciones fallidas y autoriza pagos con fecha valor antes de un cierre.",
            [
                "Lending.Decisions.Management", "Lending.Decisions.Exceptions", "Lending.Condonations.Request", "Lending.ValidationFailures.Confirm",
                "Lending.Payments.BackdateBeforeClose", "Lending.Applications.Read", "Lending.Loans.Read", "Lending.Payoffs.Read",
                "Lending.Ratings.Read", "Lending.Reports.View", "Lending.Dashboard.View", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
                // K3, bloque D (decisión del dueño del 2026-10-15): decide novedades (y la excepción de cobertura), y esas rutas exigen verlas.
                "Lending.Novelties.Read",
                // Feature 014, US15 (T216): la gerencia ve el fondo de liquidez y el riesgo de liquidez y recibe sus alertas (US15-1).
                "Lending.LiquidityFund.View",
            ],
            "Todas las oficinas y líneas; límites operativos si la cooperativa los fija."),

        new("cartera.consejo", ModuloCartera, "Secretaría del consejo",
            "Lleva las sesiones del consejo y aplica lo que decide: castigos, evaluación de cartera, modelo de deterioro, política SARC y diferencias de la migración.",
            [
                "Lending.Sessions.Read", "Lending.Sessions.Manage", "Lending.Sessions.RecordVotes", "Lending.Sessions.Close",
                "Lending.RelatedParties.Read", "Lending.WriteOffs.Apply", "Lending.PortfolioEvaluations.Apply", "Lending.ImpairmentModel.Manage",
                "Lending.RiskPolicy.Validate", "Lending.Migration.AcceptDifferences", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
                // K3, bloque D (decisión del dueño del 2026-10-15): las pantallas de castigo y evaluación que aplica leen créditos y calificaciones, y
                // el reporte de castigos al supervisor se sube como soporte del lote.
                "Lending.Loans.Read", "Lending.Ratings.Read", "Attachments.Upload", "Attachments.Download",
            ],
            "Todas las oficinas y líneas."),

        new("cartera.gestor-cobranza", ModuloCartera, "Gestor de cobranza",
            "Gestiona la cobranza: registra gestiones, propone acuerdos, escribe al deudor y envía los avisos previos con su constancia.",
            [
                "Lending.Collections.Read", "Lending.Collections.RecordActivity", "Lending.PaymentAgreements.Create", "Lending.Loans.Read",
                "Lending.Payoffs.Read", "Lending.DebtorMessages.Read", "Lending.DebtorMessages.Send", "Lending.PriorNotices.Read",
                // K4, bloque D (decisión del dueño del 2026-10-16): envía el aviso previo y registra la carta; la guía escaneada se sube como adjunto
                // (LendingPriorNoticeEvidence, que además exige RegisterEvidence), por eso sube y elige adjuntos.
                "Lending.PriorNotices.Send", "Lending.PriorNotices.RegisterEvidence", "Attachments.Upload", "Attachments.Download",
            ],
            "Sus oficinas: hay que asignárselas."),

        new("cartera.oficial-cumplimiento", ModuloCartera, "Oficial de cumplimiento",
            "Carga listas restrictivas y libera coincidencias, atiende al titular de los datos y tramita reclamos sobre el reporte.",
            [
                "Compliance.RestrictiveLists.View", "Compliance.RestrictiveLists.Load", "Compliance.RestrictiveLists.Release",
                "Compliance.DataSubjectRequests.*", "Compliance.HabeasData.ViewHistory", "Lending.RelatedParties.Read",
                "Lending.CreditReportClaims.*", "AuditLog.View", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
            ],
            "Todas las oficinas y líneas."),

        new("cartera.contador", ModuloCartera, "Contador (consulta y regulatorios)",
            "Consulta toda la Cartera, concilia con contabilidad y genera y marca los archivos regulatorios.",
            [
                "Lending.*.Read", "Lending.*.View", "Lending.Reports.*", "Lending.Reconciliation.View", "Lending.RegulatoryReports.Generate",
                "Lending.CreditBureauFiles.Generate", "Lending.RegulatoryFiles.MarkUploaded", "Core.NationalValues.View",
                "Core.NationalValues.Review", "Core.RegulatoryProfile.View", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
                // US7 (T0638; contabilidad.md §3.8, §15): la contadora diligencia la matriz, ordena los lotes y registra los indicadores.
                "Accounting.LendingRules.View", "Accounting.LendingRules.Manage", "Accounting.LendingBatches.View", "Accounting.LendingBatches.Run",
                "Accounting.RegulatoryIndicators.View", "Accounting.RegulatoryIndicators.Manage",
            ],
            "Todas las oficinas y líneas; con la matriz, los lotes y los indicadores de la entidad del lado contable."),

        new("cartera.auditor", ModuloCartera, "Auditor (sólo lectura)",
            "Lee toda la Cartera, exporta informes y verifica la integridad del libro y de la auditoría; no escribe.",
            [
                "Lending.*.Read", "Lending.*.View", "Lending.Reports.View", "Lending.Reports.Export", "Lending.Integrity.Verify",
                "AuditLog.View", "AuditLog.Export", "AuditLog.VerifyIntegrity", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
                // US7 (T0638): lee la matriz, los lotes y los indicadores; no los cambia.
                "Accounting.LendingRules.View", "Accounting.LendingBatches.View", "Accounting.RegulatoryIndicators.View",
            ],
            "Todas las oficinas y líneas, sin escritura."),

        new("cartera.revisor-fiscal", ModuloCartera, "Revisor fiscal (sólo lectura)",
            "Igual que el auditor: lee toda la Cartera (las actas entran por Lending.Sessions.Read), exporta y verifica; no escribe.",
            [
                "Lending.*.Read", "Lending.*.View", "Lending.Reports.View", "Lending.Reports.Export", "Lending.Integrity.Verify",
                "AuditLog.View", "AuditLog.Export", "AuditLog.VerifyIntegrity", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
            ],
            "Todas las oficinas y líneas, sin escritura."),

        // ------------------------------------------------------- captaciones y aportes (feature 014, T018; api.md §2) --
        new("cartera.jefe-captaciones", ModuloCartera, "Jefe de captaciones",
            "Administra las líneas de captación y de aportes, abre y cancela cuentas, decide tasas negociadas, redime CDAT, lleva embargos, cargos, "
            + "retiros del asociado, fallecimientos y no reclamados, y carga la migración de captaciones.",
            [
                "Lending.DepositLines.*", "Lending.ContributionLines.*", "Lending.Accounts.*", "Lending.TermDeposits.Constitute", "Lending.TermDeposits.Renew", "Lending.TermDeposits.Redeem", "Lending.NegotiatedRates.*",
                "Lending.Contributions.Manage", "Lending.Holds.Manage", "Lending.Garnishments.*", "Lending.DirectDebits.Manage",
                "Lending.GmfExemptions.Manage", "Lending.Charges.*", "Lending.Interest.Settle", "Lending.MembershipWithdrawals.*",
                "Lending.DeceasedCases.*", "Lending.Unclaimed.Manage", "Lending.Deposits.Migration", "Lending.Documents.Read", "Lending.Documents.Issue",
                "Lending.Reports.View", "Lending.Reports.Export", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
                "Attachments.Upload", "Attachments.Download",
            ],
            "Todas las oficinas y líneas; límite de monto en Lending.Accounts.Withdraw, TermDeposits.Redeem y MembershipWithdrawals.Settle si la "
            + "cooperativa lo fija. La segregación impide decidir lo que la misma persona registró."),

        new("cartera.asesor-captaciones", ModuloCartera, "Asesor de captaciones",
            "Abre cuentas y administra sus titulares y beneficiarios, constituye CDAT, pide tasas negociadas, marca la exención de GMF, autoriza "
            + "débitos automáticos, radica el retiro del asociado y emite documentos al titular.",
            [
                "Lending.DepositLines.View", "Lending.ContributionLines.View", "Lending.Accounts.View", "Lending.Accounts.Read", "Lending.Accounts.Open",
                "Lending.Accounts.Parties.Manage", "Lending.TermDeposits.Constitute", "Lending.NegotiatedRates.Request", "Lending.GmfExemptions.Manage",
                "Lending.DirectDebits.Manage", "Lending.MembershipWithdrawals.File", "Lending.Documents.Read", "Lending.Documents.Issue",
                "Core.People.Create", "Compliance.HabeasData.RecordConsent", "Attachments.Upload", "Attachments.Download",
            ],
            "Sus oficinas y líneas: hay que asignárselas (alcance de Cartera)."),

        new("cartera.analista-aportes", ModuloCartera, "Analista de aportes",
            "Lleva los aportes: devoluciones sin retiro y cesiones, la liquidación del retiro del asociado y la simulación de la asamblea.",
            [
                "Lending.ContributionLines.View", "Lending.Accounts.View", "Lending.Accounts.Read", "Lending.Contributions.Manage",
                "Lending.MembershipWithdrawals.File", "Lending.MembershipWithdrawals.Settle", "Lending.Assembly.Register", "Lending.Documents.Read",
                "Lending.Documents.Issue", "Lending.Reports.View", "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
            ],
            "Todas las oficinas y líneas; límite de monto en Lending.MembershipWithdrawals.Settle si la cooperativa lo fija."),

        new("cartera.tesorero-liquidez", ModuloCartera, "Tesorero (fondo de liquidez)",
            "Registra el constituido del fondo de liquidez y sus instantáneas, y consulta saldos agregados y la conciliación.",
            [
                "Lending.LiquidityFund.*", "Lending.Accounts.View", "Lending.Reconciliation.View", "Lending.Reports.View", "Lending.Reports.Export",
                "Lending.Scope.AllOffices", "Lending.Scope.AllLines",
            ],
            "Todas las oficinas y líneas; sólo cifras, sin datos de titulares."),

        // ------------------------------------------------- Recaudo y Tesorería (feature 015, T0045; FR-176; R-06; contracts/api.md §1.5) --
        // Viven aquí y no en el sembrador de permisos (desviación de tasks.md T0045): las plantillas de rol del ERP son propuestas en código. Llevan
        // los permisos de Cartera y Captaciones que los contratos verifican sobre quien actúa. Activar Recaudo exige al menos un cajero y un
        // supervisor con estos permisos.
        new("recaudo.cajero", ModuloRecaudo, "Cajero",
            "Abre su sesión, registra recibos multiproducto por los contratos de Cartera y Captaciones, mueve su caja y la arquea.",
            [
                "Receipts.Session.Open", "Receipts.Session.View", "Receipts.Session.Move", "Receipts.Session.Close", "Receipts.Receipt.Register",
                "Receipts.Receipt.View", "Receipts.Receipt.Print", "Receipts.Voiding.Request", "Receipts.Checks.View",
                "Receipts.Disbursements.View", "Receipts.Disbursements.Register",
                "Lending.Accounts.Deposit", "Core.People.Create", "Attachments.Upload", "Attachments.Download",
            ],
            "Sus cajas y puntos: hay que asignárselos (alcance de Recaudo). El arqueo es ciego: no trae Receipts.Session.ViewExpected."),

        new("recaudo.supervisor", ModuloRecaudo, "Supervisor de caja",
            "Aprueba diferencias de arqueo y anulaciones, cierra sesiones olvidadas y el día del punto, lleva la bóveda y las remesas.",
            [
                "Receipts.Setup.View", "Receipts.Session.*", "Receipts.Count.ApproveDifference", "Receipts.Day.Close", "Receipts.Vault.*",
                "Receipts.Receipt.View", "Receipts.Receipt.Print", "Receipts.Receipt.RegisterContingency", "Receipts.Receipt.Backdate",
                "Receipts.Voiding.*", "Receipts.Reversals.*", "Receipts.CreditBalances.*", "Receipts.Checks.*", "Receipts.Reports.View",
                "Receipts.PendingApplications.*", "Receipts.Disbursements.*",
                "Lending.Payments.BackdateBeforeClose", "Lending.Accounts.Deposit", "Lending.Accounts.Withdraw",
                "Attachments.Upload", "Attachments.Download",
            ],
            "Sus oficinas y puntos; límite de monto en Receipts.Count.ApproveDifference, Voiding.Approve, Receipt.Backdate y en los permisos de "
            + "Cartera con monto si la cooperativa lo fija. No aprueba lo que él mismo registró."),

        new("recaudo.analista", ModuloRecaudo, "Analista de recaudo",
            "Identifica abonos bancarios, resuelve partidas y saldos a favor, carga respuestas y aplica giros de pagadurías.",
            [
                "Receipts.BankItems.*", "Receipts.UnidentifiedItems.View", "Receipts.UnidentifiedItems.Resolve", "Receipts.CreditBalances.*",
                "Receipts.Paymasters.*", "Receipts.Receipt.View", "Receipts.Reports.View", "Receipts.Reports.Export", "Treasury.Statements.Load",
                "Treasury.Statements.View", "Attachments.Upload", "Attachments.Download",
            ],
            "Todas las cuentas de recaudo y pagadurías que se le asignen; pasar una partida a ingreso es del supervisor con aprobación."),

        new("tesoreria.tesorero", ModuloTesoreria, "Tesorero",
            "Administra cuentas propias y chequeras, prepara lotes, genera y envía archivos de pago, registra resultados y confirma remesas.",
            [
                "Treasury.BankAccounts.*", "Treasury.BankBalances.View", "Treasury.Orders.*", "Treasury.Batches.Prepare", "Treasury.PaymentFiles.*",
                "Treasury.Results.Register", "Treasury.Checkbooks.*", "Treasury.Checks.*", "Treasury.Remittances.*", "Treasury.LiquidityFund.Request",
                "Treasury.Reports.View", "Treasury.Reports.Export", "Core.PersonBankAccounts.View", "Core.PersonBankAccounts.Request",
                // T1 (fases 33 y 34): pide traslados, prepara órdenes manuales y lleva los firmantes y tokens (no los aprueba).
                "Treasury.Transfers.Request", "Treasury.ManualOrders.Create", "Treasury.Signers.Manage",
                // T2 (fases 42 y 43): las cuentas por pagar y la liquidez.
                "Treasury.Payables.*", "Treasury.Liquidity.*",
                "Attachments.Upload", "Attachments.Download",
            ],
            "Sus cuentas bancarias; no aprueba lotes ni el uso del fondo de liquidez (segregación) ni las cuentas destino que él pidió."),

        new("tesoreria.aprobador", ModuloTesoreria, "Aprobador de pagos",
            "Aprueba lotes de pago y el uso del fondo de liquidez, y aprueba las cuentas bancarias de las personas.",
            [
                "Treasury.Orders.View", "Treasury.Batches.Approve", "Treasury.LiquidityFund.Approve", "Treasury.BankAccounts.View",
                "Treasury.Reports.View", "Core.PersonBankAccounts.View", "Core.PersonBankAccounts.Approve",
                // T1 (fases 33 y 34): aprueba traslados, órdenes manuales y cambios de firmantes; administra el catálogo de órdenes manuales.
                "Treasury.Transfers.Approve", "Treasury.ManualOrders.Approve", "Treasury.ManualOrders.ManageConcepts", "Treasury.Signers.Approve",
                // T2 (fase 41): aprueba las notas bancarias que registra otra persona.
                "Treasury.BankNotes.Approve", "Treasury.Liquidity.View",
                // T2 (fase 44): decide la diferencia de las liquidaciones de las redes de tarjetas.
                "Treasury.AcquirerSettlements.Approve",
                "Attachments.Download",
            ],
            "Límite de monto en Treasury.Batches.Approve y LiquidityFund.Approve si la cooperativa lo fija. No aprueba lo que él armó."),

        new("tesoreria.conciliador", ModuloTesoreria, "Conciliador bancario",
            "Carga extractos, concilia las cuentas propias y registra partidas conciliatorias; no gira.",
            [
                "Treasury.Statements.Load", "Treasury.Statements.View", "Treasury.Reconciliation.*", "Treasury.BankAccounts.View",
                "Treasury.BankBalances.View", "Treasury.Reports.View", "Treasury.Reports.Export", "Attachments.Upload", "Attachments.Download",
                // T2 (fase 41): registra las notas bancarias (las aprueba otra persona).
                "Treasury.BankNotes.Register",
                // T2 (fase 44): carga y concilia las liquidaciones de las redes de tarjetas (la diferencia sobre la tolerancia la decide otra persona).
                "Treasury.AcquirerSettlements.Load",
            ],
            "Sus cuentas bancarias. La segregación impide conciliar la cuenta de la que él mismo gira."),

        new("cumplimiento.oficial", ModuloRecaudo, "Oficial de cumplimiento",
            "Revisa las señales de efectivo y las coincidencias en listas, libera lo retenido y genera el reporte de transacciones en efectivo.",
            [
                "Receipts.Compliance.*", "Receipts.Sarlaft.Report", "Receipts.Receipt.View", "Receipts.Reports.View", "Receipts.Reports.Export",
                "Treasury.Orders.View", "Compliance.RestrictiveLists.Release", "Receipts.Scope.All", "Attachments.Download",
            ],
            "Todas las oficinas (trae Receipts.Scope.All); la lectura de los datos del pagador queda auditada."),
    ];

    /// <summary>El módulo de las plantillas de Recaudo y la de cumplimiento (feature 015, T0045).</summary>
    public const string ModuloRecaudo = "Receipts";

    /// <summary>El módulo de las plantillas de la Tesorería nueva (feature 015, T0045).</summary>
    public const string ModuloTesoreria = "Treasury";

    /// <summary>Las claves de las siete plantillas de la 015 (FR-176).</summary>
    public static IReadOnlyList<string> ClavesDeRecaudoYTesoreria { get; } =
    [
        "recaudo.cajero", "recaudo.supervisor", "recaudo.analista", "tesoreria.tesorero", "tesoreria.aprobador", "tesoreria.conciliador", "cumplimiento.oficial",
    ];

    /// <summary>La plantilla por clave (sin distinguir mayúsculas), o <c>null</c>.</summary>
    public static PerfilSugerido? Buscar(string? clave) =>
        string.IsNullOrWhiteSpace(clave)
            ? null
            : Todos.FirstOrDefault(p => string.Equals(p.Key, clave.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Las plantillas de un módulo; sin módulo, todas.</summary>
    public static IReadOnlyList<PerfilSugerido> DelModulo(string? modulo) =>
        string.IsNullOrWhiteSpace(modulo)
            ? Todos
            : Todos.Where(p => string.Equals(p.Module, modulo.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Las plantillas de Cartera del crédito (las trece de la feature 013): las que exige la activación de créditos (D18).</summary>
    public static IReadOnlyList<PerfilSugerido> DeCreditos() =>
        DelModulo(ModuloCartera).Where(p => !ClavesDeCaptaciones.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).ToList();

    /// <summary>Las cuatro plantillas de captaciones y aportes: las que exige la activación de aportes o captaciones (D18).</summary>
    public static IReadOnlyList<PerfilSugerido> DeCaptaciones() =>
        DelModulo(ModuloCartera).Where(p => ClavesDeCaptaciones.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Expande la plantilla contra el catálogo de códigos <c>Resource.Action</c> de la cooperativa. Un código exacto
    /// entra si existe; un patrón entra con todo lo que case. Lo que no existe —el código exacto ausente o el patrón
    /// que no casa con nada— vuelve en <c>Omitidos</c>, tal como lo nombra la plantilla. Los dos, sin repetidos y en
    /// orden ordinal.
    /// </summary>
    public static (IReadOnlyList<string> Codigos, IReadOnlyList<string> Omitidos) Expandir(
        PerfilSugerido plantilla, IEnumerable<string> catalogo)
    {
        var codigosDelCatalogo = catalogo.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var porCodigo = codigosDelCatalogo.ToDictionary(c => c, c => c, StringComparer.OrdinalIgnoreCase);
        var codigos = new SortedSet<string>(StringComparer.Ordinal);
        var omitidos = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var patron in plantilla.Patrones)
        {
            if (!patron.Contains('*'))
            {
                if (porCodigo.TryGetValue(patron, out var exacto)) codigos.Add(exacto);
                else omitidos.Add(patron);
                continue;
            }

            var expresion = new Regex(
                "^" + Regex.Escape(patron).Replace("\\*", ".*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var casan = codigosDelCatalogo.Where(c => expresion.IsMatch(c)).ToList();
            if (casan.Count == 0) omitidos.Add(patron);
            foreach (var c in casan) codigos.Add(c);
        }

        foreach (var excluido in plantilla.Excluidos ?? [])
        {
            var expresion = new Regex(
                "^" + Regex.Escape(excluido).Replace("\\*", ".*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            codigos.RemoveWhere(expresion.IsMatch);
        }

        return ([.. codigos], [.. omitidos]);
    }
}
