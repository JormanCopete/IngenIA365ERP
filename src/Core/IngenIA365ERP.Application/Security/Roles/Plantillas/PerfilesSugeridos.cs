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

    /// <summary>El módulo de las trece plantillas de Cartera (feature 013, T0221; DT §2.14; contracts/api.md §2.5).</summary>
    public const string ModuloCartera = "Lending";

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
            // K3, bloque D (decisión del dueño del 2026-10-15): Attachments.Upload, porque sube los soportes de novedades, eventos del deudor y castigo.
            ["Lending.*", "Attachments.Upload"],
            "Todas las oficinas y líneas (trae Lending.Scope.AllOffices y AllLines); sin límite de monto.",
            // US5 (T0555, api.md §2.5): Lending.TestDoubles.Operate no lo trae ninguna plantilla; se concede a mano fuera de producción.
            ["Lending.Decisions.*", "Lending.Sessions.Vote", "Lending.TestDoubles.Operate"]),

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
                "Attachments.Upload",
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
                "Lending.Loans.Read", "Lending.Ratings.Read", "Attachments.Upload",
            ],
            "Todas las oficinas y líneas."),

        new("cartera.gestor-cobranza", ModuloCartera, "Gestor de cobranza",
            "Gestiona la cobranza: registra gestiones, propone acuerdos y escribe al deudor.",
            [
                "Lending.Collections.Read", "Lending.Collections.RecordActivity", "Lending.PaymentAgreements.Create", "Lending.Loans.Read",
                "Lending.Payoffs.Read", "Lending.DebtorMessages.Read", "Lending.DebtorMessages.Send", "Lending.PriorNotices.Read",
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
