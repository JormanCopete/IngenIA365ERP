namespace IngenIA365ERP.Shared.Services;

public interface ITenantService
{
    string? CurrentTenantId { get; }
    string? CurrentTenantName { get; }
    Task<bool> SetTenantAsync(string tenantIdentifier);
    Task ClearTenantAsync();
}
