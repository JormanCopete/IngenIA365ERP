using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Integration;

/// <summary>
/// El consecutivo de los lotes de integración (<c>COR_IntegrationBatchCounters</c>; feature 012, T12, T477; data-model §19).
/// Fila única, creada en el primer uso, con <c>RowVersion</c>: si dos órdenes toman número a la vez, una pierde y relee.
/// </summary>
[SinDiffDeAuditoria]
public class IntegrationBatchCounter : AuditableEntity
{
    /// <summary>El número que lleva el próximo lote.</summary>
    public long NextValue { get; private set; } = 1;

    /// <summary>Devuelve el número del lote que se crea y avanza el contador.</summary>
    public long TomarSiguiente() => NextValue++;
}
