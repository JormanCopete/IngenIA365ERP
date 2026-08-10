using IngenIA365ERP.Persistence.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Seed;

/// <summary>
/// Feature 004 (T037, primera iteracion): adapta los seeders Phase 0 de
/// seguridad (catalogo de permisos → roles built-in → SEC_Users admin) al
/// framework de seeding, de modo que corran DESPUES de las migraciones del
/// inicializador (antes vivian en Program.cs y reventaban contra BD virgen).
///
/// Alcance actual: esquema default ("dbo"). Los seeders legacy resuelven sus
/// DbContext del scope ambiente; el reparto fino por esquema de tenant llega
/// con la descomposicion en RolesSeeder/PermissionsSeeder dedicados
/// (tasks T037-T039 definitivas).
/// NOTA: IdentitySeedData (AspNetUsers legacy) quedo EXCLUIDO a proposito —
/// sus tablas no existen en instalaciones greenfield (ErpIdentityDbContext ya
/// no migra) y el flujo central del feature 002 no las usa.
/// </summary>
public sealed class PhaseZeroSecuritySeeder(IServiceProvider serviceProvider) : IDataSeeder
{
    public int Order => 20;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public async Task<int> SeedAsync(SeedContext context, CancellationToken ct)
    {
        if (context.Tenant is not null)
        {
            // Esquemas de tenant dedicados: pendiente de la descomposicion
            // granular (los seeders legacy operan sobre el scope default).
            context.Logger.LogInformation(
                "PhaseZeroSecuritySeeder: esquema {Schema} omitido (solo default por ahora).",
                context.Tenant.Schema);
            return 0;
        }

        // Orden Phase 0: catalogo de permisos → roles built-in → usuario admin.
        await DomainPermissionCatalogSeeder.SeedAsync(serviceProvider);
        await BuiltInRolesSeeder.SeedAsync(serviceProvider);
        await DomainSecuritySeedData.SeedAsync(serviceProvider);

        // Los seeders legacy no reportan conteos; 0 = "sin conteo disponible".
        return 0;
    }
}
