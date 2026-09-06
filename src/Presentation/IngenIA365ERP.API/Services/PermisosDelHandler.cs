using IngenIA365ERP.Application.Payroll.Services;

namespace IngenIA365ERP.API.Services;

/// <summary>
/// <see cref="IPermissionChecker"/> para los handlers que deciden con un permiso en la mano
/// (autorizar una excepción al aprobar la nómina, feature 005). Resuelve con
/// <see cref="PermisosDeLaPeticion"/>, el mismo camino que la puerta del endpoint:
/// identidad central del token → fila en <c>SEC_Users</c> → cooperativa activa → códigos.
///
/// <para>
/// Sustituye al verificador de Identity, que preguntaba por el <c>UserId</c> entero de
/// <c>ICurrentUserService</c>: para un usuario de identidad central ese entero es nulo y
/// la respuesta era siempre «no», aunque la puerta lo hubiera dejado pasar. Lo detectó la
/// prueba e2e de aprobación: el administrador de la cooperativa, con todos los permisos,
/// recibía <c>Payroll.ExceptionNotAuthorized</c>.
/// </para>
/// </summary>
internal sealed class PermisosDelHandler(IHttpContextAccessor accessor, PermisosDeLaPeticion permisos) : IPermissionChecker
{
    public async Task<bool> HasPermissionAsync(string permissionCode, CancellationToken ct)
    {
        var http = accessor.HttpContext;
        if (http is null) return false;
        var concedidos = await permisos.ResolverAsync(http, ct);
        return concedidos.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }
}
