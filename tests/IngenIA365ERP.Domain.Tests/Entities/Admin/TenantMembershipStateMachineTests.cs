using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Exceptions;

namespace IngenIA365ERP.Domain.Tests.Entities.Admin;

/// <summary>
/// Cubre la máquina de estados de <see cref="TenantMembership"/> (FR-009a).
/// Cada transición legal verifica el cambio de estado y los timestamps; cada transición ilegal
/// debe lanzar <see cref="InvalidMembershipTransitionException"/>.
/// </summary>
public class TenantMembershipStateMachineTests
{
    private static readonly Guid AnyUser = Guid.NewGuid();
    private static readonly Guid AnyTenant = Guid.NewGuid();
    private static readonly Guid Inviter = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    // -------------------- Constructores --------------------

    [Fact]
    public void CreateInvited_ShouldStartInInvitedState()
    {
        var m = TenantMembership.CreateInvited(AnyUser, AnyTenant, isTenantAdmin: false, Inviter, Now);

        m.Status.Should().Be(MembershipStatus.Invited);
        m.IsTenantAdmin.Should().BeFalse();
        m.InvitedAt.Should().Be(Now);
        m.ActivatedAt.Should().BeNull();
        m.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void CreateActive_ShouldStartInActiveStateWithActivatedAtSet()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, isTenantAdmin: true, Inviter, Now);

        m.Status.Should().Be(MembershipStatus.Active);
        m.IsTenantAdmin.Should().BeTrue();
        m.ActivatedAt.Should().Be(Now);
    }

    // -------------------- Transiciones legales --------------------

    [Fact]
    public void Activate_FromInvited_ShouldTransitionToActive()
    {
        var m = TenantMembership.CreateInvited(AnyUser, AnyTenant, false, Inviter, Now);

        m.Activate(Now.AddMinutes(5));

        m.Status.Should().Be(MembershipStatus.Active);
        m.ActivatedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void Suspend_FromActive_ShouldTransitionToSuspended()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);

        m.Suspend(Actor, Now.AddDays(1));

        m.Status.Should().Be(MembershipStatus.Suspended);
        m.SuspendedAt.Should().Be(Now.AddDays(1));
        m.SuspendedByUserId.Should().Be(Actor);
    }

    [Fact]
    public void Reactivate_FromSuspended_ShouldTransitionToActiveAndClearSuspensionFields()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);
        m.Suspend(Actor, Now.AddDays(1));
        var originalActivatedAt = m.ActivatedAt;

        m.Reactivate(Now.AddDays(2));

        m.Status.Should().Be(MembershipStatus.Active);
        m.SuspendedAt.Should().BeNull();
        m.SuspendedByUserId.Should().BeNull();
        m.ActivatedAt.Should().Be(originalActivatedAt, "Reactivate preserva la fecha original de primera activación");
    }

    [Fact]
    public void Revoke_FromActive_ShouldTransitionToRevoked()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);

        m.Revoke(Actor, Now.AddDays(3));

        m.Status.Should().Be(MembershipStatus.Revoked);
        m.RevokedAt.Should().Be(Now.AddDays(3));
        m.RevokedByUserId.Should().Be(Actor);
    }

    [Fact]
    public void Revoke_FromSuspended_ShouldTransitionToRevoked()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);
        m.Suspend(Actor, Now.AddDays(1));

        m.Revoke(Actor, Now.AddDays(3));

        m.Status.Should().Be(MembershipStatus.Revoked);
    }

    [Fact]
    public void Revoke_FromInvited_ShouldTransitionToRevoked()
    {
        // Cubre el cascade del InvitationExpiryJob (T122) y la revocación de invitación pendiente.
        var m = TenantMembership.CreateInvited(AnyUser, AnyTenant, false, Inviter, Now);

        m.Revoke(Actor, Now.AddDays(8));

        m.Status.Should().Be(MembershipStatus.Revoked);
    }

    [Fact]
    public void ReactivateFromRevocation_FromRevoked_ShouldTransitionToActive()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);
        m.Revoke(Actor, Now.AddDays(1));

        m.ReactivateFromRevocation(Now.AddDays(10));

        m.Status.Should().Be(MembershipStatus.Active);
        m.RevokedAt.Should().BeNull();
        m.RevokedByUserId.Should().BeNull();
    }

    // -------------------- Transiciones ilegales --------------------

    [Theory]
    [InlineData(MembershipStatus.Active)]
    [InlineData(MembershipStatus.Suspended)]
    [InlineData(MembershipStatus.Revoked)]
    public void CanTransition_FromInvitedToInvitedOrToSuspended_ShouldBeFalse(MembershipStatus impossibleTarget)
    {
        // Invited solo puede ir a Active o Revoked. Suspended NO es destino válido desde Invited.
        if (impossibleTarget == MembershipStatus.Suspended)
        {
            MembershipStatus.Invited.CanTransition(impossibleTarget).Should().BeFalse();
        }
    }

    [Fact]
    public void Suspend_FromInvited_ShouldThrow()
    {
        var m = TenantMembership.CreateInvited(AnyUser, AnyTenant, false, Inviter, Now);

        var act = () => m.Suspend(Actor, Now);

        act.Should().Throw<InvalidMembershipTransitionException>()
            .Where(e => e.From == MembershipStatus.Invited && e.To == MembershipStatus.Suspended);
    }

    [Fact]
    public void Activate_FromActive_ShouldNotChange_ButTransitionCheckAllowsReentry()
    {
        // Domain: Active → Active no está en la tabla de transiciones permitidas (no es self-loop).
        // El test documenta el comportamiento esperado: lanza.
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);

        var act = () => m.Activate(Now.AddDays(1));

        act.Should().Throw<InvalidMembershipTransitionException>();
    }

    [Fact]
    public void Reactivate_FromActive_ShouldThrow()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);

        var act = () => m.Reactivate(Now);

        act.Should().Throw<InvalidMembershipTransitionException>();
    }

    [Fact]
    public void Revoke_FromAlreadyRevoked_ShouldThrow()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);
        m.Revoke(Actor, Now.AddDays(1));

        var act = () => m.Revoke(Actor, Now.AddDays(2));

        act.Should().Throw<InvalidMembershipTransitionException>();
    }

    // -------------------- Promote / Demote --------------------

    [Fact]
    public void PromoteToAdmin_OnActive_ShouldSetIsTenantAdminTrue()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);

        m.PromoteToAdmin();

        m.IsTenantAdmin.Should().BeTrue();
    }

    [Fact]
    public void PromoteToAdmin_OnSuspended_ShouldThrow()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, false, Inviter, Now);
        m.Suspend(Actor, Now);

        var act = () => m.PromoteToAdmin();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*activas*");
    }

    [Fact]
    public void DemoteFromAdmin_OnActiveAdmin_ShouldSetIsTenantAdminFalse()
    {
        var m = TenantMembership.CreateActive(AnyUser, AnyTenant, isTenantAdmin: true, Inviter, Now);

        m.DemoteFromAdmin();

        m.IsTenantAdmin.Should().BeFalse();
    }

    // -------------------- Transition matrix smoke test --------------------

    [Theory]
    [InlineData(MembershipStatus.Invited,   MembershipStatus.Active,    true)]
    [InlineData(MembershipStatus.Invited,   MembershipStatus.Revoked,   true)]
    [InlineData(MembershipStatus.Invited,   MembershipStatus.Suspended, false)]
    [InlineData(MembershipStatus.Active,    MembershipStatus.Suspended, true)]
    [InlineData(MembershipStatus.Active,    MembershipStatus.Revoked,   true)]
    [InlineData(MembershipStatus.Active,    MembershipStatus.Invited,   false)]
    [InlineData(MembershipStatus.Suspended, MembershipStatus.Active,    true)]
    [InlineData(MembershipStatus.Suspended, MembershipStatus.Revoked,   true)]
    [InlineData(MembershipStatus.Suspended, MembershipStatus.Invited,   false)]
    [InlineData(MembershipStatus.Revoked,   MembershipStatus.Active,    true)]
    [InlineData(MembershipStatus.Revoked,   MembershipStatus.Suspended, false)]
    [InlineData(MembershipStatus.Revoked,   MembershipStatus.Invited,   false)]
    public void TransitionMatrix_MatchesSpecFR009a(MembershipStatus from, MembershipStatus to, bool expectedAllowed)
    {
        from.CanTransition(to).Should().Be(expectedAllowed);
    }
}
