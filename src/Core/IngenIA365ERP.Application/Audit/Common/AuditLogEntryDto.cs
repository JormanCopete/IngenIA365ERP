namespace IngenIA365ERP.Application.Audit.Common;

/// <summary>
/// Proyección de un evento de audit-log hacia el cliente (Blazor + REST).
/// <para>
/// <b>Diferencia con <see cref="IngenIA365ERP.Application.Common.Interfaces.AuditLogEntry"/></b>:
/// éste es el DTO público del módulo US3. El otro es un record interno del
/// servicio (firma legacy del writer/lectores actuales). Se mantienen separados
/// para que cambios en el contrato público no obliguen a tocar el writer.
/// </para>
/// <para>
/// El <c>Id</c> proviene del <c>ObjectId</c> de MongoDB y es opaco para el
/// cliente (no enumerable en sentido secuencial — Principio VI no aplica
/// porque no es entero auto-incremental). Se devuelve como string para que
/// el front pueda usarlo como key en grids/exports.
/// </para>
/// <para>
/// Feature 012 (T423, FR-007): lo que la cadena agrega a cada evento —canal (<c>web</c>, <c>app</c>, <c>pos</c>,
/// <c>process</c>), <c>ActorKind</c> (persona o proceso), origen, motivo, resultado (<c>Rejected</c> con su
/// <c>ErrorCode</c>), clave de idempotencia y posición <c>seq</c> en la cadena de sellos—, leído de la metadata del
/// documento. Nulos en los eventos que no lo llevan.
/// </para>
/// </summary>
public sealed record AuditLogEntryDto(
    string Id,
    string TenantId,
    string UserId,
    string UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string Module,
    string? IpAddress,
    string? Endpoint,
    string? HttpMethod,
    int? HttpStatusCode,
    long DurationMs,
    DateTime Timestamp,
    string? OldValuesJson,
    string? NewValuesJson,
    IReadOnlyList<string>? ChangedFields,
    string? Channel = null,
    string? ActorKind = null,
    string? Origin = null,
    string? Reason = null,
    string Result = "Accepted",
    string? ErrorCode = null,
    string? OperationKey = null,
    long? ChainSeq = null);
