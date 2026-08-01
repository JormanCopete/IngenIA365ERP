using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Implementación Redis de <see cref="ICentralRefreshTokenStore"/>.
///
/// <para>
/// Keys:
/// <list type="bullet">
///   <item><c>central-refresh:{tokenHashHex}</c> → <see cref="CentralRefreshSession"/> JSON.</item>
///   <item><c>central-refresh-family-invalid:{familyId:N}</c> → bit (existencia ⇒ invalidada).</item>
/// </list>
/// El TTL del registro de invalidación de familia se setea conservadoramente a
/// 7 días — el doble del TTL típico de un refresh (12h) para cubrir cualquier
/// token que aún estuviera en circulación. Tras 7 días la key cae sola.
/// </para>
/// </summary>
internal sealed class RedisCentralRefreshTokenStore(
    IConnectionMultiplexer redis,
    ILogger<RedisCentralRefreshTokenStore> logger) : ICentralRefreshTokenStore
{
    private const string SessionKeyPrefix = "central-refresh:";
    private const string FamilyInvalidKeyPrefix = "central-refresh-family-invalid:";
    private static readonly TimeSpan FamilyInvalidationTtl = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public async Task StoreAsync(
        string tokenHashHex, CentralRefreshSession session, TimeSpan ttl, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var payload = JsonSerializer.Serialize(session, JsonOpts);
        await db.StringSetAsync(SessionKeyPrefix + tokenHashHex, payload, ttl);
    }

    public async Task<CentralRefreshSession?> GetAsync(string tokenHashHex, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(SessionKeyPrefix + tokenHashHex);
        if (!raw.HasValue) return null;

        try
        {
            var session = JsonSerializer.Deserialize<CentralRefreshSession>((string)raw!, JsonOpts);
            if (session is null) return null;

            // Si la familia fue invalidada, NULL out aunque la key exista —
            // defensa contra reuso atrapando al atacante con el token rotado.
            var familyInvalid = await IsFamilyInvalidatedAsync(session.FamilyId, ct);
            return familyInvalid ? null : session;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Refresh token central con payload JSON inválido en key {Key}",
                SessionKeyPrefix + tokenHashHex);
            return null;
        }
    }

    public async Task MarkRotatedAsync(
        string tokenHashHex, string replacedByTokenHashHex, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(SessionKeyPrefix + tokenHashHex);
        if (!raw.HasValue) return;

        var session = JsonSerializer.Deserialize<CentralRefreshSession>((string)raw!, JsonOpts);
        if (session is null) return;

        var updated = session with { ReplacedByTokenHashHex = replacedByTokenHashHex };
        var payload = JsonSerializer.Serialize(updated, JsonOpts);

        // Preserva el TTL restante del original.
        var ttl = await db.KeyTimeToLiveAsync(SessionKeyPrefix + tokenHashHex);
        if (ttl.HasValue && ttl.Value > TimeSpan.Zero)
            await db.StringSetAsync(SessionKeyPrefix + tokenHashHex, payload, ttl.Value);
        else
            await db.StringSetAsync(SessionKeyPrefix + tokenHashHex, payload);
    }

    public Task DeleteAsync(string tokenHashHex, CancellationToken ct) =>
        redis.GetDatabase().KeyDeleteAsync(SessionKeyPrefix + tokenHashHex);

    public Task InvalidateFamilyAsync(Guid familyId, CancellationToken ct) =>
        redis.GetDatabase().StringSetAsync(
            FamilyInvalidKeyPrefix + familyId.ToString("N"),
            "1",
            FamilyInvalidationTtl);

    public async Task<bool> IsFamilyInvalidatedAsync(Guid familyId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        return await db.KeyExistsAsync(FamilyInvalidKeyPrefix + familyId.ToString("N"));
    }
}
