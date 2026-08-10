using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Initialization;

/// <summary>
/// Calcula migraciones pendientes por base y por esquema de tenant (FR-011,
/// FR-021, data-model §3). Con AutoMigrate=false y pendientes, el inicializador
/// hace fail-fast enumerandolas; el seeding tambien se rehusa a correr sobre
/// esquema desactualizado.
/// </summary>
public sealed class PendingMigrationsGuard(
    AdminDbContext adminDb,
    ApplicationDbContext appDb,
    TenantSchemaService tenantSchemaService)
{
    public sealed record PendingReport(
        IReadOnlyList<string> Admin,
        IReadOnlyList<string> Application,
        IReadOnlyDictionary<string, IReadOnlyList<string>> TenantSchemas)
    {
        public bool HasAny => Admin.Count > 0 || Application.Count > 0 || TenantSchemas.Any(t => t.Value.Count > 0);

        public string Describe()
        {
            var parts = new List<string>();
            if (Admin.Count > 0) parts.Add($"admin: {string.Join(", ", Admin)}");
            if (Application.Count > 0) parts.Add($"operativa: {string.Join(", ", Application)}");
            foreach (var (schema, pending) in TenantSchemas.Where(t => t.Value.Count > 0))
                parts.Add($"tenant {schema}: {string.Join(", ", pending)}");
            return string.Join(" | ", parts);
        }
    }

    public async Task<PendingReport> ComputeAsync(CancellationToken ct)
    {
        var adminReachable = await adminDb.Database.CanConnectAsync(ct);
        var admin = await SafePendingAsync(adminDb, ct);
        var application = await SafePendingAsync(appDb, ct);

        var tenants = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        if (!adminReachable)
        {
            // BD admin inexistente ⇒ no hay directorio de tenants que consultar;
            // el reporte "admin: todo pendiente" ya cuenta la historia completa.
            return new PendingReport(admin, application, tenants);
        }

        foreach (var tenant in await tenantSchemaService.ListTenantsAsync())
        {
            var schema = tenant.Schema;
            if (string.IsNullOrWhiteSpace(schema) || schema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                continue; // el esquema default lo cubre el chequeo "operativa"
            tenants[schema] = await tenantSchemaService.GetPendingMigrationsAsync(schema!, ct);
        }

        return new PendingReport(admin, application, tenants);
    }

    /// <summary>Base inexistente todavia ⇒ todas las migraciones estan pendientes.</summary>
    private static async Task<IReadOnlyList<string>> SafePendingAsync(
        Microsoft.EntityFrameworkCore.DbContext context, CancellationToken ct)
    {
        if (!await context.Database.CanConnectAsync(ct))
            return context.Database.GetMigrations().ToList();
        return (await context.Database.GetPendingMigrationsAsync(ct)).ToList();
    }
}
