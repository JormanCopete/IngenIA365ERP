using FluentValidation;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Audit.ExportAuditLogCsv;

/// <summary>
/// T088 — Export streaming a CSV (FR-026). El handler devuelve el stream
/// completo materializado en memoria; en producción el endpoint debe
/// pipearlo a la respuesta HTTP con <c>chunked transfer-encoding</c> para
/// evitar OOM con tenants grandes (1M+ eventos/año).
///
/// A diferencia del query interactivo, el export NO aplica el límite de
/// 6 meses (es la salida de escape para auditorías largas).
/// </summary>
public sealed record ExportAuditLogCsvQuery(
    string? UserId,
    string? EntityType,
    string? Module,
    string? Action,
    DateTime? From,
    DateTime? To) : IRequest<Result<AuditCsvExport>>;

/// <summary>Bytes del CSV + nombre de archivo sugerido + total exportado.</summary>
public sealed record AuditCsvExport(
    byte[] Content,
    string FileName,
    long RowCount);

public sealed class ExportAuditLogCsvQueryValidator : AbstractValidator<ExportAuditLogCsvQuery>
{
    public ExportAuditLogCsvQueryValidator()
    {
        RuleFor(x => x).Custom((q, ctx) =>
        {
            if (q.From is not null && q.To is not null && q.To.Value < q.From.Value)
            {
                ctx.AddFailure(nameof(q.To), "El rango 'To' debe ser posterior a 'From'.");
            }
        });
    }
}

public sealed class ExportAuditLogCsvQueryHandler
    : IRequestHandler<ExportAuditLogCsvQuery, Result<AuditCsvExport>>
{
    private readonly IAuditCsvExporter _exporter;
    private readonly ICurrentUserService _currentUser;

    public ExportAuditLogCsvQueryHandler(
        IAuditCsvExporter exporter, ICurrentUserService currentUser)
    {
        _exporter = exporter;
        _currentUser = currentUser;
    }

    public async Task<Result<AuditCsvExport>> Handle(
        ExportAuditLogCsvQuery request, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<AuditCsvExport>(
                "Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        var filters = new AuditExportFilters(
            TenantId: tenantId,
            UserId: NullIfBlank(request.UserId),
            EntityType: NullIfBlank(request.EntityType),
            Module: NullIfBlank(request.Module),
            Action: NullIfBlank(request.Action),
            From: request.From,
            To: request.To);

        var export = await _exporter.ExportAsync(filters, ct);
        return Result.Success(export);
    }

    private static string? NullIfBlank(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v;
}
