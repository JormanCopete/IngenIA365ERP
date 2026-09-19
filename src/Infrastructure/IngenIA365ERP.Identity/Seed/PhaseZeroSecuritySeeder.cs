using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Persistence.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Siembra el catálogo de permisos y los roles built-in <b>dentro del esquema de
/// cada cooperativa</b>.
///
/// <para>
/// <b>Qué hacía antes.</b> Si el esquema no era <c>dbo</c>, escribía
/// <c>"esquema {Schema} omitido (solo default por ahora)"</c> y devolvía cero.
/// No era un descuido: estaba declarado, con un TODO, y el log lo repetía seis
/// veces en cada arranque. La consecuencia medida: <c>SEC_Permissions</c>,
/// <c>SEC_Roles</c> y <c>SEC_RolePermissions</c> con 40, 4 y 66 filas en
/// <c>dbo</c>, y <b>cero</b> en los seis esquemas de cooperativa. Un esquema sin
/// roles no puede autorizar a nadie.
/// </para>
///
/// <para>
/// Mientras todo se resolvía a <c>dbo</c> daba igual. Con el aislamiento por
/// esquema cableado, es la diferencia entre una cooperativa que funciona y una
/// que responde 404 a todo con los registros limpios.
/// </para>
///
/// <para>
/// Los seeders concretos ya no resuelven su contexto del contenedor —que apunta
/// al esquema ambiente— sino que reciben el de <see cref="SeedContext.TenantDb"/>,
/// que el orquestador entrega apuntado al esquema en curso.
/// </para>
/// </summary>
/// <param name="cachePermisos">
/// Opcional: la caché de permisos resueltos (Redis, TTL 30 min). Cuando el sembrado inserta
/// vínculos rol↔permiso nuevos —feature 008: lectura de maestros para todo rol— se invalida
/// la de la cooperativa, si no los usuarios verían 404 en Personas media hora tras desplegar.
/// </param>
public sealed class PhaseZeroSecuritySeeder(IPermissionClaimsCache? cachePermisos = null) : IDataSeeder
{
    public int Order => 20;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        var db = context.TenantDb
            ?? throw new InvalidOperationException(
                "PhaseZeroSecuritySeeder es de alcance Tenant y el orquestador no entregó TenantDb.");

        var esquema = context.Tenant?.Schema ?? "dbo";

        // Catálogo primero: los roles enlazan permisos que tienen que existir ya.
        await DomainPermissionCatalogSeeder.SeedAsync(db, context.Logger);
        // Nomina (feature 005): seeder hermano con los permisos Payroll.*; misma
        // idempotencia, mismo esquema en curso.
        await PayrollPermissionCatalogSeeder.SeedAsync(db, context.Logger);
        // Feature 008: maestros de Core (personas, asociados); Payroll.Employees.* va en el
        // seeder de nómina.
        await CorePermissionCatalogSeeder.SeedAsync(db, context.Logger);
        // Feature 009: contabilidad (30 codigos Accounting.*); antes ninguna ruta contable exigia permiso.
        await AccountingPermissionCatalogSeeder.SeedAsync(db, context.Logger);
        var vinculosNuevos = await BuiltInRolesSeeder.SeedAsync(db, context.Logger);
        if (vinculosNuevos > 0)
            await InvalidarCachePermisosAsync(context, vinculosNuevos, ct);

        context.Logger.LogInformation(
            "Seguridad sembrada en el esquema {Esquema}: catálogo de permisos y roles built-in.",
            esquema);

        // DomainSecuritySeedData ya no corre, y es a propósito.
        //
        // Sembraba un usuario administrador de Fase 0 en SEC_Users. Con una base por
        // cooperativa, la base por defecto no atiende a nadie: es sólo la plantilla
        // desde la que se migra. Un usuario ahí no puede entrar a ninguna parte, y
        // las filas SEC_Users de cada cooperativa las crea TenantUserProvisioner al
        // aceptarse una invitación, con la identidad central detrás.
        //
        // Además avisaba en falso: resolvía su contexto del contenedor, o sea otra
        // conexión, y leía los roles desde fuera de la transacción que el orquestador
        // mantiene abierta. Nunca los veía, y escribía "Rol CompanyAdmin no
        // encontrado" en cada arranque sobre base limpia — con los roles sembrados
        // correctamente tres líneas más arriba.

        // Los seeders legacy no reportan conteos; 0 = "sin conteo disponible".
        return 0;
    }

    private async Task InvalidarCachePermisosAsync(SeedContext context, int vinculos, CancellationToken ct)
    {
        if (cachePermisos is null || context.Tenant is null) return;
        try
        {
            // La clave de la caché es el Id interno de la cooperativa (UserPermissionResolver).
            await cachePermisos.InvalidateAllForTenantAsync(context.Tenant.InternalId.ToString(), ct);
            context.Logger.LogInformation(
                "Caché de permisos de {Cooperativa} invalidada tras {Vinculos} vínculo(s) nuevo(s).",
                context.Tenant.Identifier, vinculos);
        }
        catch (Exception ex)
        {
            // No detener el arranque por la caché: expira sola a los 30 minutos.
            context.Logger.LogWarning(ex,
                "No se pudo invalidar la caché de permisos de {Cooperativa}; expira sola en 30 minutos.",
                context.Tenant.Identifier);
        }
    }
}
