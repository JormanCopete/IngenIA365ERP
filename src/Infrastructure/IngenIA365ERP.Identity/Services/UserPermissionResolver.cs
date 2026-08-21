using System.Data.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using Microsoft.EntityFrameworkCore;
using IngenIA365ERP.Identity.Seed;
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

    public async Task<IReadOnlyList<string>> ResolveForTenantAsync(
        int userId, int tenantInternalId, CancellationToken ct)
    {
        // La clave lleva el Id interno, el mismo que CurrentUserService expone,
        // para que la invalidacion al cambiar un rol golpee esta entrada y no
        // otra. Una clave vacia se compartiria entre cooperativas distintas.
        var claveTenant = tenantInternalId.ToString();

        if (_cache is not null)
        {
            var cached = await _cache.GetAsync(userId, claveTenant, ct);
            if (cached is not null) return cached;
        }

        IReadOnlyList<string> permisos;
        try
        {
            permisos = await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                // Sin filtro por Role.TenantId, y es deliberado: con schema-per-tenant
                // el aislamiento es el esquema, y dentro del esquema de una cooperativa
                // TODOS los roles son suyos. La columna queda como metadato nulable.
                //
                // Filtrar por ella no solo sobra: no funcionaba. La FK
                // FK_SEC_Roles_ADM_Tenants_TenantId apunta al ADM_Tenants LOCAL de cada
                // esquema, que tiene cero filas, asi que insertar un rol con TenantId
                // era fisicamente imposible y esta comprobacion no podia dar verdadera
                // jamas. Es la razon de que ningun esquema de cooperativa tenga roles.
                //
                // Se descarto la alternativa —poblar TenantId con el Id interno— porque
                // ata cada esquema a la secuencia de identidad de la BD administrativa:
                // si una cooperativa se re-registra y recibe otro Id, pierde todos sus
                // permisos en silencio.
                .Join(_db.Roles.Where(r => r.IsActive && !r.IsDeleted),
                    ur => ur.RoleId,
                    r => r.Id,
                    (ur, r) => r.Id)
                .Join(_db.RolePermissions.Where(rp => !rp.IsDeleted),
                    roleId => roleId,
                    rp => rp.RoleId,
                    (roleId, rp) => rp.PermissionId)
                .Distinct()
                .Join(_db.Permissions.Where(p => !p.IsDeleted),
                    permId => permId,
                    p => p.Id,
                    (permId, p) => p.Resource + "." + p.Action)
                .ToListAsync(ct);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException or InvalidOperationException)
        {
            // Devolver vacio y no propagar: una excepcion aqui subiria como 500
            // y delataria que el endpoint existe, rompiendo la indistinguibilidad
            // 404 (FR-017). Fallar cerrado es la respuesta correcta.
            _logger.LogError(ex,
                "No se pudieron resolver los permisos del usuario {UserId} en la cooperativa " +
                "{TenantId}. Se devuelve conjunto vacio: el usuario vera 404 en los endpoints " +
                "protegidos hasta que se resuelva.",
                userId, tenantInternalId);
            return [];
        }

        // Segundo candado, por si alguien vuelve a insertar los vinculos a mano
        // o restaura un respaldo anterior a la purga de BuiltInRolesSeeder.
        var retenidos = permisos.Where(BuiltInRolesSeeder.EsSaasGlobal).ToList();
        if (retenidos.Count > 0)
        {
            _logger.LogWarning(
                "El usuario {UserId} tiene asignados {Cantidad} permiso(s) SaaS-globales en la " +
                "cooperativa {TenantId} ({Codigos}). Se descartan: operan sobre el conjunto de " +
                "cooperativas. Revisa SEC_RolePermissions.",
                userId, retenidos.Count, tenantInternalId, string.Join(", ", retenidos));
            permisos = [.. permisos.Where(c => !BuiltInRolesSeeder.EsSaasGlobal(c))];
        }

        if (_cache is not null)
        {
            await _cache.SetAsync(userId, claveTenant, permisos, ct);
        }

        return permisos;
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
