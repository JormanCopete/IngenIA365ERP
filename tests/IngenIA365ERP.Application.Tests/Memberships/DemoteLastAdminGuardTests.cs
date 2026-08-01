using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Memberships.DemoteFromTenantAdmin;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Memberships;

/// <summary>
/// T104 — Salvaguarda del último admin del tenant. Research D-09.
/// </summary>
public class DemoteLastAdminGuardTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AdminA = Guid.NewGuid();
    private static readonly Guid AdminB = Guid.NewGuid();

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly TestAdminDbContext _db;
    private readonly IMembershipChangedNotifier _notifier;

    public DemoteLastAdminGuardTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _db = TestAdminDbContext.Create();
        _notifier = Substitute.For<IMembershipChangedNotifier>();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(AdminA);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
        _currentUser.IsGlobalMasterAdmin.Returns(false);
        _currentUser.Email.Returns("a@x.co");
    }

    private DemoteFromTenantAdminCommandHandler NewHandler() => new(
        _currentUser, _db, _notifier,
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        NullLogger<DemoteFromTenantAdminCommandHandler>.Instance);

    private TenantMembership Seed(Guid userId, bool isAdmin, MembershipStatus status = MembershipStatus.Active)
    {
        var m = isAdmin
            ? TenantMembership.CreateActive(userId, TenantId, true, AdminA, FixedNow)
            : TenantMembership.CreateActive(userId, TenantId, false, AdminA, FixedNow);
        if (status != MembershipStatus.Active)
        {
            // Para tests que necesitan otros estados — no aplica aquí
        }
        _db.TenantMemberships.Add(m);
        _db.SaveChanges();
        return m;
    }

    [Fact]
    public async Task Un_admin_unico_no_se_puede_degradar()
    {
        var only = Seed(AdminA, isAdmin: true);

        var result = await NewHandler().Handle(
            new DemoteFromTenantAdminCommand(TenantId, only.PublicId), default);

        result.IsFailure.Should().BeTrue();
        // Auto-degrade del último → SelfDemoteBlocked.LastAdmin.
        result.Error.Code.Should().Be("Membership.SelfDemoteBlocked.LastAdmin");
    }

    [Fact]
    public async Task Dos_admins_uno_se_puede_degradar()
    {
        Seed(AdminA, isAdmin: true);
        var second = Seed(AdminB, isAdmin: true);

        var result = await NewHandler().Handle(
            new DemoteFromTenantAdminCommand(TenantId, second.PublicId), default);

        result.IsSuccess.Should().BeTrue();
        var refreshed = _db.TenantMemberships.Single(m => m.PublicId == second.PublicId);
        refreshed.IsTenantAdmin.Should().BeFalse();
        await _notifier.Received(1).PublishAsync(AdminB, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Auto_degrade_del_unico_admin_devuelve_codigo_especifico()
    {
        var only = Seed(AdminA, isAdmin: true);

        var result = await NewHandler().Handle(
            new DemoteFromTenantAdminCommand(TenantId, only.PublicId), default);

        result.Error.Code.Should().Be("Membership.SelfDemoteBlocked.LastAdmin");
    }
}
