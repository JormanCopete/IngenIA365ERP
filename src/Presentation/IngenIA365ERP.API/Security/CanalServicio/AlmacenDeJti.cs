using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace IngenIA365ERP.API.Security.CanalServicio;

/// <summary>
/// Feature 021 (research R2): el <c>jti</c> de un token de servicio vale una sola vez. Se marca en Redis base 0 —lo que no es de
/// ninguna cooperativa vive ahí (Principio IV)— con <c>SET NX</c> y una vida un poco mayor que la del token; un segundo uso del
/// mismo <c>jti</c> mientras dure la marca se rechaza.
/// </summary>
public interface IAlmacenDeJti
{
    /// <summary>Marca el <c>jti</c>; devuelve <c>false</c> si ya estaba (token repetido).</summary>
    Task<bool> RegistrarAsync(string emisor, string jti, CancellationToken ct = default);
}

/// <inheritdoc />
internal sealed class AlmacenDeJti(IServiceProvider servicios, IMemoryCache memoria, ILogger<AlmacenDeJti> logger) : IAlmacenDeJti
{
    private static readonly TimeSpan Vida = TimeSpan.FromSeconds(120);

    public async Task<bool> RegistrarAsync(string emisor, string jti, CancellationToken ct = default)
    {
        var clave = $"canal:jti:{emisor}:{jti}";
        var redis = servicios.GetService<IConnectionMultiplexer>();
        if (redis is not null)
        {
            return await redis.GetDatabase(0).StringSetAsync(clave, "1", Vida, When.NotExists);
        }

        // Sin Redis (sólo en pruebas o en un arranque sin caché) la marca vive en memoria del proceso: con dos réplicas un
        // token podría usarse una vez en cada una, por eso se avisa en el log.
        logger.LogWarning("[Canal.JtiEnMemoria] No hay Redis registrado; el jti del canal se marca sólo en este proceso.");
        lock (memoria)
        {
            if (memoria.TryGetValue(clave, out _)) return false;
            memoria.Set(clave, true, Vida);
            return true;
        }
    }
}
