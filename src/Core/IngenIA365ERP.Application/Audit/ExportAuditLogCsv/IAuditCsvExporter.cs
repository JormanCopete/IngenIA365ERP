using IngenIA365ERP.Application.Audit.Common;

namespace IngenIA365ERP.Application.Audit.ExportAuditLogCsv;

/// <summary>
/// Contrato de exportación a CSV del audit log. Implementado por
/// <c>IngenIA365ERP.Audit.Services.AuditCsvExporter</c> (CsvHelper).
///
/// El contrato vive en Application para que el handler no se acople al
/// driver Mongo ni a CsvHelper — Principio II (Clean Architecture).
/// </summary>
public interface IAuditCsvExporter
{
    Task<AuditCsvExport> ExportAsync(AuditExportFilters filters, CancellationToken ct);
}
