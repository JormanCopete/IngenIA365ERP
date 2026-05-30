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
    IReadOnlyList<string>? ChangedFields);
