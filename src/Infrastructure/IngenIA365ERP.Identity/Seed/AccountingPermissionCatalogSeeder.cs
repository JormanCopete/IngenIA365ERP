using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 009 — permisos del módulo de contabilidad para <c>SEC_Permissions</c>, hermano de
/// <see cref="CorePermissionCatalogSeeder"/> y <see cref="PayrollPermissionCatalogSeeder"/>:
/// misma convención <c>Resource.Action</c>, misma idempotencia (sólo inserta los que faltan).
///
/// <para>
/// Hasta el 2026-09-14 ninguna de las veinte rutas de <c>/api/accounting</c> exigía permiso:
/// cualquier sesión creaba, contabilizaba o anulaba comprobantes. Los códigos <c>Accounting.*</c>
/// sólo existían como glob en <c>IdentitySeedData</c>, que nadie ejecuta. Las acciones de
/// lectura son <c>View</c> a propósito (patrón <c>*.View</c> de los roles integrados);
/// <c>Vouchers.Create</c> (registrar borradores) y <c>Vouchers.Post</c> (contabilizar) son
/// permisos distintos por decisión del dueño (spec 009, Clarifications), y la regla de cuatro
/// ojos por empresa se apoya en esa separación.
/// </para>
/// </summary>
public static class AccountingPermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Accounting.Setup",          "View",         "Ver la configuración contable y los catálogos PUC"),
        ("Accounting.Setup",          "Manage",       "Iniciar la contabilidad, cambiar la configuración, importar y validar catálogos"),

        ("Accounting.Accounts",       "View",         "Ver el plan de cuentas y buscar cuentas (también desde otros módulos)"),
        ("Accounting.Accounts",       "Manage",       "Crear y editar auxiliares y sus reglas; inactivar y eliminar cuentas"),

        ("Accounting.VoucherTypes",   "View",         "Ver tipos de comprobante y tipos de documento cruce"),
        ("Accounting.VoucherTypes",   "Manage",       "Crear y editar tipos de comprobante y de documento cruce"),

        ("Accounting.Vouchers",       "View",         "Ver comprobantes y sus líneas"),
        ("Accounting.Vouchers",       "Create",       "Registrar y editar borradores de comprobantes manuales"),
        ("Accounting.Vouchers",       "Post",         "Contabilizar un borrador (recibe número; queda inmutable)"),
        ("Accounting.Vouchers",       "Void",         "Anular un comprobante contabilizado mediante reversión"),

        ("Accounting.Periods",        "View",         "Ver ejercicios y períodos"),
        ("Accounting.Periods",        "Close",        "Cerrar un período mensual"),
        ("Accounting.Periods",        "Reopen",       "Reabrir un período mensual (con motivo)"),
        ("Accounting.Periods",        "CloseYear",    "Abrir, cerrar y reabrir el ejercicio"),

        ("Accounting.Opening",        "Manage",       "Cargar los saldos de apertura"),

        ("Accounting.Reports",        "View",         "Consultar libros, informes y estados financieros en pantalla"),
        ("Accounting.Reports",        "Export",       "Exportar informes a Excel, PDF y Word"),

        ("Accounting.Reconciliation", "View",         "Ver conciliaciones bancarias"),
        ("Accounting.Reconciliation", "Manage",       "Cargar extractos, conciliar, cerrar y reabrir conciliaciones"),

        ("Accounting.Budget",         "View",         "Ver el presupuesto y su ejecución"),
        ("Accounting.Budget",         "Manage",       "Registrar, modificar y aprobar el presupuesto"),

        ("Accounting.Taxes",          "View",         "Ver cuentas de impuesto, tarifas, formularios e informes tributarios"),
        ("Accounting.Taxes",          "Manage",       "Parametrizar cuentas de impuesto, tarifas y formularios"),
        ("Accounting.Taxes",          "Certificates", "Generar, reexpedir y enviar certificados de retención"),

        ("Accounting.Exogenous",      "View",         "Ver formatos, conceptos y generaciones de información exógena"),
        ("Accounting.Exogenous",      "Manage",       "Parametrizar formatos y conceptos; generar la información"),
        ("Accounting.Exogenous",      "Export",       "Exportar la exógena en Excel y en el formato del prevalidador de la DIAN"),

        ("Accounting.Assets",         "View",         "Ver activos fijos, diferidos y sus cuotas"),
        ("Accounting.Assets",         "Manage",       "Registrar y editar activos y diferidos; dar de baja"),
        ("Accounting.Assets",         "Run",          "Ejecutar y reversar la depreciación y amortización del período"),

        // Feature 012 (T125, decisiones-transversales §2.10): el lado contable del inventario —la matriz de
        // contabilización y los lotes que pasan los mensajes al libro—; la consulta de lo contabilizado usa
        // Accounting.Vouchers.View.
        ("Accounting.InventoryRules",   "View",       "Ver la matriz de contabilización del inventario"),
        ("Accounting.InventoryRules",   "Manage",     "Parametrizar la matriz de contabilización del inventario"),
        ("Accounting.InventoryBatches", "View",       "Ver los lotes de contabilización del inventario"),
        ("Accounting.InventoryBatches", "Run",        "Ejecutar los lotes de contabilización del inventario"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith("Accounting."))
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
            logger.LogDebug("Permisos de contabilidad ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de contabilidad sembrados: {Inserted} nuevos de {Total}.", toInsert.Count, Catalog.Length);
    }
}
