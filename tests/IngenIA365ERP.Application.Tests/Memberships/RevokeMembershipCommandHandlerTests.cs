using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Memberships.RevokeMembership;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Memberships;

public class RevokeMembershipCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AdminA = Guid.NewGuid();
    private static readonly Guid AdminB = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly TestAdminDbContext _db;

    public RevokeMembershipCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _db = TestAdminDbContext.Create();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(AdminA);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
        _currentUser.IsGlobalMasterAdmin.Returns(true); // master para evitar guard
    }

    private RevokeMembershipCommandHandler NewHandler() => new(
        _currentUser, _db,
        Substitute.For<IMembershipChangedNotifier>(),
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        NullLogger<RevokeMembershipCommandHandler>.Instance);

    [Fact]
    public async Task Revoca_miembro_regular_OK()
    {
        var m = TenantMembership.CreateActive(Member, TenantId, false, AdminA, DateTime.UtcNow);
        _db.TenantMemberships.Add(m);
        _db.SaveChanges();

        var result = await NewHandler().Handle(
            new RevokeMembershipCommand(TenantId, m.PublicId), default);

        result.IsSuccess.Should().BeTrue();
        _db.TenantMemberships.Single(x => x.PublicId == m.PublicId).Status
            .Should().Be(MembershipStatus.Revoked);
    }

    [Fact]
    public async Task Revoca_unico_admin_devuelve_LastAdminProtected()
    {
        var m = TenantMembership.CreateActive(AdminB, TenantId, true, AdminA, DateTime.UtcNow);
        _db.TenantMemberships.Add(m);
        _db.SaveChanges();

        var result = await NewHandler().Handle(
            new RevokeMembershipCommand(TenantId, m.PublicId), default);

        result.Error.Code.Should().Be("Membership.LastAdminProtected");
    }

    [Fact]
    public async Task Revoca_admin_cuando_hay_otro_OK()
    {
        _db.TenantMemberships.Add(TenantMembership.CreateActive(AdminA, TenantId, true, AdminA, DateTime.UtcNow));
        var second = TenantMembership.CreateActive(AdminB, TenantId, true, AdminA, DateTime.UtcNow);
        _db.TenantMemberships.Add(second);
        _db.SaveChanges();

        var result = await NewHandler().Handle(
            new RevokeMembershipCommand(TenantId, second.PublicId), default);

        result.IsSuccess.Should().BeTrue();
    }
}
