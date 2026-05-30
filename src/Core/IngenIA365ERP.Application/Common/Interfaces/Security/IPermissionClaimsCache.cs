namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Cache de permisos efectivos por usuario (FR-019). TTL 30 min — junto con
/// el canal pub/sub <c>perms:invalidate</c> garantiza que un cambio de rol
/// se propague en ≤30 min (SC-005) y opcionalmente en tiempo real cuando
/// el subscriber recibe la notificación.
/// </summary>
public interface IPermissionClaimsCache
{
    Task<IReadOnlyList<string>?> GetAsync(int userId, string tenantId, CancellationToken ct);
    Task SetAsync(int userId, string tenantId, IReadOnlyList<string> permissions, CancellationToken ct);
    Task InvalidateAsync(int userId, string tenantId, CancellationToken ct);
    Task InvalidateAllForTenantAsync(string tenantId, CancellationToken ct);

    /// <summary>
    /// Invalida el cache para todos los usuarios que tienen el rol
    /// <paramref name="roleId"/>. Disparado tras editar permisos de un rol o
    /// asignar/quitar el rol (FR-019, US2/T075).
    /// </summary>
    Task InvalidateRoleAsync(int roleId, CancellationToken ct);
}
