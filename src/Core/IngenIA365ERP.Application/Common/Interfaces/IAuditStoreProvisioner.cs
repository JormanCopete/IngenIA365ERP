namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Deja lista la base de auditoría de una cooperativa: la crea si falta y le
/// garantiza sus índices y el TTL de retención.
///
/// <para>
/// Hace falta en el aprovisionamiento y no sólo al arrancar. MongoDB crea una
/// base al escribir el primer documento, sin avisar y sin índices: una
/// cooperativa nueva escribiría su rastro correctamente y ese rastro <b>no se
/// purgaría nunca</b>, porque el TTL no existiría hasta el siguiente reinicio
/// del servicio. No da error en ningún momento — el incumplimiento aparece cinco
/// años después.
/// </para>
/// </summary>
public interface IAuditStoreProvisioner
{
    /// <param name="tenantId">Identificador público de la cooperativa, o null para la base global.</param>
    /// <returns>Nombre de la base creada o comprobada.</returns>
    Task<string> AprovisionarAsync(string? tenantId, CancellationToken ct);
}
