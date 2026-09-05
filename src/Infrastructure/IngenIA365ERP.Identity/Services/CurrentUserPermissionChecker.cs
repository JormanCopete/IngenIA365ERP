using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Services;

namespace IngenIA365ERP.Identity.Services;

/// <summary>
/// <see cref="IPermissionChecker"/> para handlers: el permiso del usuario actual vía el
/// mismo <see cref="IPermissionService"/> que usa el filtro de los endpoints, así una
/// decisión dentro de un handler (autorizar una excepción al aprobar la nómina) se
/// resuelve igual que en la puerta.
/// </summary>
public sealed class CurrentUserPermissionChecker(IPermissionService permissions, ICurrentUserService currentUser) : IPermissionChecker
{
    public Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Task.FromResult(false);
        return permissions.HasPermissionAsync(userId, permissionCode);
    }
}
