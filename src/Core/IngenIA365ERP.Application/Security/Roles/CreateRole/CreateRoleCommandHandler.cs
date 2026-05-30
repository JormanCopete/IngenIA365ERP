using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Security.Roles.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Security.Roles.CreateRole;

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateRoleCommandHandler(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();

        var codeExists = await _db.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Code == request.Code, ct);
        if (codeExists)
        {
            return Result.Failure<Guid>(
                RoleErrorCodes.CodeAlreadyExists,
                $"Ya existe un rol con código '{request.Code}' en esta cooperativa.");
        }

        // Validar que todos los PublicId de permisos existan en el catálogo.
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
            return Result.Failure<Guid>(
                RoleErrorCodes.PermissionsInvalid,
                $"Los siguientes permisos no existen en el catálogo: {string.Join(", ", missing)}.");
        }

        var role = new Role
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description,
            IsBuiltIn = false,
            IsAssignable = true,
            IsActive = true,
            CreatedBy = _currentUser.UserName ?? "SYSTEM",
            UpdatedBy = _currentUser.UserName ?? "SYSTEM"
        };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        // Vincular permisos. SaveChanges anterior asignó role.Id.
        foreach (var perm in matchedPermissions)
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

        return Result.Success(role.PublicId);
    }

    private int? ResolveTenantId() =>
        int.TryParse(_currentUser.TenantId, out var tid) ? tid : null;
}
