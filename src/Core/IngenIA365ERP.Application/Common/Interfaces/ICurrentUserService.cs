namespace IngenIA365ERP.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? UserName { get; }
    string? TenantId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAuthenticated { get; }
}
