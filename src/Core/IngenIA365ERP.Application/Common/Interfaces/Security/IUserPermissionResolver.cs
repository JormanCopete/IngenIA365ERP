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

    /// <summary>
    /// Permisos del usuario <b>dentro de una cooperativa concreta</b>, indicada
    /// por su Id interno.
    ///
    /// <para>
    /// Existe aparte de <see cref="ResolveAsync"/> porque aquella recibe el
    /// tenant como cadena libre y no lo usa para filtrar nada: devuelve los
    /// permisos de TODOS los roles del usuario, sean de la cooperativa que
    /// sean. Mientras nadie leyera el resultado daba igual; en cuanto se
    /// autoriza con él, es una fuga entre cooperativas.
    /// </para>
    ///
    /// <para>
    /// Nunca devuelve permisos SaaS-globales, ni aunque estén asignados en la
    /// base: esos operan sobre el conjunto de cooperativas y sólo los ejerce el
    /// administrador maestro.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> ResolveForTenantAsync(
        int userId, int tenantInternalId, CancellationToken ct);
}
