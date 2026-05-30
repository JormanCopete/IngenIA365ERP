using IngenIA365ERP.Application.Audit.Common;

namespace IngenIA365ERP.Application.Audit.ExportAuditLogPdf;

/// <summary>
/// Contrato del exporter PDF firmado del audit log (T089). Implementado en
/// <c>IngenIA365ERP.Audit.Services.AuditPdfExporter</c> con QuestPDF +
/// <see cref="IAuditSignatureService"/> para el HMAC.
/// </summary>
public interface IAuditPdfExporter
{
    Task<AuditPdfExport> ExportAsync(
        AuditExportFilters filters,
        AuditPdfHeader header,
        CancellationToken ct);
}
