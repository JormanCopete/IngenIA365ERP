using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.GetApprovalRequest;

/// <summary>
/// El detalle de una solicitud con sus decisiones (feature 012, T33, T085; contracts/api.md §15.2,
/// <c>GET /api/inventory/approvals/{id}</c>; el POS lo consulta cada 2 s mientras espera). Fuera del alcance: el mismo
/// 404 que una inexistente. (nuevo)
/// </summary>
public sealed record GetApprovalRequestQuery(Guid RequestPublicId) : IRequest<Result<ApprovalRequestDto>>;

public sealed class GetApprovalRequestQueryHandler(
    IApplicationDbContext db,
    VistaDeSolicitudes vista,
    IActorActual actorActual,
    IPermissionChecker permisos,
    IAlcanceDeInventario alcanceDeLaPeticion)
    : IRequestHandler<GetApprovalRequestQuery, Result<ApprovalRequestDto>>
{
    public async Task<Result<ApprovalRequestDto>> Handle(GetApprovalRequestQuery request, CancellationToken ct)
    {
        var solicitud = await db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
            .FirstOrDefaultAsync(r => r.PublicId == request.RequestPublicId, ct);
        if (solicitud is null) return Result.Failure<ApprovalRequestDto>(ErroresDeAprobaciones.SolicitudInexistente());

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        if (!await vista.AlAlcanceAsync(solicitud, alcance, ct))
            return Result.Failure<ApprovalRequestDto>(ErroresDeAprobaciones.SolicitudInexistente());

        var actor = await actorActual.ObtenerAsync(ct);
        var dtos = await vista.ADtosAsync([solicitud], actor.UserId,
            async r => VistaDeSolicitudes.NivelActual(r) is { } nivel && await permisos.HasPermissionAsync(nivel.PermissionCode, ct),
            conDecisiones: true, ct);
        return Result.Success(dtos[0]);
    }
}
