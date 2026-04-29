namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// Attaches X-Tenant-Id to every outgoing request.
///
/// Reads from ITenantService (in-memory, fast path) first, then falls back to
/// ISecureStorage. The storage fallback covers two cases:
///   1. IHttpClientFactory resolves the handler in a separate DI scope, so the
///      ITenantService instance the handler sees may not be the one AuthService
///      updated after login.
///   2. After a browser refresh, the in-memory ITenantService is empty but
///      "tenant_id" is still in localStorage from the previous session.
/// </summary>
public class TenantDelegatingHandler : DelegatingHandler
{
    public const string TenantStorageKey = "tenant_id";

    private readonly ITenantService _tenantService;
    private readonly ISecureStorage _storage;

    public TenantDelegatingHandler(ITenantService tenantService, ISecureStorage storage)
    {
        _tenantService = tenantService;
        _storage = storage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("X-Tenant-Id"))
        {
            var tenantId = _tenantService.CurrentTenantId;
            if (string.IsNullOrEmpty(tenantId))
                tenantId = await _storage.GetAsync(TenantStorageKey);

            if (!string.IsNullOrEmpty(tenantId))
                request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
