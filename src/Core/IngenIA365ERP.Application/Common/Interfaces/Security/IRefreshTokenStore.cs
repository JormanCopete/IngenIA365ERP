namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Almacén distribuido (Redis) para refresh tokens. Permite implementar
/// rotación con detección de reuso (familia de tokens). Si llega un refresh
/// que ya fue rotado, la familia entera se invalida.
/// </summary>
public interface IRefreshTokenStore
{
    Task StoreAsync(string token, RefreshTokenContext context, TimeSpan ttl, CancellationToken ct);
    Task<RefreshTokenContext?> GetAsync(string token, CancellationToken ct);
    Task MarkRotatedAsync(string token, string replacedByToken, CancellationToken ct);
    Task InvalidateFamilyAsync(string familyId, CancellationToken ct);
    Task<bool> IsFamilyInvalidatedAsync(string familyId, CancellationToken ct);
}

public sealed record RefreshTokenContext(
    int UserId,
    string TenantId,
    string FamilyId,
    DateTime IssuedAt,
    string? IpAddress,
    string? UserAgent,
    string? ReplacedByToken);
