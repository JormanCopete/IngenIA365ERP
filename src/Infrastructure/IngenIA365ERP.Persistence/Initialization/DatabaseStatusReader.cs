using IngenIA365ERP.Application.Common.Interfaces.Database;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Persistence.Initialization;

/// <summary>
/// Adapter de <see cref="IDatabaseStatusReader"/> (feature 004 — T041,
/// contracts/database-admin.md). Consulta cada esquema por separado
/// (principio IV) y jamas incluye cadenas de conexion.
/// </summary>
public sealed class DatabaseStatusReader(
    AdminDbContext adminDb,
    ApplicationDbContext appDb,
    TenantSchemaService tenantSchemaService,
    IOptions<DatabaseOptions> options) : IDatabaseStatusReader
{
    public async Task<DatabaseStatusDto> GetStatusAsync(CancellationToken ct)
    {
        var opts = options.Value;

        var admin = await ScopeStatusAsync(adminDb, ct);
        var application = await ScopeStatusAsync(appDb, ct);

        var tenants = new List<TenantSchemaStatusDto>();
        foreach (var tenant in await tenantSchemaService.ListTenantsAsync())
        {
            if (string.IsNullOrWhiteSpace(tenant.Schema) ||
                tenant.Schema!.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                continue;
            var pending = await tenantSchemaService.GetPendingMigrationsAsync(tenant.Schema!, ct);
            tenants.Add(new TenantSchemaStatusDto(
                tenant.PublicId, tenant.Name ?? tenant.Identifier ?? tenant.Schema!, tenant.Schema!, pending));
        }

        return new DatabaseStatusDto(
            opts.ProviderKey,
            opts.AutoMigrate,
            admin,
            application,
            tenants,
            opts.Seed.RunParametricSeed,
            opts.Seed.RunTestSeed);
    }

    private static async Task<DatabaseScopeStatusDto> ScopeStatusAsync(
        Microsoft.EntityFrameworkCore.DbContext db, CancellationToken ct)
    {
        if (!await db.Database.CanConnectAsync(ct))
            return new DatabaseScopeStatusDto(0, db.Database.GetMigrations().ToList());

        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).Count();
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        return new DatabaseScopeStatusDto(applied, pending);
    }
}
