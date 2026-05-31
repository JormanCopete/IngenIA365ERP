namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Lectura cacheada de membresías activas para el flujo de login (research D-07).
/// Cache en Redis con TTL 60s + invalidación pub/sub vía
/// <see cref="IMembershipChangedNotifier"/> al mutar membresías o políticas.
/// </summary>
public interface ITenantMembershipReader
{
    /// <summary>Lista de membresías activas (Status=Active) del usuario, con flags
    /// de tenant admin y política MFA. Lee de cache si está caliente.</summary>
    Task<IReadOnlyList<ActiveMembershipInfo>> GetActiveMembershipsAsync(
        Guid centralUserId,
        CancellationToken ct);

    /// <summary>Devuelve true si el usuario tiene una membresía Active con el tenant indicado.</summary>
    Task<bool> IsMemberOfTenantAsync(
        Guid centralUserId,
        Guid tenantId,
        CancellationToken ct);

    /// <summary>Invalida la cache local del proceso (no propaga pub/sub —
    /// para eso usar <see cref="IMembershipChangedNotifier.PublishAsync"/>).</summary>
    Task InvalidateLocalCacheAsync(Guid centralUserId, CancellationToken ct);
}

public sealed record ActiveMembershipInfo(
    Guid TenantId,
    string TenantName,
    bool IsTenantAdmin,
    bool IsMfaRequiredByTenant);
