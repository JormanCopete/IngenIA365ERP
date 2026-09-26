namespace IngenIA365ERP.Application.Audit.VerifyIntegrity;

/// <summary>
/// Lo que <see cref="VerifyAuditIntegrityQuery"/> necesita de Mongo y del sello (feature 012, T38; T066). Lo
/// implementa <c>Infrastructure/Audit/Integrity/LectorDeCadenaDeAuditoria</c>, el único que conoce el
/// documento de Mongo y <c>SelloDeIntegridad</c>: la consulta compara, el lector recalcula.
/// </summary>
public interface ILectorDeCadenaDeAuditoria
{
    /// <summary>
    /// Los eventos del flujo con <c>chain.seq</c> entre <paramref name="desdeSeq"/> y <paramref name="hastaSeq"/>,
    /// ordenados por <c>seq</c>, cada uno con su hash recalculado. Un <c>seq</c> puede aparecer más de una vez
    /// (intercalado) o faltar (eliminado): se devuelve lo que hay, sin juzgarlo.
    /// </summary>
    Task<IReadOnlyList<EventoDeCadena>> LeerAsync(string tenantId, string stream, long desdeSeq, long hastaSeq, CancellationToken ct);

    /// <summary>Si el HMAC de un ancla corresponde a su contenido con la clave de su versión.</summary>
    bool AnclaValida(string stream, long seq, string hash, DateTime anchoredAt, string hmac, string keyVersion);
}

/// <summary>Un evento de la cadena tal como está en Mongo.</summary>
/// <param name="Seq"><c>chain.seq</c>.</param>
/// <param name="EventId">El <c>_id</c>.</param>
/// <param name="OccurredAt"><c>occurredAt</c>, si se puede leer.</param>
/// <param name="PrevHash"><c>chain.prevHash</c> guardado.</param>
/// <param name="Hash"><c>chain.hash</c> guardado.</param>
/// <param name="HashRecalculado"><c>SHA-256(chain.prevHash ‖ canónico)</c> del documento tal como está.</param>
public sealed record EventoDeCadena(long Seq, string EventId, DateTime? OccurredAt, string? PrevHash, string? Hash, string HashRecalculado);
