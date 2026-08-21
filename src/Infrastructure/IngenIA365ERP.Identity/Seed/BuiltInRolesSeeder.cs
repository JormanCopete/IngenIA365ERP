using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// T067 — Roles built-in por tenant (FR-020). Cada cooperativa al provisionarse
/// recibe estos roles automáticamente. Aquí se siembran para el tenant SaaS-global
/// (TenantId = null) — la siembra por tenant real ocurre en
/// <c>ProvisionTenantSchemaCommand</c> (T073).
///
/// Roles:
///  - <b>CompanyAdmin</b>: acceso total dentro de la cooperativa. NO se puede eliminar.
///  - <b>Auditor</b>: solo lectura + audit log + export. NO se puede eliminar.
///  - <b>Operator</b>: operación regular (transacciones, sin admin).
///  - <b>ReadOnly</b>: solo consultas.
///
/// El patrón de permisos por rol se expresa con globs (<c>Admin.*</c>, <c>*.View</c>)
/// y se resuelve contra el catálogo en <c>SEC_Permissions</c>. Idempotente:
/// solo inserta roles y asociaciones faltantes.
/// </summary>
public static class BuiltInRolesSeeder
{
    private static readonly (string Code, string Name, string Description)[] Roles =
    [
        ("CompanyAdmin", "Administrador de Cooperativa",
            "Acceso completo a la cooperativa: usuarios, roles, sucursales, audit log, configuración."),
        ("Auditor", "Auditor",
            "Solo lectura + acceso completo al audit log y exportación."),
        ("Operator", "Operador",
            "Operación regular: transacciones del día a día, sin administración."),
        ("ReadOnly", "Solo Lectura",
            "Consultas sobre todos los módulos. No puede crear / editar."),
    ];

    /// <summary>
    /// Permisos SaaS-globales: operan SOBRE las cooperativas, no dentro de una.
    /// Ningún rol los recibe, ni siquiera CompanyAdmin. Sólo el administrador
    /// maestro, por el atajo de <c>PermissionAuthorizationFilter</c>.
    ///
    /// <para>
    /// Sin esta lista, los globs de abajo los reparten solos: <c>"*"</c> se los da
    /// enteros a CompanyAdmin —incluido <c>Admin.Tenants.Suspend</c>— y
    /// <c>"*.View"</c> le da <c>Admin.Tenants.View</c> a ReadOnly, Operator y
    /// Auditor. Como <c>ProvisionTenantSchemaCommandHandler</c> clona estos
    /// vínculos a cada cooperativa, y <c>/api/saas/tenants</c> está exento de la
    /// resolución de tenant y no filtra por cooperativa, el administrador de la
    /// cooperativa A podría listar y suspender la B. Es fuga entre cooperativas
    /// contra el Principio IV.
    /// </para>
    ///
    /// <para>
    /// Hoy está dormido porque el token central no lleva claims <c>perm</c> y la
    /// comprobación nunca da verdadera. Deja de estarlo en cuanto los permisos se
    /// resuelvan de verdad — por eso se cierra ANTES de tocar el filtro.
    /// </para>
    ///
    /// <para>
    /// <b>Admin.Branches NO va aquí</b>: pese al prefijo, las sucursales son de
    /// la cooperativa.
    /// </para>
    /// </summary>
    private static readonly string[] PermisosSaasGlobales =
    [
        "Admin.Tenants.View",
        "Admin.Tenants.Create",
        "Admin.Tenants.Update",
        "Admin.Tenants.Suspend",
        "Admin.Tenants.Activate",
        "Saas.AuditLog.Verify",
    ];

    internal static bool EsSaasGlobal(string codigo) =>
        PermisosSaasGlobales.Contains(codigo, StringComparer.OrdinalIgnoreCase);

    /// <summary>Patrón glob de permisos por rol.</summary>
    internal static readonly Dictionary<string, string[]> PermissionPatterns = new()
    {
        ["CompanyAdmin"] = ["*"],                                   // todo dentro del tenant
        ["Auditor"]      = ["*.View", "AuditLog.*"],                // read-only + audit completo
        ["Operator"]     = ["*.View", "Attachments.*", "Notifications.ManageOwn"],
        ["ReadOnly"]     = ["*.View"],
    };

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Role>>();

        try
        {
            // El seed corre con TenantId = null (rol SaaS-global "plantilla").
            // ProvisionTenantSchemaCommand (T073) replicará la plantilla a cada
            // tenant nuevo cambiando TenantId.
            await SeedRolesAsync(db, logger);
            await SeedRolePermissionsAsync(db, logger);
            await PurgarPermisosSaasGlobalesAsync(db, logger);
        }
        catch (Exception ex) when (IsSchemaIssue(ex))
        {
            logger.LogError(ex,
                "SEC_Roles / SEC_Permissions / SEC_RolePermissions están desfasadas " +
                "o tienen constraints incompatibles. Ejecuta:\n" +
                "  - database/migration/19_SEC_Roles_UserRoles_BackfillColumns.sql\n" +
                "  - database/migration/21_Roles_Scope_BuiltIn.sql\n" +
                "  - database/migration/22_SEC_Permissions_Resource_Action_Plus_Roles_TenantNullable.sql\n" +
                "El seed se omitió — los endpoints US2 no funcionarán hasta aplicar las migraciones.");
        }
    }

    private static bool IsSchemaIssue(Exception ex)
    {
        // Walk all inner exceptions buscando una SqlException con un código de
        // schema mismatch (207 = invalid column, 208 = invalid object,
        // 515 = NULL en columna NOT NULL, 547 = FK constraint).
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

    private static async Task SeedRolesAsync(IApplicationDbContext db, ILogger logger)
    {
        var existing = await db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == null)
            .Select(r => r.Code)
            .ToListAsync();
        var existingCodes = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toInsert = Roles
            .Where(r => !existingCodes.Contains(r.Code))
            .Select(r => new Role
            {
                TenantId = null,
                Code = r.Code,
                Name = r.Name,
                Description = r.Description,
                IsBuiltIn = true,
                IsSystemRole = true, // compat legacy
                IsAssignable = true,
                IsActive = true,
                CreatedBy = "Seed",
                UpdatedBy = "Seed"
            })
            .ToList();

        if (toInsert.Count > 0)
        {
            db.Roles.AddRange(toInsert);
            await db.SaveChangesAsync(default);
            logger.LogInformation("Built-in roles sembrados: {Inserted} nuevos, {Existing} ya existentes.",
                toInsert.Count, existingCodes.Count);
        }
        else
        {
            logger.LogInformation("Built-in roles ya están al día ({Count} entradas).", existingCodes.Count);
        }
    }

    /// <summary>
    /// Retira los vínculos rol↔permiso SaaS-global que el glob repartió antes de
    /// que existiera <see cref="PermisosSaasGlobales"/>.
    ///
    /// <para>
    /// Dejar de insertarlos no basta: el sembrado es sólo-inserción, así que las
    /// filas ya creadas sobreviven a todos los arranques. Y no están sólo en las
    /// plantillas — <c>ProvisionTenantSchemaCommandHandler</c> ya las clonó a
    /// cada cooperativa aprovisionada. Por eso esto barre TODOS los roles, no
    /// sólo los de <c>TenantId == null</c>.
    /// </para>
    ///
    /// <para>
    /// Idempotente: cuando no queda ninguno, no toca la base ni escribe log.
    /// </para>
    /// </summary>
    /// <summary>
    /// Qué códigos le tocan a un rol built-in, dado el catálogo. Es el reparto
    /// entero, sin base de datos de por medio, para poder fijarlo con pruebas:
    /// el glob concede, y <see cref="PermisosSaasGlobales"/> retiene.
    /// </summary>
    internal static HashSet<string> CodigosParaRol(
        string codigoRol, IEnumerable<string> codigosCatalogo)
    {
        if (!PermissionPatterns.TryGetValue(codigoRol, out var patrones))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return codigosCatalogo
            .Where(c => patrones.Any(pat => MatchesGlob(pat, c)))
            .Where(c => !EsSaasGlobal(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task PurgarPermisosSaasGlobalesAsync(
        IApplicationDbContext db, ILogger logger)
    {
        var globales = (await db.Permissions
                .IgnoreQueryFilters()
                .Select(p => new { p.Id, p.Resource, p.Action })
                .ToListAsync())
            .Where(p => EsSaasGlobal($"{p.Resource}.{p.Action}"))
            .Select(p => p.Id)
            .ToHashSet();

        if (globales.Count == 0) return;

        var sobrantes = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => globales.Contains(rp.PermissionId))
            .ToListAsync();

        if (sobrantes.Count == 0) return;

        db.RolePermissions.RemoveRange(sobrantes);
        await db.SaveChangesAsync(default);

        // Warning y no Information a propósito: si esto aparece en un arranque
        // que no sea el primero tras el cambio, alguien volvió a repartirlos.
        logger.LogWarning(
            "Retirados {Cantidad} vínculo(s) rol↔permiso SaaS-global. Esos permisos " +
            "operan sobre las cooperativas y sólo los ejerce el administrador maestro.",
            sobrantes.Count);
    }

    private static async Task SeedRolePermissionsAsync(IApplicationDbContext db, ILogger logger)
    {
        var roles = await db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == null && r.IsBuiltIn)
            .ToListAsync();
        if (roles.Count == 0) return;

        var permissions = await db.Permissions
            .IgnoreQueryFilters()
            .Select(p => new { p.Id, p.Resource, p.Action })
            .ToListAsync();
        if (permissions.Count == 0)
        {
            logger.LogWarning("No hay permisos en SEC_Permissions — corre DomainPermissionCatalogSeeder antes.");
            return;
        }

        var existingLinks = await db.RolePermissions
            .IgnoreQueryFilters()
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync();
        var existingSet = existingLinks
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();

        var inserted = 0;
        foreach (var role in roles)
        {

            var concedidos = CodigosParaRol(
                role.Code,
                permissions.Select(p => $"{p.Resource}.{p.Action}"));

            var matching = permissions
                .Where(p => concedidos.Contains($"{p.Resource}.{p.Action}"))
                .ToList();

            foreach (var perm in matching)
            {
                if (existingSet.Contains((role.Id, perm.Id))) continue;
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = perm.Id,
                    CreatedBy = "Seed",
                    UpdatedBy = "Seed"
                });
                inserted++;
            }
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(default);
            logger.LogInformation("Built-in role↔permission links sembrados: {Inserted} nuevos.", inserted);
        }
        else
        {
            logger.LogInformation("Built-in role↔permission links ya están al día.");
        }
    }

    /// <summary>
    /// Match estilo glob: <c>*</c> = cualquier prefijo/sufijo, sin escapes.
    /// Ejemplos: <c>"*"</c> match todo; <c>"*.View"</c> match cualquier recurso
    /// con action View; <c>"Admin.*"</c> match todo bajo Admin.
    /// </summary>
    internal static bool MatchesGlob(string pattern, string value)
    {
        if (pattern == "*") return true;

        // Convertir glob a regex simple.
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(
            value, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
