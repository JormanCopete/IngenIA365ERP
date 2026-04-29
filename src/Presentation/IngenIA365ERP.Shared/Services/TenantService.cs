namespace IngenIA365ERP.Shared.Services;

public class TenantService : ITenantService
{
    private readonly ISecureStorage _storage;
    private string? _currentTenantId;
    private string? _currentTenantName;

    public TenantService(ISecureStorage storage)
    {
        _storage = storage;
    }

    public string? CurrentTenantId => _currentTenantId;
    public string? CurrentTenantName => _currentTenantName;

    public async Task<bool> SetTenantAsync(string tenantIdentifier)
    {
        _currentTenantId = tenantIdentifier;
        _currentTenantName = tenantIdentifier; // Would be resolved from API
        await _storage.SetAsync("tenant_id", tenantIdentifier);
        return true;
    }

    public Task ClearTenantAsync()
    {
        _currentTenantId = null;
        _currentTenantName = null;
        _storage.Remove("tenant_id");
        return Task.CompletedTask;
    }
}
