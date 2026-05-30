using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Admin.Tenants.ProvisionSchema;

public sealed class ProvisionTenantSchemaCommandHandler
    : IRequestHandler<ProvisionTenantSchemaCommand, Result<ProvisionTenantSchemaResult>>
{
    private const string HeadquartersCode = "MAT";
    private const string HeadquartersName = "Sede Principal";

    private readonly IAdminDbContext _admin;
    private readonly IApplicationDbContext _operational;
    private readonly ICurrentUserService _currentUser;

    public ProvisionTenantSchemaCommandHandler(
        IAdminDbContext admin,
        IApplicationDbContext operational,
        ICurrentUserService currentUser)
    {
        _admin = admin;
        _operational = operational;
        _currentUser = currentUser;
    }

    public async Task<Result<ProvisionTenantSchemaResult>> Handle(
        ProvisionTenantSchemaCommand request, CancellationToken ct)
    {
        // El tenant vive en IngenIA365ERP_Admin; los roles en IngenIA365ERP
        // (operacional). Resolvemos primero el Id interno via PublicId.
        var tenant = await _admin.Tenants
            .Where(t => t.PublicId == request.TenantPublicId)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct);
        if (tenant is null)
        {
            return Result.Failure<ProvisionTenantSchemaResult>(
                "Generic.NotFound", "La cooperativa no existe.");
        }

        var actor = _currentUser.UserName ?? "SYSTEM";
        var rolesCloned = await CloneBuiltInRolesAsync(tenant.Id, actor, ct);
        var permsCloned = await CloneRolePermissionsAsync(tenant.Id, actor, ct);
        var headquartersCreated = await EnsureHeadquartersAsync(tenant.Id, tenant.Name, actor, ct);

        return Result.Success(new ProvisionTenantSchemaResult(
            rolesCloned, permsCloned, headquartersCreated));
    }

    /// <summary>
    /// Clona los roles built-in plantilla (TenantId NULL) al tenant indicado.
    /// Idempotente: si el rol ya existe para ese tenant (por Code), se omite.
    /// </summary>
    private async Task<int> CloneBuiltInRolesAsync(int tenantId, string actor, CancellationToken ct)
    {
        var templateRoles = await _operational.Roles
            .Where(r => r.TenantId == null && r.IsBuiltIn)
            .ToListAsync(ct);
        if (templateRoles.Count == 0) return 0;

        var existingCodes = await _operational.Roles
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.Code)
            .ToListAsync(ct);
        var existingSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cloned = 0;
        foreach (var t in templateRoles)
        {
            if (existingSet.Contains(t.Code)) continue;
            _operational.Roles.Add(new Role
            {
                TenantId = tenantId,
                Code = t.Code,
                Name = t.Name,
                Description = t.Description,
                IsBuiltIn = true,
                IsSystemRole = true,
                IsAssignable = t.IsAssignable,
                IsActive = true,
                CreatedBy = actor,
                UpdatedBy = actor
            });
            cloned++;
        }
        if (cloned > 0) await _operational.SaveChangesAsync(ct);
        return cloned;
    }

    /// <summary>
    /// Por cada rol clonado, copia sus vínculos a permisos. Mapea
    /// rol-plantilla → rol-tenant por <c>Code</c>.
    /// </summary>
    private async Task<int> CloneRolePermissionsAsync(int tenantId, string actor, CancellationToken ct)
    {
        var templateRoles = await _operational.Roles
            .Where(r => r.TenantId == null && r.IsBuiltIn)
            .Select(r => new { r.Id, r.Code })
            .ToListAsync(ct);
        if (templateRoles.Count == 0) return 0;

        var tenantRoles = await _operational.Roles
            .Where(r => r.TenantId == tenantId && r.IsBuiltIn)
            .Select(r => new { r.Id, r.Code })
            .ToListAsync(ct);
        var tenantRoleByCode = tenantRoles.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

        var templateRoleIds = templateRoles.Select(r => r.Id).ToList();
        var templateLinks = await _operational.RolePermissions
            .Where(rp => templateRoleIds.Contains(rp.RoleId) && !rp.IsDeleted)
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);

        var tenantRoleIds = tenantRoles.Select(r => r.Id).ToList();
        var existingLinks = await _operational.RolePermissions
            .Where(rp => tenantRoleIds.Contains(rp.RoleId))
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);
        var existingSet = existingLinks
            .Select(l => (l.RoleId, l.PermissionId))
            .ToHashSet();

        var templateRoleCodeById = templateRoles.ToDictionary(r => r.Id, r => r.Code);
        var cloned = 0;

        foreach (var link in templateLinks)
        {
            if (!templateRoleCodeById.TryGetValue(link.RoleId, out var code)) continue;
            if (!tenantRoleByCode.TryGetValue(code, out var tenantRoleId)) continue;
            if (existingSet.Contains((tenantRoleId, link.PermissionId))) continue;

            _operational.RolePermissions.Add(new RolePermission
            {
                RoleId = tenantRoleId,
                PermissionId = link.PermissionId,
                CreatedBy = actor,
                UpdatedBy = actor
            });
            cloned++;
        }
        if (cloned > 0) await _operational.SaveChangesAsync(ct);
        return cloned;
    }

    private async Task<bool> EnsureHeadquartersAsync(
        int tenantId, string tenantName, string actor, CancellationToken ct)
    {
        var any = await _admin.TenantBranches
            .AnyAsync(b => b.TenantId == tenantId && b.IsHeadquarters, ct);
        if (any) return false;

        _admin.TenantBranches.Add(new TenantBranch
        {
            TenantId = tenantId,
            Code = HeadquartersCode,
            Name = $"{HeadquartersName} — {tenantName}",
            IsActive = true,
            IsHeadquarters = true,
            CreatedBy = actor,
            UpdatedBy = actor
        });
        await _admin.SaveChangesAsync(ct);
        return true;
    }
}
