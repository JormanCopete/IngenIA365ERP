using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 005 — permisos de nómina para <c>SEC_Permissions</c>, hermano de
/// <see cref="DomainPermissionCatalogSeeder"/> (misma convención
/// <c>Resource.Action</c>, misma idempotencia: sólo inserta los que faltan).
///
/// <para>
/// Los permisos legados de <c>IdentitySeedData</c> (<c>Payroll.Employees.*</c>,
/// <c>Payroll.PayrollPeriods.*</c>, <c>Payroll.PayrollReports.*</c>) se conservan;
/// estos no los reemplazan. La segregación de funciones (FR-020) descansa en que
/// <c>Runs.Approve</c>, <c>Runs.Reverse</c> y <c>Payments.Mark</c> sean permisos
/// distintos de <c>Novelties.*</c> y <c>Runs.Calculate</c>.
/// </para>
/// </summary>
public static class PayrollPermissionCatalogSeeder
{
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        ("Payroll.Plans",           "View",               "Ver planes de nómina"),
        ("Payroll.Plans",           "Manage",             "Crear, editar y desactivar planes; cambiar el plan de un empleado"),

        ("Payroll.Novelties",       "View",               "Ver novedades, historial de salarios y recurrentes"),
        ("Payroll.Novelties",       "Create",             "Registrar novedades, cambios de salario y recurrentes"),
        ("Payroll.Novelties",       "Update",             "Corregir una novedad (crea versión)"),
        ("Payroll.Novelties",       "Cancel",             "Anular una novedad; desactivar una recurrente"),
        ("Payroll.Novelties",       "Import",             "Descargar plantilla y cargar archivo de novedades"),

        ("Payroll.Runs",            "View",               "Ver borradores, detalle por empleado, comparativo y cuadre"),
        ("Payroll.Runs",            "Calculate",          "Calcular o recalcular el período"),
        ("Payroll.Runs",            "Approve",            "Aprobar la liquidación (genera el asiento contable)"),
        ("Payroll.Runs",            "AuthorizeException", "Autorizar excepciones a los bloqueos de aprobación"),
        ("Payroll.Runs",            "Reverse",            "Reversar un período aprobado"),
        ("Payroll.Runs",            "Export",             "Exportar el detalle de la liquidación con explicaciones"),

        ("Payroll.Payments",        "View",               "Ver la relación de pago"),
        ("Payroll.Payments",        "Mark",               "Marcar pagado (total o por empleado)"),
        ("Payroll.Payments",        "Unmark",             "Retirar una marca de pago (con motivo)"),

        ("Payroll.Payslips",        "View",               "Ver y descargar comprobantes de pago"),
        ("Payroll.Payslips",        "Send",               "Enviar comprobantes de pago por correo"),

        ("Payroll.Concepts",        "View",               "Ver definiciones de conceptos, versiones y catálogo heredado; probar en seco"),
        ("Payroll.Concepts",        "Manage",             "Crear, revisar y desactivar conceptos; cuentas contables; reaplicar la semilla"),

        ("Payroll.LegalParameters", "View",               "Ver parámetros legales y sus vigencias"),
        ("Payroll.LegalParameters", "Manage",             "Registrar vigencias nuevas de parámetros legales"),
    ];

    public static async Task SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Permissions
            .IgnoreQueryFilters()
            .Where(p => p.Resource.StartsWith("Payroll."))
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
            logger.LogDebug("Permisos de nómina ya presentes ({Count}).", Catalog.Length);
            return;
        }

        db.Permissions.AddRange(toInsert);
        await db.SaveChangesAsync(default);
        logger.LogInformation("Permisos de nómina sembrados: {Inserted} nuevos de {Total}.", toInsert.Count, Catalog.Length);
    }
}
