namespace IngenIA365ERP.Application.Common.Interfaces.Audit;

/// <summary>
/// Escritor del audit log con semántica append-only (FR-024). Es el único
/// camino válido para producir entradas en la colección
/// <c>audit_events</c>. Por contrato (estructural + rol de MongoDB
/// definido en <c>database/migration/15_Audit_Mongodb_Bootstrap.json</c>),
/// no expone operaciones de mutación.
/// </summary>
public interface IAuditAppendOnlyWriter
{
    Task AppendAsync(AuditEventDocument entry, CancellationToken ct);
}

/// <summary>
/// Documento de auditoría inmutable. Espejo del modelo de MongoDB
/// (<c>AuditLog</c>) expuesto desde Application para que los handlers no
/// dependan directamente del driver. La versión completa del modelo se
/// formaliza en T086 (US3); por ahora basta con lo mínimo para Phase 2.
/// </summary>
public sealed record AuditEventDocument(
    string TenantId,
    string UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityPublicId,
    string? Module,
    string? OldValuesJson,
    string? NewValuesJson,
    IReadOnlyList<string>? ChangedFields,
    string? IpAddress,
    string? UserAgent,
    string? Endpoint,
    string? HttpMethod,
    int? HttpStatusCode,
    long? DurationMs,
    DateTime OccurredAt)
{
    /// <summary>
    /// Feature 012 (T36): canal, origen, tipo de actor, clave de operación, motivo y código de error.
    /// Opcional: los eventos de identidad no la llevan.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
