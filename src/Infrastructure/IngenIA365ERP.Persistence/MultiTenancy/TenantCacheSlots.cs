using System.Collections.Concurrent;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Traduce cooperativa a base lógica de Redis, leyendo la ranura que se le
/// asignó al aprovisionarla.
///
/// <para>
/// El resultado se memoriza para siempre, y es correcto: una ranura se asigna
/// una vez y <b>no se recicla nunca</b>. Reciclar el índice de una cooperativa
/// dada de baja haría que la siguiente heredara su caché de permisos, salvo que
/// alguien se acordara de vaciar esa base primero. No merece la pena: las
/// ranuras son baratas y el olvido es caro.
/// </para>
/// </summary>
internal sealed class TenantCacheSlots(
    AdminDbContext admin,
    ILogger<TenantCacheSlots> logger) : ITenantCacheSlots
{
    /// <summary>
    /// Compartido entre peticiones a propósito: el mapa no cambia en la vida del
    /// proceso. Estático porque el servicio es scoped y el DbContext no.
    /// </summary>
    private static readonly ConcurrentDictionary<string, int> Memoria = new();

    public async ValueTask<int> RanuraDeAsync(string? tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return 0;
        if (Memoria.TryGetValue(tenantId, out var recordada)) return recordada;
        if (!int.TryParse(tenantId, out var idInterno)) return 0;

        int? ranura;
        try
        {
            ranura = await admin.Tenants
                .AsNoTracking()
                .Where(t => t.Id == idInterno)
                .Select(t => t.RedisDbIndex)
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            // Sin tumbar la petición: el peor caso de devolver 0 es compartir base
            // con lo global, y la clave ya lleva la cooperativa como prefijo.
            logger.LogWarning(ex,
                "No se pudo leer la ranura de caché de la cooperativa {TenantId}. Se usa la global.",
                tenantId);
            return 0;
        }

        if (ranura is null or 0)
        {
            logger.LogDebug(
                "La cooperativa {TenantId} no tiene ranura de caché asignada. Se usa la global.",
                tenantId);
            return 0;
        }

        Memoria[tenantId] = ranura.Value;
        return ranura.Value;
    }
}
