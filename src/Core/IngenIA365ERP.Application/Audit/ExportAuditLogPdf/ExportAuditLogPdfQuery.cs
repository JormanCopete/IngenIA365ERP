using FluentValidation;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Audit.ExportAuditLogPdf;

/// <summary>
/// T089 — Export firmado a PDF (FR-027 / SC-004). El PDF lleva en su pie de
/// página el HMAC-SHA256 calculado sobre los bytes del cuerpo y la versión
/// de la clave usada. El endpoint SaaS <c>POST /api/saas/audit/verify</c>
/// (T090) recalcula el HMAC para validar integridad post-export.
///
/// <para>Por qué HMAC en lugar de firma asimétrica: el verificador vive en
/// el mismo dominio SaaS — el riesgo es alteración, no repudio. Una clave
/// simétrica derivada via DataProtection es suficiente y mucho más simple.
/// </para>
/// </summary>
public sealed record ExportAuditLogPdfQuery(
    string? UserId,
    string? EntityType,
    string? Module,
    string? Action,
    DateTime? From,
    DateTime? To) : IRequest<Result<AuditPdfExport>>;

/// <summary>
/// PDF + metadata de firma. <paramref name="KeyVersion"/> permite verificar
/// PDFs antiguos tras rotaciones de la clave DataProtection.
/// </summary>
public sealed record AuditPdfExport(
    byte[] Content,
    string FileName,
    long RowCount,
    string HmacBase64,
    string KeyVersion);

public sealed class ExportAuditLogPdfQueryValidator : AbstractValidator<ExportAuditLogPdfQuery>
{
    public ExportAuditLogPdfQueryValidator()
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

public sealed class ExportAuditLogPdfQueryHandler
    : IRequestHandler<ExportAuditLogPdfQuery, Result<AuditPdfExport>>
{
    private readonly IAuditPdfExporter _exporter;
    private readonly IApplicationDbContext _db;
    private readonly IAdminDbContext _admin;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;

    public ExportAuditLogPdfQueryHandler(
        IAuditPdfExporter exporter,
        IApplicationDbContext db,
        IAdminDbContext admin,
        ICurrentUserService currentUser,
        IDateTimeService clock)
    {
        _exporter = exporter;
        _db = db;
        _admin = admin;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<AuditPdfExport>> Handle(
        ExportAuditLogPdfQuery request, CancellationToken ct)
    {
        var tenantIdStr = _currentUser.TenantId;
        if (string.IsNullOrWhiteSpace(tenantIdStr))
        {
            return Result.Failure<AuditPdfExport>(
                "Auth.TenantRequired",
                "El usuario actual no está asociado a una cooperativa.");
        }

        // Resuelve la entidad Tenant para la portada (NIT, razón social).
        // Si TenantId no es un entero válido — caso raro — devolvemos NotFound
        // explícito en lugar de poblar la portada con datos placebo.
        if (!int.TryParse(tenantIdStr, out var tenantInternalId))
        {
            return Result.Failure<AuditPdfExport>(
                "Generic.NotFound",
                "La cooperativa actual no está registrada.");
        }

        // Del registro real, en la base administrativa. Antes salia de la copia
        // que el modelo operativo replicaba dentro de cada cooperativa, y que
        // estaba vacia: este export nunca pudo encontrar su propia cooperativa.
        var tenant = await _admin.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantInternalId, ct);
        if (tenant is null)
        {
            return Result.Failure<AuditPdfExport>(
                "Generic.NotFound",
                "La cooperativa actual no está registrada.");
        }

        var header = new AuditPdfHeader(
            TenantId: tenantIdStr,
            TenantName: tenant.Name,
            Nit: tenant.Nit ?? "—",
            LegalName: tenant.LegalName ?? tenant.Name,
            GeneratedAt: _clock.UtcNow,
            GeneratedByUserName: _currentUser.UserName ?? "system",
            From: request.From,
            To: request.To);

        var filters = new AuditExportFilters(
            TenantId: tenantIdStr,
            UserId: NullIfBlank(request.UserId),
            EntityType: NullIfBlank(request.EntityType),
            Module: NullIfBlank(request.Module),
            Action: NullIfBlank(request.Action),
            From: request.From,
            To: request.To);

        var export = await _exporter.ExportAsync(filters, header, ct);
        return Result.Success(export);
    }

    private static string? NullIfBlank(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v;
}
