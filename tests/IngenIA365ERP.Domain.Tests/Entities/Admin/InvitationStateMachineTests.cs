using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.ValueObjects;

namespace IngenIA365ERP.Domain.Tests.Entities.Admin;

/// <summary>
/// Cubre la máquina de estados de <see cref="Invitation"/> (Pending → Accepted/Expired/Revoked).
/// Estados terminales no permiten más transiciones.
/// </summary>
public class InvitationStateMachineTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Inviter = Guid.NewGuid();
    private static readonly Guid Acceptor = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc);

    private static Invitation BuildPending(DateTime? expiresAt = null, bool asAdmin = false)
    {
        var token = InvitationToken.Generate();
        return Invitation.Create(
            email: "ana.perez@coop.coop",
            normalizedEmail: Email.Normalize("ana.perez@coop.coop"),
            tenantId: Tenant,
            invitedByUserId: Inviter,
            inviteAsTenantAdmin: asAdmin,
            tokenHash: token.Hash,
            createdAt: Now,
            expiresAt: expiresAt ?? Now.AddDays(7));
    }

    // -------------------- Constructor --------------------

    [Fact]
    public void Create_ShouldStartInPending_WithCorrectFields()
    {
        var inv = BuildPending();

        inv.Status.Should().Be(InvitationStatus.Pending);
        inv.Email.Should().Be("ana.perez@coop.coop");
        inv.NormalizedEmail.Should().Be("ANA.PEREZ@COOP.COOP");
        inv.TenantId.Should().Be(Tenant);
        inv.InvitedByUserId.Should().Be(Inviter);
        inv.InviteAsTenantAdmin.Should().BeFalse();
        inv.TokenHash.Should().HaveCount(InvitationToken.HashBytes);
        inv.ExpiresAt.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public void Create_WithExpiresBeforeCreated_ShouldThrow()
    {
        var token = InvitationToken.Generate();
        var act = () => Invitation.Create(
            "x@y.z", "X@Y.Z", Tenant, Inviter, false, token.Hash, Now, Now.AddSeconds(-1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ExpiresAt*");
    }

    [Fact]
    public void Create_WithEmptyHash_ShouldThrow()
    {
        var act = () => Invitation.Create(
            "x@y.z", "X@Y.Z", Tenant, Inviter, false, [], Now, Now.AddDays(7));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*TokenHash*");
    }

    // -------------------- IsValid --------------------

    [Fact]
    public void IsValid_PendingAndWithinExpiry_ReturnsTrue()
    {
        var inv = BuildPending();

        inv.IsValid(Now.AddDays(3)).Should().BeTrue();
    }

    [Fact]
    public void IsValid_PendingButExpired_ReturnsFalse()
    {
        var inv = BuildPending(expiresAt: Now.AddMinutes(1));

        inv.IsValid(Now.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void IsValid_NonPending_ReturnsFalse()
    {
        var inv = BuildPending();
        inv.Revoke(Inviter, Now);

        inv.IsValid(Now.AddMinutes(1)).Should().BeFalse();
    }

    // -------------------- MarkAccepted --------------------

    [Fact]
    public void MarkAccepted_FromPending_ShouldTransitionToAccepted()
    {
        var inv = BuildPending();

        inv.MarkAccepted(Acceptor, Now.AddHours(1));

        inv.Status.Should().Be(InvitationStatus.Accepted);
        inv.AcceptedAt.Should().Be(Now.AddHours(1));
        inv.AcceptedByCentralUserId.Should().Be(Acceptor);
    }

    [Fact]
    public void MarkAccepted_AfterExpiry_ShouldThrow()
    {
        var inv = BuildPending(expiresAt: Now.AddMinutes(5));

        var act = () => inv.MarkAccepted(Acceptor, Now.AddHours(1));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*expirado*");
    }

    [Fact]
    public void MarkAccepted_Twice_ShouldThrow_NoDoubleAcceptance()
    {
        var inv = BuildPending();
        inv.MarkAccepted(Acceptor, Now);

        var act = () => inv.MarkAccepted(Acceptor, Now.AddMinutes(1));

        act.Should().Throw<InvalidOperationException>();
        inv.Status.Should().Be(InvitationStatus.Accepted);  // sigue como Accepted, no se corrompe
    }

    // -------------------- Revoke --------------------

    [Fact]
    public void Revoke_FromPending_ShouldTransitionToRevoked()
    {
        var inv = BuildPending();

        inv.Revoke(Inviter, Now.AddHours(2));

        inv.Status.Should().Be(InvitationStatus.Revoked);
        inv.RevokedAt.Should().Be(Now.AddHours(2));
        inv.RevokedByUserId.Should().Be(Inviter);
    }

    [Fact]
    public void Revoke_FromAccepted_ShouldThrow()
    {
        var inv = BuildPending();
        inv.MarkAccepted(Acceptor, Now);

        var act = () => inv.Revoke(Inviter, Now.AddMinutes(1));

        act.Should().Throw<InvalidOperationException>();
    }

    // -------------------- MarkExpired --------------------

    [Fact]
    public void MarkExpired_FromPending_WhenAlreadyPastExpiry_ShouldTransition()
    {
        var inv = BuildPending(expiresAt: Now.AddMinutes(5));

        var result = inv.MarkExpired(Now.AddDays(8));

        result.Should().BeTrue();
        inv.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public void MarkExpired_OnAcceptedInvitation_ShouldReturnFalse_NoMutation()
    {
        var inv = BuildPending();
        inv.MarkAccepted(Acceptor, Now);

        var result = inv.MarkExpired(Now.AddDays(8));

        result.Should().BeFalse();
        inv.Status.Should().Be(InvitationStatus.Accepted);
    }

    [Fact]
    public void MarkExpired_BeforeExpiresAt_ShouldThrow()
    {
        var inv = BuildPending(expiresAt: Now.AddDays(7));

        var act = () => inv.MarkExpired(Now.AddDays(1));

        act.Should().Throw<InvalidOperationException>();
    }
}
