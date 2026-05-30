namespace IngenIA365ERP.Application.Security.Auth.Common;

public sealed record LoginChallengeResult(
    string MfaChallengeToken,
    bool MustChangePassword);

public sealed record AuthTokensResult(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt,
    AuthenticatedUserDto? User);

public sealed record AuthenticatedUserDto(
    Guid PublicId,
    string Username,
    AuthenticatedTenantDto Tenant,
    AuthenticatedBranchDto? Branch,
    IReadOnlyList<string> Roles);

public sealed record AuthenticatedTenantDto(Guid PublicId, string Name);
public sealed record AuthenticatedBranchDto(Guid PublicId, string Name);

public sealed record MfaEnrollmentStartResult(
    string Secret,
    string QrCodeSvg,
    string EnrollmentToken);

public sealed record MfaBackupCodesResult(IReadOnlyList<string> BackupCodes);

public sealed record MfaResetRequestResult(Guid RequestPublicId, DateTime ExpiresAt);

public sealed record MfaResetApprovalResult(string Status); // "Approved" | "Executed"
