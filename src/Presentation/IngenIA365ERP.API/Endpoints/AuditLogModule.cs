using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Audit.ExportAuditLogCsv;
using IngenIA365ERP.Application.Audit.ExportAuditLogPdf;
using IngenIA365ERP.Application.Audit.QueryAuditLog;
using IngenIA365ERP.Application.Audit.RegisterOptionAccess;
using IngenIA365ERP.Application.Audit.VerifyIntegrity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IngenIA365ERP.API.Endpoints;

/// <summary>
/// T091 — Consola y export del audit log (FR-024 → FR-027).
///
/// <para>3 endpoints bajo <c>/api/audit/logs</c>:</para>
/// <list type="bullet">
///   <item><c>GET /</c> — query paginado con filtros (AuditLog.View).</item>
///   <item><c>GET /export.csv</c> — export streaming CSV (AuditLog.Export).</item>
///   <item><c>GET /export.pdf</c> — export PDF firmado HMAC (AuditLog.Export).</item>
/// </list>
///
/// <para>
/// El módulo aplica <see cref="ErrorEnvelopeFilter"/> al GET de query para
/// que un Result.Failure devuelva el envelope canónico. Los exports
/// procesan el Result manualmente — necesitan <c>Results.File</c> con
/// headers HMAC, no <c>Results.Ok(value)</c>.
/// </para>
/// </summary>
public sealed class AuditLogModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit/logs")
            .WithTags("Audit")
            .RequireAuthorization();

        group.MapGet("/", QueryAsync).WithName("AuditLog_Query")
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("AuditLog.View");

        group.MapGet("/export.csv", ExportCsvAsync).WithName("AuditLog_ExportCsv")
            .RequirePermission("AuditLog.Export");

        group.MapGet("/export.pdf", ExportPdfAsync).WithName("AuditLog_ExportPdf")
            .RequirePermission("AuditLog.Export");

        // Feature 012 (T38, FR-008; T066): verificación de la cadena de sellos de la auditoría. Es una
        // consulta (sin Idempotency-Key, contracts/api.md §2.3, §29).
        app.MapPost("/api/audit/integrity/verify", async (VerifyAuditIntegrityRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new VerifyAuditIntegrityQuery(body.From, body.To, body.Stream), ct))
            .WithTags("Audit")
            .WithName("AuditLog_VerifyIntegrity")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>()
            .RequirePermission("AuditLog.VerifyIntegrity");

        // Feature 009 (FR-051): ingreso a una opción del ERP. Sesión y cooperativa activa bastan: es el
        // propio usuario contando dónde estuvo; el evento lo escribe el handler con Module=Navigation
        // (feature 012: en COR_AuditOutbox, encadenado).
        app.MapPost("/api/audit/access", async (RegisterOptionAccessCommand body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(body, ct);
                return result.IsSuccess ? Results.Accepted() : (object)result;
            })
            .WithTags("Audit")
            .WithName("AuditLog_RegisterAccess")
            .RequireAuthorization()
            .AddEndpointFilter<ErrorEnvelopeFilter>();
    }

    private static async Task<object?> QueryAsync(
        [AsParameters] AuditQueryParams q,
        ISender sender,
        CancellationToken ct) =>
        await sender.Send(new QueryAuditLogQuery(
            UserId: q.UserId,
            EntityType: q.EntityType,
            EntityId: q.EntityId,
            Module: q.Module,
            Action: q.Action,
            From: q.From,
            To: q.To,
            Paging: new PageRequest(q.Page ?? 1, q.PageSize ?? 50))
        {
            // Feature 012 (T423): ?modules=Inventory,Approvals&result=Rejected.
            Modules = string.IsNullOrWhiteSpace(q.Modules) ? null : q.Modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Outcome = q.Result,
        }, ct);

    private static async Task<IResult> ExportCsvAsync(
        [AsParameters] AuditExportParams q,
        ISender sender,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await sender.Send(new ExportAuditLogCsvQuery(
            UserId: q.UserId, EntityType: q.EntityType,
            Module: q.Module, Action: q.Action,
            From: q.From, To: q.To), ct);

        if (result.IsFailure)
        {
            return (IResult)ErrorEnvelopeFilter.Translate(http, result)!;
        }

        var export = result.Value;
        http.Response.Headers["X-Audit-Row-Count"] = export.RowCount.ToString();
        return Results.File(export.Content, "text/csv; charset=utf-8", export.FileName);
    }

    private static async Task<IResult> ExportPdfAsync(
        [AsParameters] AuditExportParams q,
        ISender sender,
        HttpContext http,
        CancellationToken ct)
    {
        var result = await sender.Send(new ExportAuditLogPdfQuery(
            UserId: q.UserId, EntityType: q.EntityType,
            Module: q.Module, Action: q.Action,
            From: q.From, To: q.To), ct);

        if (result.IsFailure)
        {
            return (IResult)ErrorEnvelopeFilter.Translate(http, result)!;
        }

        var export = result.Value;
        // El verificador SaaS (T090) lee estos headers para recalcular HMAC.
        http.Response.Headers["X-Audit-Hmac"] = export.HmacBase64;
        http.Response.Headers["X-Audit-Key-Version"] = export.KeyVersion;
        http.Response.Headers["X-Audit-Row-Count"] = export.RowCount.ToString();
        return Results.File(export.Content, "application/pdf", export.FileName);
    }
}

public sealed record AuditQueryParams(
    string? UserId,
    string? EntityType,
    string? EntityId,
    string? Module,
    string? Action,
    DateTime? From,
    DateTime? To,
    int? Page = null,
    int? PageSize = null,
    string? Modules = null,
    string? Result = null);

public sealed record AuditExportParams(
    string? UserId,
    string? EntityType,
    string? Module,
    string? Action,
    DateTime? From,
    DateTime? To);

/// <summary>Cuerpo de <c>POST /api/audit/integrity/verify</c> (contracts/api.md §29).</summary>
public sealed record VerifyAuditIntegrityRequest(DateTime From, DateTime To, string? Stream);
