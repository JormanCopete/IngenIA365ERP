using System.Globalization;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Audit.ExportAuditLogPdf;
using IngenIA365ERP.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.Audit.Services;

/// <summary>
/// T089 — Render del audit log a PDF firmado (QuestPDF + HMAC-SHA256).
///
/// <para>
/// <b>Cursor paginado con tope</b>: itera <see cref="IAuditService.QueryAsync"/>
/// en páginas de <see cref="PageSize"/> filas y trunca en <see cref="MaxRows"/>.
/// PDF es mucho más pesado que CSV, por eso el tope es ~50k — para rangos
/// más grandes el cliente debe usar CSV.
/// </para>
///
/// <para>
/// <b>Firma</b>: tras renderizar, calcula HMAC-SHA256 sobre los bytes del
/// PDF con la clave activa via <see cref="IAuditSignatureService"/>. El
/// HMAC y la <c>KeyVersion</c> viajan en headers HTTP — el verificador
/// SaaS (T090) los reusa para validar integridad.
/// </para>
/// </summary>
public sealed class AuditPdfExporter : IAuditPdfExporter
{
    private const int PageSize = 1_000;
    private const long MaxRows = 50_000;

    private readonly IAuditService _audit;
    private readonly IAuditSignatureService _signatures;

    public AuditPdfExporter(IAuditService audit, IAuditSignatureService signatures)
    {
        _audit = audit;
        _signatures = signatures;
    }

    public async Task<AuditPdfExport> ExportAsync(
        AuditExportFilters filters, AuditPdfHeader header, CancellationToken ct)
    {
        var entries = await FetchEntriesAsync(filters, ct);
        var truncated = entries.Count >= MaxRows;

        var pdfBytes = Document.Create(doc => BuildDocument(doc, header, entries, truncated))
            .GeneratePdf();

        var hmac = _signatures.ComputeHmacBase64(pdfBytes);
        var fileName = BuildFileName(filters);

        return new AuditPdfExport(
            Content: pdfBytes,
            FileName: fileName,
            RowCount: entries.Count,
            HmacBase64: hmac,
            KeyVersion: _signatures.CurrentKeyVersion);
    }

    private async Task<List<AuditLogEntry>> FetchEntriesAsync(
        AuditExportFilters filters, CancellationToken ct)
    {
        var entries = new List<AuditLogEntry>();
        var page = 1;

        while (entries.Count < MaxRows)
        {
            ct.ThrowIfCancellationRequested();

            var paged = await _audit.QueryAsync(new AuditQueryParameters
            {
                TenantId = filters.TenantId,
                UserId = filters.UserId,
                EntityType = filters.EntityType,
                Module = filters.Module,
                Action = filters.Action,
                From = filters.From,
                To = filters.To,
                PageNumber = page,
                PageSize = PageSize
            }, ct);

            if (paged.Items.Count == 0) break;

            var remaining = (int)(MaxRows - entries.Count);
            entries.AddRange(paged.Items.Take(remaining));

            if (paged.Items.Count < PageSize) break;
            page++;
        }

        return entries;
    }

    private static void BuildDocument(
        IDocumentContainer doc,
        AuditPdfHeader header,
        List<AuditLogEntry> entries,
        bool truncated)
    {
        doc.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(9));

            page.Header().Element(c => RenderHeader(c, header));

            page.Content().Element(c => RenderBody(c, entries, truncated));

            page.Footer().AlignCenter().Text(footer =>
            {
                footer.DefaultTextStyle(t => t.FontSize(8).FontColor(Colors.Grey.Medium));
                footer.Span("IngenIA365ERP — Audit Log — Página ");
                footer.CurrentPageNumber();
                footer.Span(" de ");
                footer.TotalPages();
            });
        });
    }

    private static void RenderHeader(IContainer container, AuditPdfHeader header)
    {
        container.Column(col =>
        {
            col.Item().Text(header.LegalName).Bold().FontSize(13);
            col.Item().Text($"NIT: {header.Nit}");
            col.Item().Text($"Reporte: Registro de auditoría").FontSize(11).Bold();

            var rangeText = header is { From: not null, To: not null }
                ? $"Rango: {FormatDate(header.From)} → {FormatDate(header.To)}"
                : "Rango: completo";
            col.Item().Text(rangeText).FontColor(Colors.Grey.Darken2);

            col.Item().Text(
                $"Generado por {header.GeneratedByUserName} " +
                $"el {header.GeneratedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}")
                .FontColor(Colors.Grey.Darken2);

            col.Item().PaddingTop(6).LineHorizontal(0.5f);
        });
    }

    private static void RenderBody(
        IContainer container, List<AuditLogEntry> entries, bool truncated)
    {
        container.Column(col =>
        {
            col.Item().PaddingTop(6).Text($"Total de eventos: {entries.Count:N0}").Bold();

            if (truncated)
            {
                col.Item().Text(
                    $"⚠ Resultado truncado a {MaxRows:N0} filas. Use export CSV para rangos mayores.")
                    .FontColor(Colors.Red.Darken2).Bold();
            }

            col.Item().PaddingTop(8).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(2); // Timestamp
                    c.RelativeColumn(2); // User
                    c.RelativeColumn(2); // Module
                    c.RelativeColumn(2); // Action
                    c.RelativeColumn(2); // EntityType
                    c.RelativeColumn(2); // EntityId
                });

                table.Header(h =>
                {
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Fecha (UTC)").Bold().FontSize(9);
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Usuario").Bold().FontSize(9);
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Módulo").Bold().FontSize(9);
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Acción").Bold().FontSize(9);
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Entidad").Bold().FontSize(9);
                    h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Id").Bold().FontSize(9);
                });

                foreach (var e in entries)
                {
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                        .Text(e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).FontSize(8);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                        .Text(e.UserName ?? string.Empty).FontSize(8);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                        .Text(e.Module ?? string.Empty).FontSize(8);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                        .Text(e.Action).FontSize(8);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                        .Text(e.EntityType).FontSize(8);
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3)
                        .Text(e.EntityId ?? string.Empty).FontSize(8);
                }
            });
        });
    }

    private static string FormatDate(DateTime? d) =>
        d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";

    private static string BuildFileName(AuditExportFilters filters)
    {
        var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"audit-{filters.TenantId}-{ts}.pdf";
    }
}
