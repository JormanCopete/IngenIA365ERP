using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Profile.DisableMfa;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class DisableMfaCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;
    private readonly TestAdminDbContext _db;
    private readonly ITenantMembershipReader _memberships;

    public DisableMfaCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _db = TestAdminDbContext.Create();
        _memberships = Substitute.For<ITenantMembershipReader>();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(UserId);
        _currentUser.Email.Returns("user@coop.co");
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);

        _identity.ValidatePasswordAsync(UserId, "good-password", Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private DisableMfaCommandHandler NewHandler() => new(
        _currentUser, _identity, _db, _memberships,
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        NullLogger<DisableMfaCommandHandler>.Instance);

    [Fact]
    public async Task Password_incorrecta_rechaza()
    {
        _identity.ValidatePasswordAsync(UserId, "wrong", Arg.Any<CancellationToken>()).Returns(false);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ActiveMembershipInfo>());

        var result = await NewHandler().Handle(new DisableMfaCommand("wrong"), default);

        result.Error.Code.Should().Be("Identity.InvalidCredentials");
    }

    [Fact]
    public async Task Tenant_que_exige_MFA_rechaza_con_nombre_de_tenant()
    {
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, IsMfaRequiredByTenant: false),
                new ActiveMembershipInfo(TenantB, "Coop B", false, IsMfaRequiredByTenant: true),
            });

        var result = await NewHandler().Handle(new DisableMfaCommand("good-password"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.Mfa.RequiredByTenantPolicy");
        result.Error.Message.Should().Contain("Coop B");

        await _identity.DidNotReceive().DisableMfaAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_tenants_que_exigen_MFA_desactiva()
    {
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, IsMfaRequiredByTenant: false),
            });

        var result = await NewHandler().Handle(new DisableMfaCommand("good-password"), default);

        result.IsSuccess.Should().BeTrue();
        await _identity.Received(1).DisableMfaAsync(UserId, Arg.Any<CancellationToken>());
    }
}
