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
public sealed record PerfilSugerido(
    string Key,
    string Module,
    string Actor,
    string Description,
    IReadOnlyList<string> Patrones,
    string Notes);

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

        return ([.. codigos], [.. omitidos]);
    }
}
