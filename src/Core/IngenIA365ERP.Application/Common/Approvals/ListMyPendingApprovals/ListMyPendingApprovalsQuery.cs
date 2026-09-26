using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.ListMyPendingApprovals;

/// <summary>
/// La bandeja de aprobaciones (feature 012, T33, T085; contracts/api.md §15.2,
/// <c>GET /api/inventory/approvals?status=&amp;mine=&amp;documentClass=&amp;warehousePublicId=&amp;page=&amp;pageSize=</c>).
/// <c>mine=true</c> (defecto): las que quien consulta puede decidir <b>ahora</b> (<c>PendientesParaMiAsync</c>);
/// <c>mine=false</c>: las de su alcance en cualquier estado, incluidas las que pidió. Orden: las más antiguas primero.
/// </summary>
public sealed record ListMyPendingApprovalsQuery(
    ApprovalRequestStatus? Status = null,
    bool? Mine = null,
    string? DocumentClass = null,
    Guid? WarehousePublicId = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<ApprovalRequestDto>>>;

public sealed class ListMyPendingApprovalsQueryHandler(
    IApplicationDbContext db,
    IMotorDeAprobaciones motor,
    VistaDeSolicitudes vista,
    IActorActual actorActual,
    IPermissionChecker permisos,
    IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<ListMyPendingApprovalsQuery, Result<PagedResult<ApprovalRequestDto>>>
{
    public async Task<Result<PagedResult<ApprovalRequestDto>>> Handle(ListMyPendingApprovalsQuery request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);

        IReadOnlyList<ApprovalRequest> candidatas;
        if (request.Mine != false)
        {
            candidatas = await motor.PendientesParaMiAsync(ct);
            if (request.Status is { } s && s != ApprovalRequestStatus.Pending) candidatas = [];
        }
        else
        {
            var consulta = db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
                .Where(r => r.Module == ApprovalPolicy.ModuloInventario);
            if (request.Status is { } estado) consulta = consulta.Where(r => r.Status == estado);
            var todas = await consulta.OrderBy(r => r.RequestedAt).ToListAsync(ct);

            var visibles = new List<ApprovalRequest>(todas.Count);
            foreach (var r in todas)
                if (await vista.AlAlcanceAsync(r, alcance, ct)) visibles.Add(r);
            candidatas = visibles;
        }

        if (request.WarehousePublicId is { } bodega)
            candidatas = candidatas.Where(r => r.ScopeWarehousePublicId == bodega).ToList();

        var permisosVistos = new Dictionary<string, bool>(StringComparer.Ordinal);
        async Task<bool> PuedeAsync(ApprovalRequest r)
        {
            if (VistaDeSolicitudes.NivelActual(r) is not { } nivel) return false;
            if (!permisosVistos.TryGetValue(nivel.PermissionCode, out var tiene))
                permisosVistos[nivel.PermissionCode] = tiene = await permisos.HasPermissionAsync(nivel.PermissionCode, ct);
            return tiene && await vista.AlAlcanceAsync(r, alcance, ct);
        }

        // La clase la conoce sólo la fuente: se filtra sobre lo descrito, antes de paginar.
        var dtos = await vista.ADtosAsync(candidatas, actor.UserId, PuedeAsync, conDecisiones: false, ct);
        if (!string.IsNullOrWhiteSpace(request.DocumentClass))
            dtos = dtos.Where(d => string.Equals(d.Source.Class, request.DocumentClass, StringComparison.OrdinalIgnoreCase)).ToList();

        var pagina = new PageRequest(request.Page, request.PageSize);
        var items = dtos.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        return Result.Success(new PagedResult<ApprovalRequestDto>(items, pagina.SafePage, pagina.SafePageSize, dtos.Count));
    }
}
