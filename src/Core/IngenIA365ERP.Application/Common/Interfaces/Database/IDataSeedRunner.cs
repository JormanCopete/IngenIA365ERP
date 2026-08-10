namespace IngenIA365ERP.Application.Common.Interfaces.Database;

/// <summary>
/// Abstraccion del seeding bajo demanda (feature 004 — FR-019). La
/// implementacion vive en Infrastructure/Persistence (SeedOrchestrator);
/// Application solo conoce este contrato (principio II).
/// </summary>
public interface IDataSeedRunner
{
    /// <exception cref="SeedRunException">Con codigo Database.Seed.* (esquema desactualizado, tenant inexistente, confirmacion faltante).</exception>
    Task<IReadOnlyList<SeedRunEntry>> RunAsync(
        string category,
        string? scope,
        Guid? tenantPublicId,
        bool confirmTestSeed,
        CancellationToken ct);
}

public sealed record SeedRunEntry(string Name, string Scope, int TenantsTouched, int Inserted);

/// <summary>Fallo de seeding con codigo namespaced (Database.Seed.*).</summary>
public sealed class SeedRunException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
