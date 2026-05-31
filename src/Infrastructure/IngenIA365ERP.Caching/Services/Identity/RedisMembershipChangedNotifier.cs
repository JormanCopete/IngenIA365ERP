using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Publica invalidaciones de cache de membresías vía Redis pub/sub (T039a, research D-07).
/// Canal: <c>membership-changed</c> (single-cast: cualquier instancia que tenga la cache
/// caliente del centralUserId la limpia al recibir).
///
/// <para>Payload: <c>"central:{guid}"</c> para una persona,
/// <c>"tenant-members:{guid}"</c> para todos los miembros de un tenant.</para>
/// </summary>
internal sealed class RedisMembershipChangedNotifier : IMembershipChangedNotifier
{
    public const string Channel = "membership-changed";

    private readonly IConnectionMultiplexer _redis;
    private readonly IAdminDbContext _adminDb;

    public RedisMembershipChangedNotifier(IConnectionMultiplexer redis, IAdminDbContext adminDb)
    {
        _redis = redis;
        _adminDb = adminDb;
    }

    public Task PublishAsync(Guid centralUserId, CancellationToken ct) =>
        _redis.GetSubscriber().PublishAsync(
            RedisChannel.Literal(Channel),
            $"central:{centralUserId:N}");

    public async Task PublishForTenantMembersAsync(Guid tenantId, CancellationToken ct)
    {
        // Resolver los IDs de los miembros activos del tenant y publicar un mensaje
        // por cada uno. Para tenants grandes (>500 miembros) optimizar a un mensaje
        // único 'tenant:{id}' con suscriptor inteligente — TODO en siguiente iter.
        var memberIds = await _adminDb.TenantMemberships
            .Where(m => m.TenantId == tenantId && m.Status == Domain.Entities.Admin.MembershipStatus.Active)
            .Select(m => m.CentralUserId)
            .ToListAsync(ct);

        var subscriber = _redis.GetSubscriber();
        foreach (var id in memberIds)
        {
            await subscriber.PublishAsync(RedisChannel.Literal(Channel), $"central:{id:N}");
        }
    }
}
