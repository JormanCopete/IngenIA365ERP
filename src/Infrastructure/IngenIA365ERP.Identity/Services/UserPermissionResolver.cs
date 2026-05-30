using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Services;

/// <summary>
/// T075 — Resuelve los permisos efectivos del usuario uniendo
/// <c>SEC_UserRoles → SEC_RolePermissions → SEC_Permissions</c> y devuelve
/// la lista de códigos <c>Resource.Action</c>. Cachea el resultado en
/// <see cref="IPermissionClaimsCache"/> (TTL 30 min — FR-019, SC-005).
///
/// Cualquier mutación de roles/permisos (T069/T070) invalida el cache vía
/// <c>IPermissionClaimsCache.InvalidateAsync</c> o <c>InvalidateRoleAsync</c>;
/// si la invalidación falla (Redis caído), el TTL garantiza la propagación
/// en máximo 30 minutos.
///
/// Resiliencia: si la consulta a BD falla por schema mismatch (207/208),
/// loguea y devuelve lista vacía — el login sigue, pero el usuario opera
/// sin permisos hasta resolver. Mejor que tumbar el flujo.
/// </summary>
public sealed class UserPermissionResolver : IUserPermissionResolver
{
    private readonly IApplicationDbContext _db;
    private readonly IPermissionClaimsCache? _cache;
    private readonly ILogger<UserPermissionResolver> _logger;

    public UserPermissionResolver(
        IApplicationDbContext db,
        ILogger<UserPermissionResolver> logger,
        IPermissionClaimsCache? cache = null)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    public async Task<IReadOnlyList<string>> ResolveAsync(
        int userId, string tenantId, CancellationToken ct)
    {
        var safeTenant = tenantId ?? string.Empty;

        if (_cache is not null)
        {
            var cached = await _cache.GetAsync(userId, safeTenant, ct);
            if (cached is not null) return cached;
        }

        IReadOnlyList<string> permissions;
        try
        {
            permissions = await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Join(_db.Roles,
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => r.Id)
                .Where(roleId => _db.Roles.Any(r => r.Id == roleId && r.IsActive))
                .Join(_db.RolePermissions.Where(rp => !rp.IsDeleted),
                    roleId => roleId,
                    rp => rp.RoleId,
                    (roleId, rp) => rp.PermissionId)
                .Distinct()
                .Join(_db.Permissions,
                    permId => permId,
                    p => p.Id,
                    (permId, p) => p.Resource + "." + p.Action)
                .ToListAsync(ct);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 207 or 208)
        {
            _logger.LogError(ex,
                "UserPermissionResolver: tablas SEC_UserRoles/SEC_RolePermissions/SEC_Permissions " +
                "están desfasadas. Verifica migraciones 19 y 21. Devolviendo lista vacía — " +
                "el usuario operará sin permisos hasta que se resuelva.");
            permissions = [];
        }

        if (_cache is not null)
        {
            await _cache.SetAsync(userId, safeTenant, permissions, ct);
        }

        return permissions;
    }
}
