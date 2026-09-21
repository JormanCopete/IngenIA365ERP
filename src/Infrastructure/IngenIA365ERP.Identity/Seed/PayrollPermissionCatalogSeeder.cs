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
        // Feature 008: la ficha del empleado tenía sólo códigos legados (Read/Create/Update/
        // Delete en IdentitySeedData, que nadie ejecuta). Estos son los que exige la API.
        ("Payroll.Employees",       "View",               "Ver empleados y su ficha"),
        ("Payroll.Employees",       "Create",             "Registrar empleados (con persona existente o nueva)"),
        ("Payroll.Employees",       "Update",             "Editar la ficha laboral de un empleado"),
        ("Payroll.Employees",       "Terminate",          "Terminar el contrato de un empleado"),

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

        // Feature 010 (contracts/api.md §1): cuatro liquidaciones en cuatro recursos —la R12
        // proponía uno solo— para que una cooperativa pueda dar prima y PILA a una persona sin
        // darle definitivas. Segregación como la ordinaria: calcular, registrar y generar son
        // permisos distintos de aprobar, reversar, transmitir, marcar y administrar.
        ("Payroll.ServiceBonus",    "View",               "Ver liquidaciones de prima de servicios"),
        ("Payroll.ServiceBonus",    "Calculate",          "Calcular, recalcular o descartar la prima del semestre"),
        ("Payroll.ServiceBonus",    "Approve",            "Aprobar la prima (contabiliza contra la provisión)"),
        ("Payroll.ServiceBonus",    "Reverse",            "Reversar una prima aprobada"),

        ("Payroll.Severance",       "View",               "Ver liquidaciones de cesantías e intereses y la relación por fondo"),
        ("Payroll.Severance",       "Calculate",          "Calcular, recalcular o descartar las cesantías del año"),
        ("Payroll.Severance",       "Approve",            "Aprobar las cesantías e intereses"),
        ("Payroll.Severance",       "Reverse",            "Reversar unas cesantías aprobadas"),
        ("Payroll.Severance",       "MarkDeposited",      "Registrar la consignación de las cesantías a cada fondo"),

        ("Payroll.Vacations",       "View",               "Ver saldo, movimientos y liquidaciones de vacaciones"),
        ("Payroll.Vacations",       "Register",           "Registrar disfrute o compensación de vacaciones (y ver la vista previa de días)"),
        ("Payroll.Vacations",       "Calculate",          "Recalcular o descartar la liquidación de vacaciones"),
        ("Payroll.Vacations",       "Approve",            "Aprobar la liquidación de vacaciones"),
        ("Payroll.Vacations",       "Reverse",            "Reversar una liquidación de vacaciones aprobada"),

        ("Payroll.Settlements",     "View",               "Ver terminaciones y liquidaciones definitivas"),
        ("Payroll.Settlements",     "Calculate",          "Registrar la terminación y calcular, recalcular o descartar la definitiva"),
        ("Payroll.Settlements",     "Approve",            "Aprobar la definitiva (cierra la ficha y aplica los descuentos en Cartera)"),
        ("Payroll.Settlements",     "Reverse",            "Reversar una definitiva aprobada (reintegra la ficha)"),
        ("Payroll.Settlements",     "AdjustDeduction",    "Bajar un descuento de Cartera propuesto en la definitiva, con motivo"),
        ("Payroll.Settlements",     "Manage",             "Administrar el catálogo de motivos de retiro"),

        ("Payroll.BenefitBalances", "View",               "Ver saldos iniciales de prestaciones"),
        ("Payroll.BenefitBalances", "Manage",             "Digitar y ajustar saldos iniciales de prestaciones"),

        ("Payroll.WithholdingRate", "View",               "Ver cálculos del porcentaje fijo del procedimiento 2"),
        ("Payroll.WithholdingRate", "Calculate",          "Calcular el porcentaje fijo del procedimiento 2"),
        ("Payroll.WithholdingRate", "Approve",            "Aprobar el porcentaje calculado (abre la vigencia del semestre)"),

        ("Payroll.Pila",            "View",               "Ver generaciones de PILA, inconsistencias y descargar el archivo"),
        ("Payroll.Pila",            "Generate",           "Validar y generar la planilla PILA del mes"),
        ("Payroll.Pila",            "MarkUploaded",       "Marcar la planilla como cargada en el operador"),
        ("Payroll.Pila",            "Manage",             "Administrar los datos del aportante"),

        ("Payroll.ElectronicPayroll", "View",             "Ver documentos de nómina electrónica, estados y respuestas de la DIAN"),
        ("Payroll.ElectronicPayroll", "Generate",         "Generar los documentos del mes y las notas de ajuste"),
        ("Payroll.ElectronicPayroll", "Transmit",         "Transmitir a la DIAN y consultar estado"),
        ("Payroll.ElectronicPayroll", "Manage",           "Administrar la habilitación, los rangos de numeración y el set de pruebas"),

        ("Payroll.Disbursement",    "View",               "Ver archivos de dispersión bancaria"),
        ("Payroll.Disbursement",    "Generate",           "Generar el archivo de dispersión de una corrida aprobada"),
        ("Payroll.Disbursement",    "MarkSent",           "Marcar el archivo como enviado al banco (marca los pagos)"),
        ("Payroll.Disbursement",    "Manage",             "Administrar los formatos de archivo por banco"),

        ("Payroll.CompanyPolicies", "View",               "Ver las políticas de nómina de la empresa y sus vigencias"),
        ("Payroll.CompanyPolicies", "Manage",             "Registrar vigencias nuevas de las políticas de nómina"),

        ("Payroll.Holidays",        "View",               "Ver el calendario de festivos"),
        ("Payroll.Holidays",        "Manage",             "Registrar y retirar festivos decretados o manuales"),
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
