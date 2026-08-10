using IngenIA365ERP.Application.Common.Interfaces.Database;
using IngenIA365ERP.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace IngenIA365ERP.Persistence.Seeding;

/// <summary>
/// Adapter de <see cref="IDataSeedRunner"/> (Application) sobre el
/// <see cref="SeedOrchestrator"/> (feature 004 — T041). Resuelve el
/// tenantPublicId al identificador del directorio y aplica la salvaguarda
/// FR-017 (demo en Production exige confirmacion explicita).
/// </summary>
public sealed class DataSeedRunner(
    SeedOrchestrator orchestrator,
    TenantDbContext tenantDb,
    IHostEnvironment environment) : IDataSeedRunner
{
    public async Task<IReadOnlyList<SeedRunEntry>> RunAsync(
        string category, string? scope, Guid? tenantPublicId, bool confirmTestSeed, CancellationToken ct)
    {
        if (!Enum.TryParse<SeedCategory>(category, ignoreCase: true, out var cat))
            throw new SeedRunException("Database.Seed.InvalidCategory",
                "Categoría inválida. Valores válidos: Parametric, Test.");

        SeedScope? scopeEnum = scope is null
            ? null
            : Enum.TryParse<SeedScope>(scope, ignoreCase: true, out var s)
                ? s
                : throw new SeedRunException("Database.Seed.InvalidScope",
                    "Alcance inválido. Valores válidos: Admin, Tenant, All.");

        if (cat == SeedCategory.Test && environment.IsProduction() && !confirmTestSeed)
            throw new SeedRunException("Database.Seed.ConfirmationRequired",
                "Cargar datos de demostración en Producción requiere confirmTestSeed=true (FR-017).");

        string? tenantIdentifier = null;
        if (tenantPublicId is not null)
        {
            tenantIdentifier = (await tenantDb.Tenants
                    .Where(t => t.PublicId == tenantPublicId)
                    .Select(t => t.Identifier)
                    .FirstOrDefaultAsync(ct))
                ?? throw new SeedRunException("Database.Seed.TenantNotFound",
                    "No existe un tenant activo con el identificador indicado.");
        }

        try
        {
            var results = await orchestrator.RunAsync(cat, scopeEnum, tenantIdentifier, ct);
            return results
                .Select(r => new SeedRunEntry(r.Name, r.Scope.ToString(), r.TenantsTouched, r.Inserted))
                .ToList();
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("[Database.", StringComparison.Ordinal))
        {
            var code = ex.Message[1..ex.Message.IndexOf(']')];
            throw new SeedRunException(code, ex.Message);
        }
    }
}
