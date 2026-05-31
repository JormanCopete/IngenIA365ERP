using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Invitations.RevokeInvitation;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Invitations;

public class RevokeInvitationCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid MasterAdmin = Guid.NewGuid();
    private static readonly Guid AdminOfA = Guid.NewGuid();
    private static readonly Guid AdminOfB = Guid.NewGuid();
    private static readonly Guid RegularMember = Guid.NewGuid();

    private readonly TestAdminDbContext _db;
    private readonly IDateTimeService _clock;

    public RevokeInvitationCommandHandlerTests()
    {
        _db = TestAdminDbContext.Create();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        // Seed: TenantMemberships con admins de A y B (activos).
        _db.TenantMemberships.Add(TenantMembership.CreateActive(
            AdminOfA, TenantA, isTenantAdmin: true, invitedByUserId: MasterAdmin, FixedNow));
        _db.TenantMemberships.Add(TenantMembership.CreateActive(
            AdminOfB, TenantB, isTenantAdmin: true, invitedByUserId: MasterAdmin, FixedNow));
        _db.TenantMemberships.Add(TenantMembership.CreateActive(
            RegularMember, TenantA, isTenantAdmin: false, invitedByUserId: AdminOfA, FixedNow));
        _db.SaveChanges();
    }

    [Fact]
    public async Task Master_admin_puede_revocar_invitacion_de_cualquier_tenant()
    {
        var invitation = SeedPendingInvitation(TenantA, "x@y.co", invitedBy: AdminOfA);
        var handler = NewHandler(callerCentralUserId: MasterAdmin, isMaster: true);

        var result = await handler.Handle(new RevokeInvitationCommand(invitation.PublicId), default);

        result.IsSuccess.Should().BeTrue();
        var refreshed = _db.Invitations.Single(i => i.PublicId == invitation.PublicId);
        refreshed.Status.Should().Be(InvitationStatus.Revoked);
        refreshed.RevokedByUserId.Should().Be(MasterAdmin);
    }

    [Fact]
    public async Task Tenant_admin_del_mismo_tenant_puede_revocar()
    {
        var invitation = SeedPendingInvitation(TenantA, "x@y.co", invitedBy: AdminOfA);
        var handler = NewHandler(callerCentralUserId: AdminOfA, isMaster: false);

        var result = await handler.Handle(new RevokeInvitationCommand(invitation.PublicId), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Tenant_admin_de_otro_tenant_recibe_Forbidden()
    {
        var invitation = SeedPendingInvitation(TenantA, "x@y.co", invitedBy: AdminOfA);
        var handler = NewHandler(callerCentralUserId: AdminOfB, isMaster: false);

        var result = await handler.Handle(new RevokeInvitationCommand(invitation.PublicId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.Forbidden");
    }

    [Fact]
    public async Task Miembro_regular_recibe_Forbidden()
    {
        var invitation = SeedPendingInvitation(TenantA, "x@y.co", invitedBy: AdminOfA);
        var handler = NewHandler(callerCentralUserId: RegularMember, isMaster: false);

        var result = await handler.Handle(new RevokeInvitationCommand(invitation.PublicId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.Forbidden");
    }

    [Fact]
    public async Task Invitacion_ya_aceptada_no_se_puede_revocar()
    {
        var invitation = SeedPendingInvitation(TenantA, "x@y.co", invitedBy: AdminOfA);
        invitation.MarkAccepted(Guid.NewGuid(), FixedNow);
        await _db.SaveChangesAsync();

        var handler = NewHandler(callerCentralUserId: MasterAdmin, isMaster: true);
        var result = await handler.Handle(new RevokeInvitationCommand(invitation.PublicId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.NotPending");
        result.Error.Message.Should().Contain("ya fue aceptada");
    }

    [Fact]
    public async Task Invitacion_inexistente_devuelve_NotFound()
    {
        var handler = NewHandler(callerCentralUserId: MasterAdmin, isMaster: true);

        var result = await handler.Handle(new RevokeInvitationCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.NotFound");
    }

    [Fact]
    public async Task Sin_autenticacion_devuelve_Unauthenticated()
    {
        var invitation = SeedPendingInvitation(TenantA, "x@y.co", invitedBy: AdminOfA);

        var unauthenticated = Substitute.For<ICurrentCentralUserContext>();
        unauthenticated.CentralUserId.Returns((Guid?)null);
        unauthenticated.IsAuthenticated.Returns(false);

        var handler = new RevokeInvitationCommandHandler(
            unauthenticated, _db, _clock, NullLogger<RevokeInvitationCommandHandler>.Instance);

        var result = await handler.Handle(new RevokeInvitationCommand(invitation.PublicId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Unauthenticated");
    }

    // ----- Helpers -----

    private Invitation SeedPendingInvitation(Guid tenantId, string email, Guid invitedBy)
    {
        var normalized = email.ToUpperInvariant();
        var invitation = Invitation.Create(
            email: email,
            normalizedEmail: normalized,
            tenantId: tenantId,
            invitedByUserId: invitedBy,
            inviteAsTenantAdmin: false,
            tokenHash: System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),
            createdAt: FixedNow,
            expiresAt: FixedNow.AddDays(7));
        _db.Invitations.Add(invitation);
        _db.SaveChanges();
        return invitation;
    }

    private RevokeInvitationCommandHandler NewHandler(Guid callerCentralUserId, bool isMaster)
    {
        var currentUser = Substitute.For<ICurrentCentralUserContext>();
        currentUser.CentralUserId.Returns(callerCentralUserId);
        currentUser.IsAuthenticated.Returns(true);
        currentUser.IsGlobalMasterAdmin.Returns(isMaster);
        currentUser.Email.Returns($"{callerCentralUserId:N}@test.local");

        return new RevokeInvitationCommandHandler(
            currentUser, _db, _clock, NullLogger<RevokeInvitationCommandHandler>.Instance);
    }
}
