using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Users.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Users.RemoveRole;

public sealed class RemoveRoleCommandHandler : IRequestHandler<RemoveRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _clock;
    private readonly IPermissionClaimsCache? _claimsCache;

    public RemoveRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IDateTimeService clock,
        IPermissionClaimsCache? claimsCache = null)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _claimsCache = claimsCache;
    }

    public async Task<Result> Handle(RemoveRoleCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PublicId == request.UserPublicId, ct);
        if (user is null) return Result.Failure("Generic.NotFound", "El usuario no existe.");

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId, ct);
        if (role is null) return Result.Failure("Generic.NotFound", "El rol no existe.");

        var link = await _db.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id, ct);

        if (link is null)
        {
            return Result.Failure(UserErrorCodes.NotAssignedRole,
                "El usuario no tiene asignado ese rol.");
        }

        link.IsDeleted = true;
        link.DeletedAt = _clock.UtcNow;
        link.DeletedBy = _currentUser.UserName ?? "SYSTEM";
        link.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        await _db.SaveChangesAsync(ct);

        if (_claimsCache is not null)
        {
            await _claimsCache.InvalidateAsync(user.Id,
                _currentUser.TenantId ?? string.Empty, ct);
        }
        return Result.Success();
    }
}
