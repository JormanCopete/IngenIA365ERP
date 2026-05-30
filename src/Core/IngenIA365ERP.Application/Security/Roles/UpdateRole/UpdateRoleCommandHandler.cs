using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.UpdateRole;

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Result>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionClaimsCache? _claimsCache;

    public UpdateRoleCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IPermissionClaimsCache? claimsCache = null)
    {
        _db = db;
        _currentUser = currentUser;
        _claimsCache = claimsCache;
    }

    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.PublicId == request.RolePublicId, ct);

        if (role is null)
        {
            return Result.Failure("Generic.NotFound", "El rol no existe.");
        }

        // Built-in: solo se permite editar Name, Description y permisos. IsAssignable
        // y Code quedan blindados. (FR-020: CompanyAdmin/Auditor son inmutables en
        // su naturaleza, pero permitimos refinar el set de permisos.)
        role.Name = request.Name;
        role.Description = request.Description;
        if (!role.IsBuiltIn)
        {
            role.IsAssignable = request.IsAssignable;
        }
        role.UpdatedBy = _currentUser.UserName ?? "SYSTEM";

        // Reasignar permisos: diff contra los actuales.
        var requestedPermissionIds = request.PermissionPublicIds?.Distinct().ToList() ?? [];
        var matchedPermissions = await _db.Permissions
            .Where(p => requestedPermissionIds.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId })
            .ToListAsync(ct);

        if (matchedPermissions.Count != requestedPermissionIds.Count)
        {
            var missing = requestedPermissionIds
                .Except(matchedPermissions.Select(p => p.PublicId))
                .ToList();
            return Result.Failure(
                RoleErrorCodes.PermissionsInvalid,
                $"Permisos no existen en el catálogo: {string.Join(", ", missing)}.");
        }

        var currentLinks = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(ct);
        var currentPermissionIds = currentLinks.Select(l => l.PermissionId).ToHashSet();
        var requestedPermissionIdsInt = matchedPermissions.Select(p => p.Id).ToHashSet();

        // Remover los que ya no están.
        foreach (var link in currentLinks.Where(l => !requestedPermissionIdsInt.Contains(l.PermissionId)))
        {
            link.IsDeleted = true;
            link.DeletedAt = DateTime.UtcNow;
            link.DeletedBy = _currentUser.UserName ?? "SYSTEM";
        }

        // Añadir los nuevos.
        foreach (var perm in matchedPermissions.Where(p => !currentPermissionIds.Contains(p.Id)))
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = perm.Id,
                CreatedBy = _currentUser.UserName ?? "SYSTEM",
                UpdatedBy = _currentUser.UserName ?? "SYSTEM"
            });
        }

        await _db.SaveChangesAsync(ct);

        // Invalidar el cache de permisos efectivos de los usuarios con este rol.
        // T075 expone el método; si aún no está cableado, el TTL del cache (30 min)
        // sirve de fallback (FR-019 — ≤ 30 min para propagar).
        if (_claimsCache is not null)
        {
            await _claimsCache.InvalidateRoleAsync(role.Id, ct);
        }

        return Result.Success();
    }
}
