using System.Security.Claims;
using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.API.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public int? UserId
    {
        get
        {
            var raw = User?.FindFirst("uid")?.Value
                   ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? UserName =>
        User?.FindFirst("name")?.Value
        ?? User?.Identity?.Name;

    public string? TenantId =>
        User?.FindFirst("tenant_id")?.Value;

    public IReadOnlyList<string> Roles =>
        User?.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList()
        ?? new List<string>();

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
