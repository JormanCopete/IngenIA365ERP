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
    /// <summary>
    /// El <c>v2</c> no es decorativo. <see cref="ActiveMembershipInfo"/> se
    /// serializa entero bajo esta clave, y al añadirle los métodos aceptados las
    /// entradas ya calientes —escritas por el binario anterior— no traen ese campo.
    /// Sin subir el prefijo, durante los 60 segundos siguientes al despliegue cada
    /// cooperativa se leería con el valor por defecto del campo ausente,
    /// simultáneamente en las siete puertas. Y se cura solo, que es lo peor que le
    /// puede pasar a un síntoma: para cuando alguien mira, ya no está.
    ///
    /// <para>
    /// Va por <c>v3</c> desde que el record lleva además la recuperación por
    /// correo. Se sube el prefijo en CADA campo que se le añade, sin excepción: el
    /// coste es un minuto de lecturas frías y la alternativa es un fallo que se
    /// cura solo.
    /// </para>
    /// </summary>
    public const string KeyPrefix = "memberships:v3:";
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
                p != null && p.IsRequired,
                // Sin fila de política no hay restricción: acepta todo. Es el mismo
                // criterio que la línea de arriba usa para IsRequired, y tiene que
                // seguir siéndolo — «no hay política» no puede significar «exige» en
                // una columna y «no acepta nada» en la otra.
                p != null ? p.AllowedMethodsMask : ConversionDeMetodosMfa.Todos,
                // Sin fila de política, la recuperación por correo está APAGADA.
                // Es el criterio opuesto al de los métodos, y a propósito: allí «no
                // hay política» significa «no restringe», y aquí significaría
                // «habilita una vía que nadie encendió».
                p != null && p.AllowEmailRecovery,
                p != null ? p.EmailRecoveryDelayHours : TenantMfaPolicy.DemoraPorDefectoEnHoras))
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
