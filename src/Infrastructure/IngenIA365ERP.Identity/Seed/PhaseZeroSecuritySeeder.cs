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
public sealed class PhaseZeroSecuritySeeder(IServiceProvider serviceProvider) : IDataSeeder
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
        await BuiltInRolesSeeder.SeedAsync(db, context.Logger);

        context.Logger.LogInformation(
            "Seguridad sembrada en el esquema {Esquema}: catálogo de permisos y roles built-in.",
            esquema);

        // El usuario administrador de Fase 0 sólo tiene sentido en dbo, que ya no
        // atiende a ninguna cooperativa. Las filas SEC_Users de cada cooperativa las
        // crea TenantUserProvisioner al aceptarse una invitación, con la identidad
        // central detrás: sembrar usuarios por esquema crearía cuentas que nadie pidió.
        if (context.Tenant is null)
        {
            await DomainSecuritySeedData.SeedAsync(serviceProvider);
        }

        // Los seeders legacy no reportan conteos; 0 = "sin conteo disponible".
        return 0;
    }
}
