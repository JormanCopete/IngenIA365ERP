namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Emite y valida access tokens (JWT RS256). Convención de claims:
/// <c>sub</c>=PublicId del usuario, <c>uid</c>=Id interno, <c>tenant_id</c>=Tenant.PublicId,
/// <c>branch_id</c>=Branch.PublicId, <c>roles</c> y <c>perms</c> (CSV), <c>perm_ver</c>
/// (versión de permisos para invalidación), <c>jti</c> (JWT ID para blacklist).
/// </summary>
public interface IAccessTokenIssuer
{
    AccessTokenIssueResult IssueAccessToken(AccessTokenClaims claims);

    /// <summary>Genera un refresh token opaco aleatorio y devuelve token + hash.</summary>
    RefreshTokenIssueResult IssueRefreshToken();

    /// <summary>Hashea (SHA-256 hex) un refresh token para almacenamiento/lookup.</summary>
    string HashRefreshToken(string token);
}

public sealed record AccessTokenClaims(
    Guid UserPublicId,
    int UserId,
    string Username,
    Guid TenantPublicId,
    string TenantId,
    Guid? BranchPublicId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    int PermissionsVersion,
    string Jti);

public sealed record AccessTokenIssueResult(string Token, DateTime ExpiresAt, string Jti);

public sealed record RefreshTokenIssueResult(string Token, string TokenHash, DateTime ExpiresAt);
