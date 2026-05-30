namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Cache de challenge MFA emitido tras un login con credenciales correctas.
/// El challenge se canjea por un par access+refresh cuando el usuario
/// confirma el segundo factor (TOTP o backup code). Vida útil: 5 min.
/// </summary>
public interface IMfaChallengeStore
{
    Task<string> IssueAsync(MfaChallengeContext context, TimeSpan ttl, CancellationToken ct);
    Task<MfaChallengeContext?> ConsumeAsync(string token, CancellationToken ct);
}

public sealed record MfaChallengeContext(
    int UserId,
    Guid UserPublicId,
    string Username,
    int? TenantId,
    Guid TenantPublicId,
    string TenantIdentifier,
    DateTime IssuedAt);

/// <summary>
/// Cache del token corto de inscripción MFA. El token se emite por
/// <c>mfa/enroll/start</c> y se canjea por <c>mfa/enroll/confirm</c>.
/// </summary>
public interface IMfaEnrollmentStore
{
    Task<string> IssueAsync(MfaEnrollmentContext context, TimeSpan ttl, CancellationToken ct);
    Task<MfaEnrollmentContext?> GetAsync(string token, CancellationToken ct);
    Task RemoveAsync(string token, CancellationToken ct);
}

public sealed record MfaEnrollmentContext(
    int UserId,
    Guid UserPublicId,
    string Base32Secret,
    DateTime IssuedAt);
