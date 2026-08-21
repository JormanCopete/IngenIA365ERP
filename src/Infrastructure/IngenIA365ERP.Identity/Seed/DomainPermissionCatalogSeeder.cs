using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// T066 — Catálogo inmutable de permisos para la tabla de dominio
/// <c>SEC_Permissions</c> (no confundir con <c>IdentitySeedData</c> que
/// alimenta la tabla legacy de ASP.NET Identity).
///
/// Convención: <c>Code = "{Resource}.{Action}"</c>. Resource sigue el patrón
/// <c>Modulo.Recurso</c> (p. ej. <c>Admin.Tenants</c>, <c>Security.Users</c>),
/// Action es el verbo del caso de uso (<c>View</c>, <c>Create</c>, <c>Update</c>, …).
///
/// El catálogo cubre los recursos introducidos en Fase 0 (Admin + Security +
/// Audit + Attachments + Notifications + Compliance). Los módulos posteriores
/// añaden sus propios permisos vía un seeder hermano cuando aterrizan.
///
/// Idempotente: solo inserta los códigos que aún no existen.
/// </summary>
public static class DomainPermissionCatalogSeeder
{
    /// <summary>Permisos Phase 0 — Resource + Action.</summary>
    internal static readonly (string Resource, string Action, string Description)[] Catalog =
    [
        // Admin (SaaS-global)
        ("Admin.Tenants",          "View",     "Listar cooperativas-tenant del SaaS"),
        ("Admin.Tenants",          "Create",   "Registrar nueva cooperativa-tenant"),
        ("Admin.Tenants",          "Update",   "Actualizar datos legales / NIT / régimen"),
        ("Admin.Tenants",          "Suspend",  "Suspender una cooperativa por falta de pago / SARLAFT"),
        ("Admin.Tenants",          "Activate", "Reactivar una cooperativa suspendida"),

        // Admin Branches (por tenant)
        ("Admin.Branches",         "View",     "Listar sucursales de la cooperativa"),
        ("Admin.Branches",         "Create",   "Crear sucursal"),
        ("Admin.Branches",         "Update",   "Actualizar sucursal"),
        ("Admin.Branches",         "Deactivate", "Desactivar sucursal"),

        // Security — Users
        ("Security.Users",         "View",     "Ver usuarios de la cooperativa"),
        ("Security.Users",         "Create",   "Registrar usuario"),
        ("Security.Users",         "Update",   "Editar usuario"),
        ("Security.Users",         "Disable",  "Deshabilitar usuario (soft-delete)"),
        ("Security.Users",         "Restore",  "Restaurar usuario soft-deleted"),
        ("Security.Users",         "AssignRole",     "Asignar rol a usuario"),
        ("Security.Users",         "RemoveRole",     "Quitar rol a usuario"),
        ("Security.Users",         "AssignBranch",   "Asignar sucursal a usuario"),
        ("Security.Users",         "ResetPassword",  "Reset administrativo de contraseña"),
        ("Security.Users",         "Unlock",         "Desbloquear usuario tras lockout"),

        // Security — Roles
        ("Security.Roles",         "View",     "Ver roles disponibles"),
        ("Security.Roles",         "Create",   "Crear rol personalizado"),
        ("Security.Roles",         "Update",   "Editar rol (asignar/quitar permisos)"),
        ("Security.Roles",         "Delete",   "Eliminar rol personalizado (no built-in)"),

        // Security — Permission catalog (read-only)
        ("Security.Permissions",   "View",     "Ver catálogo de permisos del sistema"),

        // Security — Parametros del sistema (COR_SystemSettings). Cambian el
        // comportamiento contable y regional de toda la cooperativa, asi que
        // se separan de Users y Roles: ver la configuracion no debe implicar
        // poder cambiarla.
        ("Security.Parameters",    "View",     "Ver parámetros de configuración de la cooperativa"),
        ("Security.Parameters",    "Update",   "Cambiar el valor de un parámetro del sistema"),

        // Security — MFA reset (doble aprobación)
        ("Security.MfaReset",      "Request",  "Solicitar reset administrativo de MFA para otro usuario"),
        ("Security.MfaReset",      "Approve",  "Aprobar solicitud de reset de MFA"),

        // Audit log
        ("AuditLog",               "View",     "Consultar el audit log de la cooperativa"),
        ("AuditLog",               "Export",   "Exportar audit log a CSV / PDF firmado"),

        // Attachments (adjuntos cifrados)
        ("Attachments",            "Upload",   "Subir archivo cifrado"),
        ("Attachments",            "Download", "Descargar archivo cifrado"),
        ("Attachments",            "Delete",   "Eliminar archivo (soft-delete)"),

        // Notifications
        ("Notifications",          "Send",     "Disparar notificación interna a otro usuario"),
        ("Notifications",          "ManageOwn", "Marcar/archivar las propias notificaciones"),

        // Compliance — Habeas data
        ("Compliance.HabeasData",  "Publish",      "Publicar nueva versión de política habeas data"),
        ("Compliance.HabeasData",  "RecordConsent", "Registrar consentimiento de un titular"),
        ("Compliance.HabeasData",  "Revoke",        "Registrar revocación de consentimiento"),
        ("Compliance.HabeasData",  "ViewHistory",   "Consultar historial de consentimientos"),

        // SaaS operator (acceso transversal del operador del producto)
        ("Saas.AuditLog",          "Verify",   "Validar HMAC de un PDF firmado del audit log"),
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Permission>>();

        try
        {
            var existing = await db.Permissions
                .IgnoreQueryFilters()
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
                logger.LogInformation("Catálogo SEC_Permissions ya está al día ({Existing} entradas, 0 nuevos).",
                    existingKeys.Count);
                return;
            }

            db.Permissions.AddRange(toInsert);
            await db.SaveChangesAsync(default);
            logger.LogInformation("Catálogo SEC_Permissions sembrado: {Inserted} nuevos, {Existing} ya existentes.",
                toInsert.Count, existingKeys.Count);
        }
        catch (Exception ex) when (IsSchemaIssue(ex))
        {
            logger.LogError(ex,
                "SEC_Permissions no está alineada con la entidad Permission. " +
                "Ejecuta database/migration/22_SEC_Permissions_Resource_Action_Plus_Roles_TenantNullable.sql " +
                "y reinicia.");
        }
    }

    private static bool IsSchemaIssue(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().FullName ?? string.Empty;
            if (!typeName.Equals("Microsoft.Data.SqlClient.SqlException", StringComparison.Ordinal))
            {
                continue;
            }
            var numberProp = current.GetType().GetProperty("Number");
            if (numberProp?.GetValue(current) is int number
                && (number is 207 or 208 or 515 or 547))
            {
                return true;
            }
        }
        return false;
    }
}
