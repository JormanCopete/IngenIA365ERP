namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Pub/sub para invalidar caches de membresías en todas las instancias cuando
/// un handler cambia el estado (T039a, research D-07). Implementación inicial:
/// <c>RedisMembershipChangedNotifier</c> publicando en canal <c>membership-changed</c>;
/// <see cref="ITenantMembershipReader"/> está suscrito y limpia su cache local.
///
/// <para>
/// Sin pub/sub, el TTL 60s sería la única defensa contra inconsistencia entre
/// instancias — aceptable pero subóptimo. Con pub/sub, el cambio se propaga
/// instantáneamente en condiciones normales.
/// </para>
/// </summary>
public interface IMembershipChangedNotifier
{
    Task PublishAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>Publica para todos los miembros activos de un tenant (caso típico:
    /// cambio de política MFA obligatoria — invalida cache de cada miembro).</summary>
    Task PublishForTenantMembersAsync(Guid tenantId, CancellationToken ct);
}
