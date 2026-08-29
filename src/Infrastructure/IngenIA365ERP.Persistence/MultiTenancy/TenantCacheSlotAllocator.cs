using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.MultiTenancy;

/// <summary>
/// Reserva la base lógica de Redis de una cooperativa nueva.
///
/// <para>
/// La ranura 0 no se asigna: queda para las claves que no pertenecen a ninguna
/// cooperativa. Y una ranura entregada <b>no se recicla</b>, aunque la
/// cooperativa se dé de baja — se busca siempre por encima del máximo entregado.
/// Reutilizar el hueco haría que la cooperativa nueva heredara el caché de
/// permisos de la anterior salvo que alguien vaciara esa base antes.
/// </para>
/// </summary>
internal sealed class TenantCacheSlotAllocator(
    AdminDbContext admin,
    ICacheSlotCapacity capacidad,
    ILogger<TenantCacheSlotAllocator> logger) : ITenantCacheSlotAllocator
{
    /// <exception cref="InvalidOperationException">
    /// Cuando ya no quedan ranuras. Se falla diciendo el número exacto en vez de
    /// repartir un índice que colisione: una colisión de caché entre cooperativas
    /// no da error, da los permisos de otra.
    /// </exception>
    public async Task<int> ReservarAsync(string nombreParaElMensaje, CancellationToken ct)
    {
        var entregadas = await admin.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.RedisDbIndex != null)
            .Select(t => t.RedisDbIndex!.Value)
            .ToListAsync(ct);

        var siguiente = entregadas.Count == 0 ? 1 : entregadas.Max() + 1;
        var total = await capacidad.RanurasDisponiblesAsync(ct);

        if (total is not null && siguiente >= total)
        {
            throw new InvalidOperationException(
                $"[Cache.TenantSlotsExhausted] Redis declara {total} bases lógicas y la 0 queda " +
                $"reservada para lo global, así que hay {total - 1} ranuras para cooperativas y ya " +
                $"están entregadas. No se asigna una repetida a «{nombreParaElMensaje}» porque una " +
                "colisión de caché entre cooperativas no daría error, daría los permisos de otra. " +
                "Subí 'databases' al arrancar el servidor de Redis (es inmutable en caliente) o " +
                "dedicá una instancia.");
        }

        if (total is null)
        {
            logger.LogWarning(
                "No se pudo confirmar cuántas bases lógicas declara Redis. Se asigna la ranura " +
                "{Ranura} a «{Cooperativa}» sin comprobar el techo.", siguiente, nombreParaElMensaje);
        }

        return siguiente;
    }
}
