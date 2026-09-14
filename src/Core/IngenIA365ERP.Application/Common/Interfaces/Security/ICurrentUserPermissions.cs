namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Los permisos efectivos de quien hace la petición, en la cooperativa activa (feature 008).
/// La implementación vive en la API (<c>PermisosDelHandler</c>), que es quien tiene el
/// <c>HttpContext</c>; Application sólo conoce este contrato para que un handler pueda
/// exponerlos al cliente (<c>GetMyPermissionsQuery</c>).
/// </summary>
public interface ICurrentUserPermissions
{
    /// <summary>
    /// El administrador maestro de la plataforma no lleva permisos por cooperativa: la puerta
    /// de la API lo deja pasar por un atajo. El cliente lo trata igual, como «todo».
    /// </summary>
    bool EsMaestroGlobal { get; }

    /// <summary>Códigos <c>Recurso.Acción</c> concedidos por los roles del usuario en la cooperativa activa. Vacío si no hay cooperativa o fila en <c>SEC_Users</c>.</summary>
    Task<IReadOnlyCollection<string>> ListAsync(CancellationToken ct);
}
