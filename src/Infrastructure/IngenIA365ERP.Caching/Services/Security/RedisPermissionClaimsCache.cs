using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Security;

internal sealed class RedisPermissionClaimsCache(IConnectionMultiplexer redis) : IPermissionClaimsCache
{
    private const string Prefix = "perms:";
    private const string InvalidateChannel = "perms:invalidate";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private static string Key(int userId, string tenantId) => $"{Prefix}{tenantId}:{userId}";
    private static string TenantPattern(string tenantId) => $"{Prefix}{tenantId}:*";

    public async Task<IReadOnlyList<string>?> GetAsync(int userId, string tenantId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(Key(userId, tenantId));
        return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<List<string>>((string)raw!);
    }

    public Task SetAsync(int userId, string tenantId, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        return db.StringSetAsync(Key(userId, tenantId), JsonSerializer.Serialize(permissions), Ttl);
    }

    public async Task InvalidateAsync(int userId, string tenantId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(Key(userId, tenantId));
        // Notifica a otras instancias para que purguen su cache local si la tienen.
        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(InvalidateChannel), $"{tenantId}:{userId}");
    }

    public async Task InvalidateAllForTenantAsync(string tenantId, CancellationToken ct)
    {
        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(pattern: TenantPattern(tenantId)).ToArray();
        if (keys.Length > 0)
            await redis.GetDatabase().KeyDeleteAsync(keys);
        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(InvalidateChannel), $"{tenantId}:*");
    }

    /// <summary>
    /// Invalida por rol publicando una señal global. Los nodos suscritos
    /// purgan su cache; la siguiente lectura repuebla desde BD. No purgamos
    /// claves específicas porque eso requiere conocer qué usuarios tienen el
    /// rol (lo sabe el handler, no el cache).
    /// </summary>
    public async Task InvalidateRoleAsync(int roleId, CancellationToken ct)
    {
        // Estrategia simple Phase 0: invalida toda la cache; los datos
        // se repueblan a demanda. Para una base con muchos tenants se puede
        // afinar publicando role:{id} y que el subscriber resuelva los users.
        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{Prefix}*").ToArray();
        if (keys.Length > 0)
            await redis.GetDatabase().KeyDeleteAsync(keys);
        await redis.GetSubscriber().PublishAsync(
            RedisChannel.Literal(InvalidateChannel), $"role:{roleId}");
    }
}
