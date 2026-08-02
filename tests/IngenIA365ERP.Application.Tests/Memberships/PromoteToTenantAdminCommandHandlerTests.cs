using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Memberships.PromoteToTenantAdmin;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Memberships;

public class PromoteToTenantAdminCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CallerId = Guid.NewGuid();
    private static readonly Guid TargetUserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly IMembershipChangedNotifier _notifier;
    private readonly IAuditAppendOnlyWriter _audit;
    private readonly IDateTimeService _clock;
    private readonly TestAdminDbContext _db;

    public PromoteToTenantAdminCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _notifier = Substitute.For<IMembershipChangedNotifier>();
        _audit = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);
        _db = TestAdminDbContext.Create();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(CallerId);
        _currentUser.Email.Returns("admin@cooperativa.co");
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
        _currentUser.IsGlobalMasterAdmin.Returns(true);
    }

    private PromoteToTenantAdminCommandHandler NewHandler() => new(
        _currentUser, _db, _notifier, _audit, _clock,
        NullLogger<PromoteToTenantAdminCommandHandler>.Instance);

    private async Task<TenantMembership> SeedMembershipAsync(
        bool isTenantAdmin = false, bool active = true)
    {
        var membership = active
            ? TenantMembership.CreateActive(TargetUserId, TenantId, isTenantAdmin, CallerId, FixedNow)
            : TenantMembership.CreateInvited(TargetUserId, TenantId, isTenantAdmin, CallerId, FixedNow);
        _db.TenantMemberships.Add(membership);
        await _db.SaveChangesAsync(default);
        return membership;
    }

    [Fact]
    public async Task Promocion_exitosa_marca_IsTenantAdmin_y_persiste()
    {
        var membership = await SeedMembershipAsync(isTenantAdmin: false);

        var result = await NewHandler().Handle(
            new PromoteToTenantAdminCommand(TenantId, membership.PublicId), default);

        result.IsSuccess.Should().BeTrue();
        membership.IsTenantAdmin.Should().BeTrue();
        membership.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Promocion_es_idempotente_si_ya_es_admin()
    {
        var membership = await SeedMembershipAsync(isTenantAdmin: true);

        var result = await NewHandler().Handle(
            new PromoteToTenantAdminCommand(TenantId, membership.PublicId), default);

        result.IsSuccess.Should().BeTrue();
        membership.IsTenantAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Promocion_exitosa_notifica_invalidacion_de_cache_de_membresias()
    {
        var membership = await SeedMembershipAsync(isTenantAdmin: false);

        var result = await NewHandler().Handle(
            new PromoteToTenantAdminCommand(TenantId, membership.PublicId), default);

        result.IsSuccess.Should().BeTrue();
        await _notifier.Received(1).PublishAsync(TargetUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Membresia_inexistente_devuelve_NotFound_y_no_notifica()
    {
        var result = await NewHandler().Handle(
            new PromoteToTenantAdminCommand(TenantId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Membership.NotFound");
        await _notifier.DidNotReceive().PublishAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Membresia_no_activa_devuelve_InvalidTransition()
    {
        var membership = await SeedMembershipAsync(isTenantAdmin: false, active: false);

        var result = await NewHandler().Handle(
            new PromoteToTenantAdminCommand(TenantId, membership.PublicId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Membership.InvalidTransition");
        await _notifier.DidNotReceive().PublishAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
