using IngenIA365ERP.Application.Common.Interfaces.Security;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Security;

internal sealed class RedisRevokedTokenBlacklist(IConnectionMultiplexer redis) : IRevokedTokenBlacklist
{
    private const string Prefix = "auth:revoked:";
    private static string Key(string jti) => Prefix + jti;

    public Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct)
        => redis.GetDatabase().StringSetAsync(Key(jti), "1", ttl);

    public Task<bool> IsRevokedAsync(string jti, CancellationToken ct)
        => redis.GetDatabase().KeyExistsAsync(Key(jti));
}
