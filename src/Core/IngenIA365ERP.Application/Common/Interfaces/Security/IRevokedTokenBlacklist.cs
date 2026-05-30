namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Lista negra distribuida de <c>jti</c>s (JWT IDs) que ya fueron revocados.
/// Soporta logout-all (revocar todos los access tokens de un usuario) y
/// rotación forzosa por compromiso. TTL = vida útil del access token.
/// </summary>
public interface IRevokedTokenBlacklist
{
    Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct);
}
