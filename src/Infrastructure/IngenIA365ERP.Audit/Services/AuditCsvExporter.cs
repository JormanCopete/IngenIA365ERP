using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Audit.ExportAuditLogCsv;
using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.Audit.Services;

/// <summary>
/// T088 — Implementación de <see cref="IAuditCsvExporter"/> usando CsvHelper.
///
/// <para>
/// <b>Cursor paginado vs streaming real</b>: para minimizar memoria con
/// datasets grandes (millones de eventos), itera el audit log en páginas de
/// <see cref="PageSize"/> filas y escribe directamente al <see cref="CsvWriter"/>
/// (no acumula en lista intermedia). El resultado se materializa en
/// <c>byte[]</c> porque el handler lo expone así por contrato — la
/// "streaming real" pipeada a <c>HttpResponse</c> se aterriza cuando el
/// módulo Carter (T091) maneje la respuesta directamente.
/// </para>
///
/// <para>
/// <b>Safety limit</b>: <see cref="MaxRows"/> evita exports patológicos que
/// agotarían memoria. Si se alcanza, el export se trunca y el row count
/// devuelto refleja lo que SÍ se exportó — el cliente debe acotar el rango.
/// </para>
///
/// <para>
/// Encoding: UTF-8 con BOM para que Excel detecte caracteres acentuados
/// (NIT, nombres en español) sin necesidad de "Importar de texto".
/// </para>
/// </summary>
public sealed class AuditCsvExporter : IAuditCsvExporter
{
    private const int PageSize = 1_000;
    private const long MaxRows = 1_000_000;

    private static readonly CsvConfiguration CsvConfig =
        new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        };

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    private readonly IAuditService _audit;

    public AuditCsvExporter(IAuditService audit) => _audit = audit;

    public async Task<AuditCsvExport> ExportAsync(AuditExportFilters filters, CancellationToken ct)
    {
        await using var stream = new MemoryStream();
        // El StreamWriter / CsvWriter NO se cierran con `await using` aquí
        // porque al disponer cerrarían el MemoryStream — necesitamos los
        // bytes después del Flush.
        var writer = new StreamWriter(stream, Utf8WithBom, leaveOpen: true);
        var csv = new CsvWriter(writer, CsvConfig, leaveOpen: true);

        csv.WriteHeader<AuditCsvRow>();
        await csv.NextRecordAsync();

        long total = 0;
        var page = 1;
        var truncated = false;

        while (!truncated)
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

            foreach (var entry in paged.Items)
            {
                if (total >= MaxRows) { truncated = true; break; }
                csv.WriteRecord(MapToRow(entry));
                await csv.NextRecordAsync();
                total++;
            }

            if (paged.Items.Count < PageSize) break;
            page++;
        }

        await csv.FlushAsync();
        await writer.FlushAsync();
        csv.Dispose();
        await writer.DisposeAsync();

        var fileName = BuildFileName(filters);
        return new AuditCsvExport(stream.ToArray(), fileName, total);
    }

    private static string BuildFileName(AuditExportFilters filters)
    {
        var ts = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"audit-{filters.TenantId}-{ts}.csv";
    }

    private static AuditCsvRow MapToRow(AuditLogEntry e) => new(
        Id: e.Id,
        Timestamp: e.Timestamp.ToString("O", CultureInfo.InvariantCulture),
        TenantId: e.TenantId ?? string.Empty,
        UserId: e.UserId ?? string.Empty,
        UserName: e.UserName ?? string.Empty,
        Module: e.Module ?? string.Empty,
        Action: e.Action,
        EntityType: e.EntityType,
        EntityId: e.EntityId ?? string.Empty,
        IpAddress: e.IpAddress ?? string.Empty,
        Endpoint: e.Endpoint ?? string.Empty,
        DurationMs: e.DurationMs,
        ChangedFields: e.ChangedFields is null ? string.Empty : string.Join("|", e.ChangedFields),
        OldValuesJson: e.OldValues ?? string.Empty,
        NewValuesJson: e.NewValues ?? string.Empty);

    /// <summary>
    /// Fila plana para CSV. El orden de las properties define el orden de
    /// columnas — CsvHelper respeta la declaración del record.
    /// </summary>
    private sealed record AuditCsvRow(
        string Id,
        string Timestamp,
        string TenantId,
        string UserId,
        string UserName,
        string Module,
        string Action,
        string EntityType,
        string EntityId,
        string IpAddress,
        string Endpoint,
        long DurationMs,
        string ChangedFields,
        string OldValuesJson,
        string NewValuesJson);
}
