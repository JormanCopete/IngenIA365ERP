using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services;

/// <summary>
/// Cuántas bases lógicas declara el Redis que hay detrás.
///
/// <para>
/// Se pregunta al servidor en vez de leerlo de la configuración de la
/// aplicación. El parámetro <c>databases</c> es inmutable en caliente —
/// <c>CONFIG SET</c> lo rechaza— y se fija al arrancar el proceso de Redis, así
/// que lo que la aplicación cree y lo que el servidor tiene pueden diferir sin
/// que nadie lo note. Descubrirlo al asignar la ranura es barato; descubrirlo
/// cuando una cooperativa ya está registrada y sin caché, no.
/// </para>
/// </summary>
internal sealed class RedisSlotCapacity(
    IConnectionMultiplexer redis,
    ILogger<RedisSlotCapacity> logger) : ICacheSlotCapacity
{
    public ValueTask<int?> RanurasDisponiblesAsync(CancellationToken ct)
    {
        try
        {
            var punto = redis.GetEndPoints().FirstOrDefault();
            if (punto is null) return ValueTask.FromResult<int?>(null);

            return ValueTask.FromResult<int?>(redis.GetServer(punto).DatabaseCount);
        }
        catch (Exception ex)
        {
            // Null y no una excepción: no poder preguntar no debe tumbar el alta de
            // una cooperativa. Quien decide qué hacer con la duda es el asignador.
            logger.LogWarning(ex, "No se pudo consultar cuántas bases lógicas declara Redis.");
            return ValueTask.FromResult<int?>(null);
        }
    }
}
