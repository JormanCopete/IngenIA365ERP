using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Implementación Redis de <see cref="IMfaPendingStore"/>. Key namespace
/// <c>mfa-pending:{centralUserId:N}</c>. Cada Begin sobrescribe el anterior
/// (idempotente — no necesita TX porque Redis SET es atómico).
/// </summary>
internal sealed class RedisMfaPendingStore(IConnectionMultiplexer redis) : IMfaPendingStore
{
    private const string KeyPrefix = "mfa-pending:";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public Task StoreAsync(
        Guid centralUserId, MfaPendingEnrollment pending, TimeSpan ttl, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var payload = JsonSerializer.Serialize(pending, JsonOpts);
        return db.StringSetAsync(KeyPrefix + centralUserId.ToString("N"), payload, ttl);
    }

    public async Task<MfaPendingEnrollment?> GetAsync(Guid centralUserId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(KeyPrefix + centralUserId.ToString("N"));
        if (!raw.HasValue) return null;

        try
        {
            return JsonSerializer.Deserialize<MfaPendingEnrollment>((string)raw!, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task ClearAsync(Guid centralUserId, CancellationToken ct) =>
        redis.GetDatabase().KeyDeleteAsync(KeyPrefix + centralUserId.ToString("N"));
}
