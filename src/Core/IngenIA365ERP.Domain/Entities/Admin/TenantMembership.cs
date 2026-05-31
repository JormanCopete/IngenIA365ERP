using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Relación N:N entre <see cref="CentralUser"/> y empresa (FR-009).
/// La máquina de estados (FR-009a) está implementada como métodos que validan la
/// transición vía <see cref="MembershipStatusTransitions.EnsureTransition"/> antes de mutar.
/// Maps to [dbo].[ADM_TenantMemberships].
/// </summary>
public class TenantMembership : AuditableEntity
{
    /// <summary>FK lógica a <c>ADM_CentralUsers.Id</c>. Sin navigation property porque
    /// EF mapea el bridge <c>CentralUserIdentity</c> (Infrastructure), no
    /// <c>CentralUser</c> POCO (Domain). El handler que necesita la entidad
    /// completa la carga vía <c>ICentralIdentityProvider.FindByIdAsync</c>.</summary>
    public Guid CentralUserId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// Setter privado para forzar el uso de los métodos de transición; EF Core lo respeta
    /// vía backing field. Cualquier cambio fuera de la API pública del agregado es bug.
    /// </summary>
    public MembershipStatus Status { get; private set; } = MembershipStatus.Invited;

    public bool IsTenantAdmin { get; private set; }

    public Guid? InvitedByUserId { get; private set; }
    public DateTime InvitedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }

    public DateTime? SuspendedAt { get; private set; }
    public Guid? SuspendedByUserId { get; private set; }

    public DateTime? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }

    // EF Core
    private TenantMembership() { }

    /// <summary>Crea una membresía nueva en estado <see cref="MembershipStatus.Invited"/>.</summary>
    public static TenantMembership CreateInvited(
        Guid centralUserId,
        Guid tenantId,
        bool isTenantAdmin,
        Guid? invitedByUserId,
        DateTime now)
    {
        return new TenantMembership
        {
            CentralUserId = centralUserId,
            TenantId = tenantId,
            Status = MembershipStatus.Invited,
            IsTenantAdmin = isTenantAdmin,
            InvitedByUserId = invitedByUserId,
            InvitedAt = now,
        };
    }

    /// <summary>
    /// Crea una membresía directamente en estado <see cref="MembershipStatus.Active"/>.
    /// Solo lo usa el bootstrap del master admin (T018) o el handler que provisiona
    /// el primer admin de un tenant.
    /// </summary>
    public static TenantMembership CreateActive(
        Guid centralUserId,
        Guid tenantId,
        bool isTenantAdmin,
        Guid? invitedByUserId,
        DateTime now)
    {
        return new TenantMembership
        {
            CentralUserId = centralUserId,
            TenantId = tenantId,
            Status = MembershipStatus.Active,
            IsTenantAdmin = isTenantAdmin,
            InvitedByUserId = invitedByUserId,
            InvitedAt = now,
            ActivatedAt = now,
        };
    }

    /// <summary>Invited → Active (primera aceptación de invitación).</summary>
    public void Activate(DateTime now)
    {
        Status.EnsureTransition(MembershipStatus.Active);
        Status = MembershipStatus.Active;
        ActivatedAt ??= now;
        SuspendedAt = null;
        SuspendedByUserId = null;
        RevokedAt = null;
        RevokedByUserId = null;
    }

    /// <summary>Suspended → Active (reactivación sin nueva invitación).</summary>
    public void Reactivate(DateTime now)
    {
        Status.EnsureTransition(MembershipStatus.Active);
        Status = MembershipStatus.Active;
        SuspendedAt = null;
        SuspendedByUserId = null;
        // ActivatedAt conserva la fecha original de la primera activación.
    }

    /// <summary>Revoked → Active (reactivación vía aceptación de nueva invitación).</summary>
    public void ReactivateFromRevocation(DateTime now)
    {
        Status.EnsureTransition(MembershipStatus.Active);
        Status = MembershipStatus.Active;
        RevokedAt = null;
        RevokedByUserId = null;
    }

    /// <summary>Active → Suspended.</summary>
    public void Suspend(Guid byUserId, DateTime now)
    {
        Status.EnsureTransition(MembershipStatus.Suspended);
        Status = MembershipStatus.Suspended;
        SuspendedAt = now;
        SuspendedByUserId = byUserId;
    }

    /// <summary>Active|Suspended|Invited → Revoked.</summary>
    public void Revoke(Guid byUserId, DateTime now)
    {
        Status.EnsureTransition(MembershipStatus.Revoked);
        Status = MembershipStatus.Revoked;
        RevokedAt = now;
        RevokedByUserId = byUserId;
    }

    /// <summary>Promueve a admin del tenant (FR-040a). La salvaguarda del "último admin"
    /// se valida en Application (handlers transaccionales), no en Domain.</summary>
    public void PromoteToAdmin()
    {
        if (Status != MembershipStatus.Active)
            throw new InvalidOperationException(
                $"Solo membresías activas pueden ser promovidas a admin. Estado actual: {Status}.");

        IsTenantAdmin = true;
    }

    /// <summary>Degrada de admin a miembro regular (FR-040a). La salvaguarda del "último
    /// admin" se valida en Application antes de invocar este método.</summary>
    public void DemoteFromAdmin()
    {
        IsTenantAdmin = false;
    }
}
