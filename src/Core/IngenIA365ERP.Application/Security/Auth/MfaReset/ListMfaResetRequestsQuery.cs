using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Auth.MfaReset;

/// <summary>
/// Feature 003 (US6, FR-119) — Listado paginado de solicitudes de reset de MFA
/// del tenant activo, con solicitante y afectado resueltos a username/email
/// para que el aprobador opere sin GUIDs. Query bajo el tenant context
/// (principio IV — nunca expone <c>int Id</c>).
/// </summary>
public sealed record ListMfaResetRequestsQuery(
    MfaResetStatus? Status = null,
    int Page = 1,
    int PageSize = 20)
    : IRequest<Result<ListMfaResetRequestsResult>>;

public sealed record ListMfaResetRequestsResult(
    IReadOnlyList<MfaResetRequestSummary> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record MfaResetRequestSummary(
    Guid PublicId,
    string TargetUserName,
    string TargetEmail,
    string RequestedByUserName,
    string Reason,
    DateTime RequestedAt,
    DateTime ExpiresAt,
    string Status,
    bool HasFirstApproval,
    bool HasSecondApproval);

public sealed class ListMfaResetRequestsQueryHandler(
    IApplicationDbContext db,
    ICurrentCentralUserContext usuarioCentral)
    : IRequestHandler<ListMfaResetRequestsQuery, Result<ListMfaResetRequestsResult>>
{
    public async Task<Result<ListMfaResetRequestsResult>> Handle(
        ListMfaResetRequestsQuery request, CancellationToken ct)
    {
        // Ver QuienLlamaEnLaCooperativa: leer ICurrentUserService.UserId aquí
        // devolvía null siempre bajo identidad central, así que la cola de
        // aprobación respondía 401 a todo el mundo.
        if (await QuienLlamaEnLaCooperativa.IdAsync(db, usuarioCentral, ct) is null)
        {
            return Result.Failure<ListMfaResetRequestsResult>(
                "Generic.Unauthorized", "No autenticado.");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = db.MfaResetRequests.AsNoTracking();
        if (request.Status is { } status)
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.PublicId,
                r.Reason,
                r.RequestedAt,
                r.ExpiresAt,
                r.Status,
                r.FirstApproverId,
                r.SecondApproverId,
                r.UserId,
                r.RequestedBy,
            })
            .ToListAsync(ct);

        // Resolver afectado y solicitante en una sola consulta (sin depender
        // de la navegación r.User, que algunos contextos no materializan).
        var userIds = items.Select(i => i.UserId)
            .Concat(items.Select(i => i.RequestedBy))
            .Distinct().ToArray();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);

        var summaries = items.Select(i => new MfaResetRequestSummary(
            PublicId: i.PublicId,
            TargetUserName: users.TryGetValue(i.UserId, out var t) ? t.Username : string.Empty,
            TargetEmail: users.TryGetValue(i.UserId, out var te) ? te.Email ?? string.Empty : string.Empty,
            RequestedByUserName: users.TryGetValue(i.RequestedBy, out var rq) ? rq.Username : string.Empty,
            Reason: i.Reason,
            RequestedAt: i.RequestedAt,
            ExpiresAt: i.ExpiresAt,
            Status: i.Status.ToString(),
            HasFirstApproval: i.FirstApproverId.HasValue,
            HasSecondApproval: i.SecondApproverId.HasValue)).ToList();

        return Result.Success(new ListMfaResetRequestsResult(summaries, total, page, pageSize));
    }
}
