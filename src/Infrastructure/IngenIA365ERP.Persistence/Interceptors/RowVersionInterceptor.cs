using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Persistence.Interceptors;

/// <summary>
/// Observa los <c>DbUpdateConcurrencyException</c> en el pipeline de
/// <c>SaveChangesAsync</c> y los registra en el log con suficiente contexto
/// (tipo de entidad y claves) para diagnóstico. La traducción real a
/// <c>IngenIA365ERP.Domain.Exceptions.ConcurrencyConflictException</c> ocurre
/// en el override de <c>ApplicationDbContext.SaveChangesAsync</c>, ya que
/// EF Core no permite transformar excepciones desde un interceptor.
/// </summary>
public sealed class RowVersionInterceptor(ILogger<RowVersionInterceptor> logger) : SaveChangesInterceptor
{
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Exception is DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                logger.LogWarning(
                    "Conflicto de concurrencia optimista en {EntityType} (estado {State}); RowVersion obsoleto.",
                    entry.Entity.GetType().Name,
                    entry.State);
            }
        }
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}
