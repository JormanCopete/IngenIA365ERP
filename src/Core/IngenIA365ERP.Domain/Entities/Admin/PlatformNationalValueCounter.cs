using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// <c>ADM_NationalValueCounters</c> (feature 013, C44; data-model parte 3 §3.3): la numeración común de
/// <c>ADM_NationalValues</c> y <c>ADM_RegulatoryDocuments</c>. Fila única, creada en el primer uso, con <c>RowVersion</c>:
/// el comando que registra toma el número y avanza el contador en el <b>mismo</b> guardado que inserta la fila, así el
/// orden de confirmación es el de la numeración y la réplica («todo lo de <c>Sequence</c> mayor que la última local») no
/// se salta una fila que confirmó tarde. Si dos registros toman número a la vez, uno pierde por concurrencia y relee
/// (molde de <c>COR_IntegrationBatchCounters</c>; no hay secuencias de base de datos en el código).
/// </summary>
[SinDiffDeAuditoria]
public class PlatformNationalValueCounter : AuditableEntity
{
    /// <summary>La <c>Sequence</c> que lleva la próxima fila.</summary>
    public long NextValue { get; private set; } = 1;

    /// <summary>Devuelve la <c>Sequence</c> de la fila que se registra y avanza el contador.</summary>
    public long TomarSiguiente() => NextValue++;
}
