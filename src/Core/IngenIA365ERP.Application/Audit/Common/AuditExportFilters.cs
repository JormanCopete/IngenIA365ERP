namespace IngenIA365ERP.Application.Audit.Common;

/// <summary>
/// Filtros canónicos para los exporters del audit log (CSV — T088, PDF — T089).
/// El <see cref="TenantId"/> SIEMPRE viene resuelto por el handler desde el
/// usuario actual; ninguna superficie del cliente debe poder fijarlo
/// directamente (FR-004 — aislamiento por cooperativa).
/// </summary>
public sealed record AuditExportFilters(
    string TenantId,
    string? UserId,
    string? EntityType,
    string? Module,
    string? Action,
    DateTime? From,
    DateTime? To);
