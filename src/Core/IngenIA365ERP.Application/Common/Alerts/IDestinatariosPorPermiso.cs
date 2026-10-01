namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// A quién le llega una alerta (feature 012, T39, T092; SC-022). La implementación vive en la API
/// (<c>DestinatariosPorPermiso</c>), que resuelve permisos de otros usuarios como la puerta: por
/// <c>SEC_UserRoles → SEC_RolePermissions</c> de la cooperativa (los globos de los roles integrados ya están expandidos
/// en filas por <c>BuiltInRolesSeeder</c>). (nuevo)
/// </summary>
public interface IDestinatariosPorPermiso
{
    /// <summary>
    /// Los usuarios activos de la cooperativa cuyos roles —distintos de <c>CompanyAdmin</c>, que lo concede todo y es el
    /// respaldo— conceden alguno de los <paramref name="permisos"/> y tienen alcance sobre la bodega y el punto de la
    /// alerta. Sin ninguno, los titulares de <c>CompanyAdmin</c> con <see cref="DestinatariosDeAlerta.SinDestinatario"/>.
    /// </summary>
    Task<DestinatariosDeAlerta> ResolverAsync(
        IReadOnlyCollection<string> permisos,
        Guid? bodegaPublicId,
        Guid? puntoPublicId,
        CancellationToken ct);

    /// <summary>Cuántos usuarios activos recibirían hoy una alerta de estos permisos, sin mirar alcance (<c>activeRecipients</c>).</summary>
    Task<int> ContarActivosAsync(IReadOnlyCollection<string> permisos, CancellationToken ct);

    /// <summary>
    /// Los tipos vigentes y habilitados a la fecha cuyo permiso no tiene ningún usuario activo: los que se irían a
    /// <c>CompanyAdmin</c> (el reporte de completitud de SC-022).
    /// </summary>
    Task<IReadOnlyList<string>> TiposSinDestinatarioAsync(DateOnly fecha, CancellationToken ct);
}

/// <summary>Un destinatario de alerta. (nuevo)</summary>
public sealed record DestinatarioDeAlerta(int UserId, Guid UserPublicId, string Name, string? Email);

/// <summary>Los destinatarios y si la alerta se enrutó a <c>CompanyAdmin</c> por no tener ninguno. (nuevo)</summary>
public sealed record DestinatariosDeAlerta(IReadOnlyList<DestinatarioDeAlerta> Usuarios, bool SinDestinatario);
