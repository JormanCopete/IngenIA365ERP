using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Security;

internal sealed class RedisMfaChallengeStore(IConnectionMultiplexer redis) : IMfaChallengeStore
{
    private const string Prefix = "auth:mfachallenge:";

    public async Task<string> IssueAsync(MfaChallengeContext context, TimeSpan ttl, CancellationToken ct)
    {
        var token = OpaqueToken();
        var db = redis.GetDatabase();
        await db.StringSetAsync(Prefix + token, JsonSerializer.Serialize(context), ttl);
        return token;
    }

    public async Task<MfaChallengeContext?> ConsumeAsync(string token, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = Prefix + token;
        // Lectura atómica + borrado (uso único).
        var raw = await db.StringGetDeleteAsync(key);
        return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<MfaChallengeContext>((string)raw!);
    }

    private static string OpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

internal sealed class RedisMfaEnrollmentStore(IConnectionMultiplexer redis) : IMfaEnrollmentStore
{
    private const string Prefix = "auth:mfaenroll:";

    public async Task<string> IssueAsync(MfaEnrollmentContext context, TimeSpan ttl, CancellationToken ct)
    {
        var token = OpaqueToken();
        var db = redis.GetDatabase();
        await db.StringSetAsync(Prefix + token, JsonSerializer.Serialize(context), ttl);
        return token;
    }

    public async Task<MfaEnrollmentContext?> GetAsync(string token, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(Prefix + token);
        return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<MfaEnrollmentContext>((string)raw!);
    }

    public async Task RemoveAsync(string token, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(Prefix + token);
    }

    private static string OpaqueToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
