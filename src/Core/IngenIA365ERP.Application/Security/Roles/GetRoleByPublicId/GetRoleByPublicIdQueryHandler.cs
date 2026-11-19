using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.GetRoleByPublicId;

public sealed class GetRoleByPublicIdQueryHandler
    : IRequestHandler<GetRoleByPublicIdQuery, Result<RoleDto>>
{
    private readonly IApplicationDbContext _db;

    public GetRoleByPublicIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<RoleDto>> Handle(GetRoleByPublicIdQuery request, CancellationToken ct)
    {
        var role = await _db.Roles
            .Where(r => r.PublicId == request.RolePublicId)
            .Select(r => new
            {
                r.Id,
                r.PublicId,
                r.Code,
                r.Name,
                r.Description,
                r.IsBuiltIn,
                r.IsAssignable,
                r.IsActive
            })
            .FirstOrDefaultAsync(ct);

        if (role is null)
        {
            return Result.Failure<RoleDto>("Generic.NotFound", "Recurso no encontrado.");
        }

        var perms = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
            .Join(_db.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => new RolePermissionDto(p.PublicId, $"{p.Resource}.{p.Action}"))
            .ToListAsync(ct);

        // 2026-11-19: quién tiene el rol. User lleva filtro global de borrado: un usuario eliminado deja ur.User en null.
        var usuarios = await _db.UserRoles
            .Where(ur => ur.RoleId == role.Id && ur.User != null)
            .OrderBy(ur => ur.User!.Username)
            .Select(ur => new RoleUserDto(ur.User!.PublicId, ur.User.Username, ur.User.IsActive))
            .ToListAsync(ct);

        return Result.Success(new RoleDto(
            role.PublicId, role.Code, role.Name, role.Description,
            role.IsBuiltIn, role.IsAssignable, role.IsActive, perms, usuarios));
    }
}
