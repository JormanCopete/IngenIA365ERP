using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Audit;

/// <summary>
/// La cabeza de cada cadena de auditoría (<c>COR_AuditChainHeads</c>; feature 012, T38; data-model §23).
/// Se actualiza con <c>RowVersion</c> en la misma transacción que las filas que sella: si dos réplicas
/// coinciden, la segunda choca y vuelve a leer la cabeza, así que la cadena no se bifurca.
/// </summary>
[SinDiffDeAuditoria]
public class AuditChainHead : AuditableEntity
{
    /// <summary>Flujo: <c>{tenantPublicId:N}:10y</c>. Único.</summary>
    public string Stream { get; set; } = string.Empty;

    /// <summary>Último <c>Seq</c> sellado; 0 recién activada.</summary>
    public long LastSeq { get; set; }

    /// <summary>Hash del último evento sellado; el hash inicial recién activada.</summary>
    public string LastHash { get; set; } = string.Empty;
}
