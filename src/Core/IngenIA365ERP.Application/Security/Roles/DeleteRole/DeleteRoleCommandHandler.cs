using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.DeleteRole;

public sealed class DeleteRoleCommandHandler : IRequestHandler<DeleteRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionClaimsCache? _claimsCache;

    public DeleteRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionClaimsCache? claimsCache = null)
    {
        _db = db;
        _currentUser = currentUser;
        _claimsCache = claimsCache;
    }

    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken ct)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId, ct);

        if (role is null)
        {
            return Result.Failure("Generic.NotFound", "El rol no existe.");
        }

        if (role.IsBuiltIn)
        {
            return Result.Failure(
                RoleErrorCodes.CannotDeleteBuiltIn,
                $"El rol built-in '{role.Code}' no se puede eliminar.");
        }

        var hasUsers = await _db.UserRoles
            .AnyAsync(ur => ur.RoleId == role.Id, ct);
        if (hasUsers)
        {
            return Result.Failure(
                RoleErrorCodes.CannotDeleteWithUsers,
                "El rol está asignado a uno o más usuarios. " +
                "Quita primero la asignación antes de eliminarlo.");
        }

        var now = DateTime.UtcNow;
        var actor = _currentUser.UserName ?? "SYSTEM";
        role.IsDeleted = true;
        role.DeletedAt = now;
        role.DeletedBy = actor;
        role.UpdatedBy = actor;

        // Soft-delete también los vínculos a permisos para que no aparezcan
        // en listados futuros del rol si llegara a "renacer" por restore.
        var links = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id && !rp.IsDeleted)
            .ToListAsync(ct);
        foreach (var link in links)
        {
            link.IsDeleted = true;
            link.DeletedAt = now;
            link.DeletedBy = actor;
        }

        await _db.SaveChangesAsync(ct);

        if (_claimsCache is not null)
        {
            await _claimsCache.InvalidateRoleAsync(role.Id, ct);
        }

        return Result.Success();
    }
}
