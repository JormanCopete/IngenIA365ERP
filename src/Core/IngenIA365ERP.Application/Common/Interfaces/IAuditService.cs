using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Interfaces;

public interface IAuditService
{
    // Legacy overload (backward compatible)
    Task LogAsync(string action, string entityType, string entityId,
        object? oldValues, object? newValues, CancellationToken cancellationToken = default);

    // Full audit log entry
    Task LogAsync(AuditLogCommand command, CancellationToken cancellationToken = default);

    // Access log (login/logout/token events)
    Task LogAccessAsync(AccessLogCommand command, CancellationToken cancellationToken = default);

    // Force flush queued entries
    Task FlushAsync();

    // Query audit logs
    Task<PagedList<AuditLogEntry>> QueryAsync(AuditQueryParameters query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetByUserAsync(string userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    // Query access logs
    Task<IReadOnlyList<AccessLogEntry>> GetAccessLogsAsync(string? userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    // Legacy query (backward compatible)
    Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(string entityType, string entityId,
        int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}

// === Commands ===

public record AuditLogCommand
{
    public string Action { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string Module { get; init; } = string.Empty;
    public object? OldValues { get; init; }
    public object? NewValues { get; init; }
    public List<string>? ChangedFields { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Endpoint { get; init; }
    public string? HttpMethod { get; init; }
    public int? HttpStatusCode { get; init; }
    public long DurationMs { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

public record AccessLogCommand
{
    public string Action { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

// === Query parameters ===

public record AuditQueryParameters
{
    public string? TenantId { get; init; }
    public string? UserId { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? Module { get; init; }
    public string? Action { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Feature 012 (T423, FR-007): varios módulos a la vez (los encadenados: <c>Inventory</c>, <c>Approvals</c>,
    /// <c>Navigation</c>…). Se combina con <see cref="Module"/> por intersección; vacío o nulo no filtra.
    /// </summary>
    public IReadOnlyList<string>? Modules { get; init; }

    /// <summary>
    /// Feature 012 (T423): <c>Rejected</c> sólo los rechazos (<c>AuditEventTypes.CommandRejected</c>);
    /// <c>Accepted</c> todo lo demás; nulo, todo.
    /// </summary>
    public string? Outcome { get; init; }
}

// === Result records ===

public record AuditLogEntry(
    string Id,
    string? TenantId,
    string? UserId,
    string? UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string? Module,
    string? OldValues,
    string? NewValues,
    List<string>? ChangedFields,
    string? IpAddress,
    string? Endpoint,
    long DurationMs,
    DateTime Timestamp,
    IReadOnlyDictionary<string, string>? Metadata = null,
    long? ChainSeq = null);

public record AccessLogEntry(
    string Id,
    string? TenantId,
    string? UserId,
    string? UserName,
    string Action,
    string? IpAddress,
    string? UserAgent,
    bool Success,
    string? FailureReason,
    DateTime Timestamp);
