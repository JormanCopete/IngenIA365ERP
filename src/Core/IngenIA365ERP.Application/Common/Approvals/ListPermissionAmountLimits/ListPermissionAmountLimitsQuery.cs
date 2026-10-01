using IngenIA365ERP.Application.Common.Approvals.SetPermissionAmountLimit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Approvals.ListPermissionAmountLimits;

/// <summary>
/// Los montos máximos por permiso (feature 012, T084; contracts/api.md §15.3,
/// <c>GET /api/inventory/amount-limits?rolePublicId=&amp;permissionCode=&amp;asOf=</c>). Con <see cref="AsOf"/>, sólo los
/// vigentes a esa fecha; sin él, toda la historia. Por rol, permiso y fecha; sin paginar. (nuevo)
/// </summary>
public sealed record ListPermissionAmountLimitsQuery(
    Guid? RolePublicId = null,
    string? PermissionCode = null,
    DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<PermissionAmountLimitDto>>>;

public sealed class ListPermissionAmountLimitsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListPermissionAmountLimitsQuery, Result<IReadOnlyList<PermissionAmountLimitDto>>>
{
    public async Task<Result<IReadOnlyList<PermissionAmountLimitDto>>> Handle(ListPermissionAmountLimitsQuery request, CancellationToken ct)
    {
        var consulta = db.PermissionAmountLimits.AsNoTracking().Include(l => l.Role).AsQueryable();
        if (request.RolePublicId is { } rol) consulta = consulta.Where(l => l.Role!.PublicId == rol);
        if (!string.IsNullOrWhiteSpace(request.PermissionCode)) consulta = consulta.Where(l => l.PermissionCode == request.PermissionCode);

        var filas = await consulta.ToListAsync(ct);
        if (request.AsOf is { } fecha) filas = filas.Where(l => l.VigenteEn(fecha)).ToList();

        IReadOnlyList<PermissionAmountLimitDto> dtos = filas
            .Where(l => l.Role is not null)
            .OrderBy(l => l.Role!.Name, StringComparer.CurrentCulture)
            .ThenBy(l => l.PermissionCode, StringComparer.Ordinal)
            .ThenBy(l => l.ValidFrom)
            .Select(l => MontosMaximos.ADto(l, l.Role!))
            .ToList();
        return Result.Success(dtos);
    }
}
