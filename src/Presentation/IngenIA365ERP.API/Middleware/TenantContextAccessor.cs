using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.MultiTenancy;

namespace IngenIA365ERP.API.Middleware;

public class TenantContextAccessor : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContext;

    public TenantContextAccessor(IHttpContextAccessor httpContext)
        => _httpContext = httpContext;

    private ErpTenantInfo? TenantInfo =>
        _httpContext.HttpContext?.Items["TenantInfo"] as ErpTenantInfo;

    public string TenantId => TenantInfo?.Id ?? string.Empty;
    public string? TenantName => TenantInfo?.Name;
    public string? Schema => TenantInfo?.Schema;
    public string ConnectionString => TenantInfo?.ConnectionString ?? string.Empty;
}
