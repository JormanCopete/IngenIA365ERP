namespace IngenIA365ERP.Identity.Models;

public record AuthResponse(
    Guid UserPublicId,
    string FullName,
    string Email,
    string TenantId,
    string TenantName,
    IList<string> Roles,
    IList<string> Permissions,
    TokenResponse Tokens
);
