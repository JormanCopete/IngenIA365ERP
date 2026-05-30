namespace IngenIA365ERP.Application.Audit.Common;

/// <summary>
/// Datos de la portada del PDF de audit log. El handler resuelve estos
/// campos vía DB (entidad <c>Tenant</c>) y los pasa al exporter — así el
/// exporter de Infrastructure no consulta el DbContext directamente
/// (Principio II).
/// </summary>
public sealed record AuditPdfHeader(
    string TenantId,
    string TenantName,
    string Nit,
    string LegalName,
    DateTime GeneratedAt,
    string GeneratedByUserName,
    DateTime? From,
    DateTime? To);
