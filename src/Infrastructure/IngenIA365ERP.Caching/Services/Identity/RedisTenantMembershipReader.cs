using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Implementación combinada cache + DB de <see cref="ITenantMembershipReader"/>
/// (T038, research D-07). Estrategia "cache-aside con TTL corto":
/// <list type="number">
///   <item>Lee de Redis (key <c>memberships:{guid}</c>, TTL 60s).</item>
///   <item>Cache miss → query a <c>ADM_TenantMemberships</c> + <c>ADM_Tenants</c>
///         + <c>ADM_TenantMfaPolicies</c> (JOIN único).</item>
///   <item>Popula cache para próximas lecturas.</item>
/// </list>
///
/// La invalidación distribuida la maneja <see cref="RedisMembershipChangedNotifier"/>
/// publicando en canal <c>membership-changed</c>; este reader se suscribe en
/// <see cref="StartSubscriptionAsync"/> (llamado desde <c>AddCachingServices</c>).
///
/// <para>El TTL 60s es la última defensa: si el pub/sub falla por cualquier razón,
/// el dato obsoleto se purga en máximo 60s.</para>
/// </summary>
internal sealed class RedisTenantMembershipReader : ITenantMembershipReader
{
    public const string KeyPrefix = "memberships:";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IConnectionMultiplexer _redis;
    private readonly IAdminDbContext _adminDb;

    public RedisTenantMembershipReader(IConnectionMultiplexer redis, IAdminDbContext adminDb)
    {
        _redis = redis;
        _adminDb = adminDb;
    }

    public async Task<IReadOnlyList<ActiveMembershipInfo>> GetActiveMembershipsAsync(
        Guid centralUserId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{centralUserId:N}";

        var cached = await db.StringGetAsync(key);
        if (cached.HasValue)
        {
            var fromCache = JsonSerializer.Deserialize<List<ActiveMembershipInfo>>(cached.ToString());
            if (fromCache is not null) return fromCache;
        }

        // Cache miss — JOIN: memberships + tenants + mfa policies.
        var fresh = await (
            from m in _adminDb.TenantMemberships
            where m.CentralUserId == centralUserId && m.Status == MembershipStatus.Active
            join t in _adminDb.Tenants on m.TenantId equals t.PublicId
            join p in _adminDb.TenantMfaPolicies on m.TenantId equals p.TenantId into pg
            from p in pg.DefaultIfEmpty()
            select new ActiveMembershipInfo(
                m.TenantId,
                t.Name,
                m.IsTenantAdmin,
                p != null && p.IsRequired))
            .ToListAsync(ct);

        var json = JsonSerializer.Serialize(fresh);
        await db.StringSetAsync(key, json, Ttl);

        return fresh;
    }

    public async Task<bool> IsMemberOfTenantAsync(
        Guid centralUserId, Guid tenantId, CancellationToken ct)
    {
        // Usa la cache para no hacer un round-trip distinto por chequeo.
        var memberships = await GetActiveMembershipsAsync(centralUserId, ct);
        return memberships.Any(m => m.TenantId == tenantId);
    }

    public async Task InvalidateLocalCacheAsync(Guid centralUserId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = $"{KeyPrefix}{centralUserId:N}";
        await db.KeyDeleteAsync(key);
    }

    /// <summary>Suscriptor al canal pub/sub de invalidaciones — instalado en
    /// <c>AddCachingServices</c> como singleton, vive con el connection multiplexer.</summary>
    public static Task StartSubscriptionAsync(IConnectionMultiplexer redis)
    {
        var subscriber = redis.GetSubscriber();
        return subscriber.SubscribeAsync(
            RedisChannel.Literal(RedisMembershipChangedNotifier.Channel),
            (_, value) =>
            {
                var payload = value.ToString();
                if (!payload.StartsWith("central:", StringComparison.Ordinal)) return;

                var guidPart = payload["central:".Length..];
                if (!Guid.TryParseExact(guidPart, "N", out var centralUserId)) return;

                var db = redis.GetDatabase();
                db.KeyDelete($"{KeyPrefix}{centralUserId:N}");
            });
    }
}
