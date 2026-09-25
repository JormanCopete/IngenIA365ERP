using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 012 (T123; decisiones-transversales §2.10, T48; contracts/api.md §1.1) — permisos del inventario
/// comercial para <c>SEC_Permissions</c>, hermano de <see cref="PayrollPermissionCatalogSeeder"/>: misma convención
/// <c>Resource.Action</c>, misma idempotencia (sólo inserta los que faltan). Lo invoca
/// <see cref="PhaseZeroSecuritySeeder"/> antes de <see cref="BuiltInRolesSeeder"/>, nunca el DbMigrator.
///
/// <para>
/// Se siembra <b>entero</b> en I1, también lo que sólo tiene efecto en entregas posteriores (ventas, POS, caja,
/// tablero…), para que las plantillas de rol (<c>PerfilesSugeridos</c>) no cambien de forma entre entregas.
/// </para>
///
/// <para>
/// Toda lectura sensible usa una acción distinta de <c>View</c> (<c>Costs.Read</c>, <c>Scope.*</c>,
/// <c>CashSessions.ViewAll</c>, <c>Reports.Export</c>, <c>Reports.ExportPersonalData</c>): los roles integrados
/// Operator, ReadOnly y Auditor reciben todo <c>*.View</c> por su glob, y así no reciben nada más del módulo.
/// Crear, confirmar y aprobar son acciones distintas (segregación).
/// </para>
/// </summary>
public static class InventoryPermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Inventory.Catalog",          "View",                          "Ver unidades, categorías, marcas, grupos contables, productos, causas de ajuste y canales de venta"),
        ("Inventory.Catalog",          "Manage",                        "Administrar el catálogo: unidades, categorías, marcas, grupos contables, productos, causas y canales"),
        ("Inventory.Catalog",          "Import",                        "Descargar las plantillas del catálogo y cargarlas"),
        ("Inventory.Catalog",          "Export",                        "Descargar el catálogo en su plantilla"),
        ("Inventory.Catalog",          "ReclassifyAccountingGroup",     "Cambiar el grupo contable de un producto que ya tuvo movimientos"),

        ("Inventory.Salespeople",      "View",                          "Ver vendedores"),
        ("Inventory.Salespeople",      "Manage",                        "Registrar y editar el rol vendedor"),

        ("Inventory.Warehouses",       "View",                          "Ver tipos de bodega, bodegas, ubicaciones y políticas de reorden"),
        ("Inventory.Warehouses",       "Manage",                        "Administrar tipos de bodega, bodegas, ubicaciones y políticas de reorden"),
        ("Inventory.Warehouses",       "Activate",                      "Pedir la comparación con contabilidad y activar una bodega con cuadre"),
        ("Inventory.Warehouses",       "AcceptActivationDifference",    "Activar una bodega con diferencia frente a contabilidad, con motivo"),

        ("Inventory.Stock",            "View",                          "Ver existencia física, reservada, disponible y en tránsito, sin valores"),
        ("Inventory.Costs",            "Read",                          "Ver costo unitario, costo total, promedio y valorizado en cualquier respuesta del módulo"),
        ("Inventory.Costing",          "Manage",                        "Registrar vigencias del método y ámbito de costeo y de la retroactividad"),

        ("Inventory.Integrity",        "Verify",                        "Verificar el kardex contra las proyecciones de existencias y costo"),
        ("Inventory.Integrity",        "Rebuild",                       "Reconstruir las proyecciones de existencias y costo"),

        ("Inventory.Periods",          "View",                          "Ver los períodos de inventario"),
        ("Inventory.Periods",          "Close",                         "Cerrar un período de inventario"),
        ("Inventory.Periods",          "Reopen",                        "Reabrir un período de inventario cerrado (permiso especial)"),
        ("Inventory.Periods",          "AcceptUnbilledShipments",       "Aceptar con motivo las remisiones sin facturar al cerrar el período"),

        ("Inventory.DocumentTypes",    "View",                          "Ver tipos de documento y consecutivos"),
        ("Inventory.DocumentTypes",    "Manage",                        "Administrar tipos de documento y consecutivos"),
        ("Inventory.DocumentTypes",    "DisableFiscalPosting",          "Dejar sin paso a contabilidad un tipo de documento fiscal"),

        ("Inventory.Documents",        "View",                          "Ver la lista y el detalle genéricos de documentos (el grupo exige además su lectura)"),
        ("Inventory.Documents",        "Reprint",                       "Reimprimir un documento"),

        ("Inventory.Parameters",       "View",                          "Ver los parámetros del módulo y sus vigencias"),
        ("Inventory.Parameters",       "Manage",                        "Registrar vigencias nuevas de los parámetros del módulo"),

        ("Inventory.ApprovalPolicies", "View",                          "Ver políticas de aprobación y montos máximos por rol"),
        ("Inventory.ApprovalPolicies", "Manage",                        "Administrar políticas de aprobación por tipo de documento y montos máximos por rol"),

        ("Inventory.Approvals",        "View",                          "Abrir la bandeja de aprobaciones y seguir el estado de una solicitud"),
        ("Inventory.Approvals",        "Supervisor",                    "Decidir los niveles de aprobación de supervisor"),
        ("Inventory.Approvals",        "Management",                    "Decidir los niveles de aprobación de gerencia"),

        ("Inventory.Scopes",           "Manage",                        "Asignar bodegas y puntos de venta a los usuarios"),
        ("Inventory.Scope",            "AllWarehouses",                 "Alcance sobre todas las bodegas sin filas de asignación"),
        ("Inventory.Scope",            "AllPointsOfSale",               "Alcance sobre todos los puntos de venta sin filas de asignación"),

        ("Inventory.Adjustments",      "View",                          "Ver ajustes, consumos internos, bajas y movimientos entre ubicaciones"),
        ("Inventory.Adjustments",      "Create",                        "Registrar borradores de ajustes, consumos internos, bajas y movimientos entre ubicaciones"),
        ("Inventory.Adjustments",      "Confirm",                       "Confirmar ajustes, consumos internos, bajas y movimientos entre ubicaciones"),
        ("Inventory.Adjustments",      "Approve",                       "Aprobar ajustes, consumos internos y bajas"),
        ("Inventory.Adjustments",      "Void",                          "Anular ajustes, consumos internos, bajas y movimientos entre ubicaciones"),
        ("Inventory.Adjustments",      "SetUnitCost",                   "Indicar el costo unitario de un ajuste positivo"),

        ("Inventory.Transfers",        "View",                          "Ver traslados entre bodegas"),
        ("Inventory.Transfers",        "Create",                        "Registrar borradores de traslado"),
        ("Inventory.Transfers",        "Dispatch",                      "Despachar un traslado"),
        ("Inventory.Transfers",        "Receive",                       "Recibir un traslado y resolver sus diferencias"),
        ("Inventory.Transfers",        "Approve",                       "Aprobar traslados"),
        ("Inventory.Transfers",        "Void",                          "Anular traslados"),

        ("Inventory.Counts",           "View",                          "Ver conteos físicos"),
        ("Inventory.Counts",           "Open",                          "Abrir un conteo físico"),
        ("Inventory.Counts",           "Capture",                       "Registrar las cantidades contadas"),
        ("Inventory.Counts",           "Close",                         "Cerrar un conteo y generar su ajuste"),
        ("Inventory.Counts",           "Approve",                       "Aprobar el ajuste de un conteo"),

        ("Inventory.Purchases",        "View",                          "Ver recepciones, compras, facturas, notas y devoluciones del proveedor"),
        ("Inventory.Purchases",        "Create",                        "Registrar borradores de recepción, compra, factura, nota y devolución al proveedor"),
        ("Inventory.Purchases",        "Confirm",                       "Confirmar recepciones, compras, facturas, notas y devoluciones al proveedor"),
        ("Inventory.Purchases",        "Approve",                       "Aprobar documentos de compra"),
        ("Inventory.Purchases",        "Void",                          "Anular documentos de compra"),
        ("Inventory.Purchases",        "RegisterRadianEvent",           "Anotar un evento RADIAN (030/032) emitido por fuera del ERP"),
        ("Inventory.Purchases",        "EmitRadianEvent",               "Emitir eventos RADIAN desde el ERP"),

        ("Inventory.Sales",            "View",                          "Ver ventas, remisiones y notas"),
        ("Inventory.Sales",            "Create",                        "Registrar borradores de venta, remisión y nota"),
        ("Inventory.Sales",            "Confirm",                       "Confirmar ventas, remisiones y notas"),
        ("Inventory.Sales",            "Approve",                       "Aprobar documentos de venta"),
        ("Inventory.Sales",            "Void",                          "Anular documentos de venta"),
        ("Inventory.Sales",            "SellOnCredit",                  "Vender a crédito"),
        ("Inventory.Sales",            "RefundOtherMeans",              "Devolver el dinero por un medio distinto del original"),

        ("Inventory.Pos",              "Sell",                          "Vender en el punto de venta"),
        ("Inventory.Discounts",        "Authorize",                     "Autorizar descuentos por encima del tope"),
        ("Inventory.Prices",           "View",                          "Ver listas de precios"),
        ("Inventory.Prices",           "Manage",                        "Administrar listas de precios"),
        ("Inventory.DiscountCaps",     "Manage",                        "Administrar los topes de descuento"),

        ("Inventory.PointsOfSale",     "View",                          "Ver puntos de venta y cajas"),
        ("Inventory.PointsOfSale",     "Manage",                        "Administrar puntos de venta y cajas"),

        ("Inventory.CashSessions",     "View",                          "Ver las sesiones de caja propias"),
        ("Inventory.CashSessions",     "ViewAll",                       "Ver las sesiones de caja de todos los cajeros"),
        ("Inventory.CashSessions",     "Open",                          "Abrir una sesión de caja"),
        ("Inventory.CashSessions",     "Close",                         "Cerrar una sesión de caja con arqueo"),

        ("Inventory.CashMovements",    "Create",                        "Registrar ingresos y retiros de caja"),
        ("Inventory.CashMovements",    "Approve",                       "Aprobar ingresos y retiros de caja"),
        ("Inventory.CashDifferences",  "Approve",                       "Aprobar diferencias de arqueo"),

        ("Inventory.DayClose",         "Execute",                       "Ejecutar el cierre del día"),
        ("Inventory.DayClose",         "Reopen",                        "Reabrir un día cerrado"),

        ("Inventory.OpeningBalance",   "Load",                          "Importar, confirmar y anular el saldo inicial de inventario"),
        ("Inventory.OpeningBalance",   "Approve",                       "Aprobar el saldo inicial de inventario"),
        ("Inventory.LegacyFigures",    "Import",                        "Importar y consultar las cifras de SOLIDO"),

        ("Inventory.Messages",         "View",                          "Ver la bandeja de mensajes y su estado en cada documento"),
        ("Inventory.Messages",         "Reprocess",                     "Reprocesar un mensaje fallido"),
        ("Inventory.Messages",         "SendNotApplicable",             "Marcar un mensaje como no aplicable, con motivo"),

        ("Inventory.Reconciliation",   "View",                          "Ver la conciliación de inventario con contabilidad"),
        ("Inventory.Dashboard",        "View",                          "Ver el tablero del inventario comercial"),

        ("Inventory.Reports",          "View",                          "Consultar los informes de inventario en pantalla"),
        ("Inventory.Reports",          "Export",                        "Exportar los informes de inventario"),
        ("Inventory.Reports",          "ExportPersonalData",            "Exportar informes con datos personales de terceros"),

        ("Inventory.Alerts",           "View",                          "Ver la bandeja de alertas"),
        ("Inventory.Alerts",           "Attend",                        "Atender una alerta"),
        ("Inventory.Alerts",           "Manage",                        "Configurar los tipos de alerta"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith("Inventory."))
            .Select(p => new { p.Resource, p.Action })
            .ToListAsync();

        var existingKeys = existing
            .Select(p => $"{p.Resource}.{p.Action}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = Catalog
            .Where(p => !existingKeys.Contains($"{p.Resource}.{p.Action}"))
            .Select(p => new Permission
            {
                Resource = p.Resource,
                Action = p.Action,
                Description = p.Description,
                CreatedBy = "Seed",
                UpdatedBy = "Seed"
            })
            .ToList();

        if (toInsert.Count == 0)
        {
            logger.LogDebug("Permisos de inventario ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de inventario sembrados: {Inserted} nuevos de {Total}.", toInsert.Count, Catalog.Length);
    }
}
