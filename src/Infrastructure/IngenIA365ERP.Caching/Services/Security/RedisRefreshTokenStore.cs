using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Security;

internal sealed class RedisRefreshTokenStore(IConnectionMultiplexer redis) : IRefreshTokenStore
{
    private const string TokenPrefix = "auth:refresh:";
    private const string FamilyPrefix = "auth:refreshfamily:";

    private static string Key(string token) => TokenPrefix + token;
    private static string FamilyKey(string familyId) => FamilyPrefix + familyId;

    public async Task StoreAsync(string token, RefreshTokenContext context, TimeSpan ttl, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var payload = JsonSerializer.Serialize(context);
        await db.StringSetAsync(Key(token), payload, ttl);
    }

    public async Task<RefreshTokenContext?> GetAsync(string token, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(Key(token));
        return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<RefreshTokenContext>((string)raw!);
    }

    public async Task MarkRotatedAsync(string token, string replacedByToken, CancellationToken ct)
    {
        var existing = await GetAsync(token, ct);
        if (existing is null) return;
        var rotated = existing with { ReplacedByToken = replacedByToken };
        var db = redis.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync(Key(token)) ?? TimeSpan.FromHours(12);
        await db.StringSetAsync(Key(token), JsonSerializer.Serialize(rotated), ttl);
    }

    public async Task InvalidateFamilyAsync(string familyId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(FamilyKey(familyId), "1", TimeSpan.FromHours(24));
    }

    public async Task<bool> IsFamilyInvalidatedAsync(string familyId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        return await db.KeyExistsAsync(FamilyKey(familyId));
    }
}
