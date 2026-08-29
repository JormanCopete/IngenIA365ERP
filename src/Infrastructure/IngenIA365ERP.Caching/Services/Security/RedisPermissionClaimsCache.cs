using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Security;

internal sealed class RedisPermissionClaimsCache(
    IConnectionMultiplexer redis,
    ITenantCacheSlots ranuras,
    ICurrentTenantService cooperativaActual) : IPermissionClaimsCache
{
    private const string Prefix = "perms:";
    private const string InvalidateChannel = "perms:invalidate";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private static string Key(int userId, string tenantId) => $"{Prefix}{tenantId}:{userId}";
    private static string TenantPattern(string tenantId) => $"{Prefix}{tenantId}:*";

    public async Task<IReadOnlyList<string>?> GetAsync(int userId, string tenantId, CancellationToken ct)
    {
        var db = redis.GetDatabase(await ranuras.RanuraDeAsync(tenantId, ct));
        var raw = await db.StringGetAsync(Key(userId, tenantId));
        return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<List<string>>((string)raw!);
    }

    public async Task SetAsync(
        int userId, string tenantId, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        var db = redis.GetDatabase(await ranuras.RanuraDeAsync(tenantId, ct));
        await db.StringSetAsync(Key(userId, tenantId), JsonSerializer.Serialize(permissions), Ttl);
    }

    public async Task InvalidateAsync(int userId, string tenantId, CancellationToken ct)
    {
        var db = redis.GetDatabase(await ranuras.RanuraDeAsync(tenantId, ct));
        await db.KeyDeleteAsync(Key(userId, tenantId));
        // Notifica a otras instancias para que purguen su cache local si la tienen.
        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(InvalidateChannel), $"{tenantId}:{userId}");
    }

    public async Task InvalidateAllForTenantAsync(string tenantId, CancellationToken ct)
    {
        // El argumento `database` NO es opcional en la practica. Sin el, el barrido
        // mira la base por defecto de la conexion —la 0— y con las claves viviendo
        // en la base de su cooperativa encontraria cero: la invalidacion seria un
        // no-op mudo, y los permisos revocados se seguirian sirviendo media hora.
        var ranura = await ranuras.RanuraDeAsync(tenantId, ct);
        await BorrarAsync(ranura, TenantPattern(tenantId));

        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal(InvalidateChannel), $"{tenantId}:*");
    }

    /// <summary>
    /// Borra por patron dentro de UNA base logica. El canal de anuncio sigue siendo
    /// global: el pub/sub de Redis no esta acotado por base, asi que separar las
    /// claves no separa las notificaciones.
    /// </summary>
    private async Task BorrarAsync(int ranura, string patron)
    {
        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(database: ranura, pattern: patron).ToArray();
        if (keys.Length > 0)
        {
            await redis.GetDatabase(ranura).KeyDeleteAsync(keys);
        }
    }

    /// <summary>
    /// Invalida el caché de permisos tras cambiar un rol.
    ///
    /// <para>
    /// Antes barría <c>perms:*</c> entero, o sea el caché de TODAS las
    /// cooperativas por un cambio en una. Ahora se acota a la cooperativa en
    /// curso, que es de donde es el rol: quien llama a esto —crear, actualizar o
    /// borrar un rol— siempre opera dentro de una. Es a la vez correcto y menos
    /// destructivo.
    /// </para>
    ///
    /// <para>
    /// No se purgan claves de usuarios concretos porque saber quién tiene el rol
    /// es cosa del handler, no del caché.
    /// </para>
    /// </summary>
    public async Task InvalidateRoleAsync(int roleId, CancellationToken ct)
    {
        var tenantId = cooperativaActual.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await BorrarAsync(await ranuras.RanuraDeAsync(tenantId, ct), TenantPattern(tenantId));
        }

        await redis.GetSubscriber().PublishAsync(
            RedisChannel.Literal(InvalidateChannel), $"role:{roleId}");
    }
}
