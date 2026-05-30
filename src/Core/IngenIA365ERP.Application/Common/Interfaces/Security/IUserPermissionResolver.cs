namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Resuelve los permisos efectivos de un usuario en el tenant indicado
/// (unión de los permisos de todos los roles activos asignados — FR-016).
/// El resultado son strings <c>Resource.Action</c> que el JWT emite como
/// claims <c>perm</c>; el <see cref="PermissionAuthorizationFilter"/> los lee.
///
/// La implementación usa el cache <see cref="IPermissionClaimsCache"/>:
/// la primera consulta toca BD, las siguientes (mismo userId + tenantId)
/// se sirven desde memoria/Redis hasta que un cambio de rol/permiso
/// dispare invalidación.
/// </summary>
public interface IUserPermissionResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(int userId, string tenantId, CancellationToken ct);
}
