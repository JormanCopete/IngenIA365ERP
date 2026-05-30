using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Auth.Common;
using MediatR;

namespace IngenIA365ERP.Application.Security.Auth.VerifyMfa;

/// <summary>
/// Paso 2 del flujo de autenticación (FR-005). Canjea el challenge MFA por
/// un par access+refresh. Admite TOTP o, alternativamente, un código de
/// respaldo de uso único.
/// </summary>
public sealed record VerifyMfaCommand(
    string MfaChallengeToken,
    string? TotpCode,
    bool UseBackupCode,
    string? BackupCode,
    Guid? BranchPublicId,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<AuthTokensResult>>;
