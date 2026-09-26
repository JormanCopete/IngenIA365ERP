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
        // Nomina (feature 005): el auditor exporta el detalle con explicaciones; el
        // operador registra novedades y calcula, pero NO aprueba, ni marca pagos, ni
        // reversa (segregacion de funciones, FR-020).
        // Feature 009: el auditor (y el revisor fiscal, que suele llevar ese rol) exporta los
        // libros y los estados financieros.
        ["Auditor"]      = ["*.View", "AuditLog.*", "Payroll.Runs.Export", "Accounting.Reports.Export"],
        // Feature 008: el operador crea y edita personas, empleados y asociados; no da de
        // baja (Core.People.Delete, Payroll.Employees.Terminate) — eso queda en CompanyAdmin.
        // Feature 009: registra borradores de comprobantes y exporta informes, pero NO
        // contabiliza, anula, cierra ni parametriza (segregación; cuatro ojos opcional).
        // Feature 010 (contracts/api.md §1): ve, registra, calcula y genera —prima, cesantías,
        // vacaciones, definitiva, porcentaje P2, PILA, nómina electrónica, dispersión— y digita
        // saldos iniciales; NO aprueba, reversa, transmite, marca enviado/cargado/consignado,
        // ajusta descuentos ni administra políticas, festivos, formatos ni habilitación (FR-006).
        ["Operator"]     = ["*.View", "Attachments.*", "Notifications.ManageOwn",
                            "Payroll.Novelties.*", "Payroll.Runs.Calculate",
                            "Core.People.Create", "Core.People.Update",
                            "Core.Associates.Create", "Core.Associates.Update",
                            "Payroll.Employees.Create", "Payroll.Employees.Update",
                            "Accounting.Vouchers.Create", "Accounting.Reports.Export",
                            "Payroll.ServiceBonus.Calculate", "Payroll.Severance.Calculate",
                            "Payroll.Vacations.Register", "Payroll.Vacations.Calculate",
                            "Payroll.Settlements.Calculate", "Payroll.BenefitBalances.Manage",
                            "Payroll.WithholdingRate.Calculate", "Payroll.Pila.Generate",
                            "Payroll.ElectronicPayroll.Generate", "Payroll.Disbursement.Generate"],
        ["ReadOnly"]     = ["*.View"],
        // Feature 012 (T126, T48; contracts/api.md §1.2): a propósito, ningún patrón nombra Inventory.*,
        // ElectronicInvoicing.*, Core.Taxes.*, Core.PaymentMeans.* ni Accounting.Inventory*: los tres roles de
        // arriba reciben sólo su *.View (las lecturas sensibles usan otra acción) y CompanyAdmin todo por "*".
        // La escritura del comercio se da con roles creados desde PerfilesSugeridos. Lo fija PermisosDeInventarioTests.
    };

    /// <summary>
    /// Lectura de los maestros de persona que recibe <b>todo</b> rol de la cooperativa, también
    /// los personalizados (feature 008, FR-010). Hasta que la API exigió permiso, cualquier
    /// sesión podía consultar Personas, Empleados y Asociados; conservar esa lectura el día del
    /// despliegue evita que un rol creado por el administrador amanezca con 404. Los
    /// built-in ya la tienen por <c>*.View</c>; el paso explícito es para los demás.
    /// </summary>
    internal static readonly string[] LecturaDeMaestros =
    [
        "Core.People.View",
        "Core.Associates.View",
        "Payroll.Employees.View",
        // Feature 009: el plan de cuentas y los tipos de comprobante los eligen todos los
        // módulos (cuentas por concepto, parámetros de cartera, bancos…); sin lectura, sus
        // buscadores amanecerían vacíos.
        "Accounting.Accounts.View",
        "Accounting.VoucherTypes.View",
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Role>>();
        await SeedAsync(db, logger);
    }

    /// <summary>
    /// Siembra sobre el contexto que se le pase. Ver la nota de la sobrecarga
    /// equivalente en <see cref="DomainPermissionCatalogSeeder"/>: los roles tienen
    /// que existir dentro del esquema de cada cooperativa, no solo en dbo.
    /// </summary>
    /// <returns>
    /// Cuántos vínculos rol↔permiso se insertaron. Si es mayor que cero, quien llama
    /// invalida la caché de permisos de la cooperativa: tiene TTL de 30 minutos y, sin eso,
    /// el día del despliegue los usuarios verían 404 en los maestros media hora.
    /// </returns>
    public static async Task<int> SeedAsync(IApplicationDbContext db, ILogger logger)
    {
        try
        {
            // Los roles se siembran con TenantId = null, y ahora eso es lo correcto y
            // no un provisional: con aislamiento por esquema, dentro del esquema de
            // una cooperativa todos los roles son suyos y la columna no discrimina
            // nada. Ya no hay plantillas que replicar — este seeder corre una vez
            // por esquema.
            await SeedRolesAsync(db, logger);
            var vinculos = await SeedRolePermissionsAsync(db, logger);
            vinculos += await ConcederLecturaDeMaestrosATodosLosRolesAsync(db, logger);
            await PurgarPermisosSaasGlobalesAsync(db, logger);
            return vinculos;
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
            return 0;
        }
    }

    /// <summary>
    /// Todo rol de la cooperativa —built-in o personalizado— recibe
    /// <see cref="LecturaDeMaestros"/> si le falta. Idempotente: sin faltantes no toca la base.
    /// </summary>
    private static async Task<int> ConcederLecturaDeMaestrosATodosLosRolesAsync(
        IApplicationDbContext db, ILogger logger)
    {
        var lectura = (await db.Permissions
                .IgnoreQueryFilters()
                .Select(p => new { p.Id, p.Resource, p.Action })
                .ToListAsync())
            .Where(p => LecturaDeMaestros.Contains($"{p.Resource}.{p.Action}", StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .ToList();
        if (lectura.Count == 0)
        {
            logger.LogWarning("Faltan los permisos de lectura de maestros en SEC_Permissions — corre CorePermissionCatalogSeeder y PayrollPermissionCatalogSeeder antes.");
            return 0;
        }

        var roles = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.IsActive)
            .Select(r => r.Id)
            .ToListAsync();
        if (roles.Count == 0) return 0;

        var existentes = (await db.RolePermissions.IgnoreQueryFilters()
                .Where(rp => lectura.Contains(rp.PermissionId))
                .Select(rp => new { rp.RoleId, rp.PermissionId })
                .ToListAsync())
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();

        var insertados = 0;
        foreach (var rolId in roles)
        foreach (var permisoId in lectura)
        {
            if (existentes.Contains((rolId, permisoId))) continue;
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = rolId,
                PermissionId = permisoId,
                CreatedBy = "Seed",
                UpdatedBy = "Seed"
            });
            insertados++;
        }

        if (insertados == 0) return 0;

        await db.SaveChangesAsync(default);
        logger.LogInformation(
            "Lectura de Personas/Empleados/Asociados concedida a los roles que no la tenían: {Insertados} vínculo(s).",
            insertados);
        return insertados;
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

    private static async Task<int> SeedRolePermissionsAsync(IApplicationDbContext db, ILogger logger)
    {
        var roles = await db.Roles
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == null && r.IsBuiltIn)
            .ToListAsync();
        if (roles.Count == 0) return 0;

        var permissions = await db.Permissions
            .IgnoreQueryFilters()
            .Select(p => new { p.Id, p.Resource, p.Action })
            .ToListAsync();
        if (permissions.Count == 0)
        {
            logger.LogWarning("No hay permisos en SEC_Permissions — corre DomainPermissionCatalogSeeder antes.");
            return 0;
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
        return inserted;
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
