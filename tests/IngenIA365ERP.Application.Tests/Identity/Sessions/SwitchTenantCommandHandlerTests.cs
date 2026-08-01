using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Sessions.SwitchTenant;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Sessions;

public class SwitchTenantCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private const string Email = "u@x.co";

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;
    private readonly ITenantMembershipReader _memberships;
    private readonly ICentralJwtIssuer _jwt;
    private readonly ICentralRefreshTokenStore _refresh;

    public SwitchTenantCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _memberships = Substitute.For<ITenantMembershipReader>();
        _jwt = Substitute.For<ICentralJwtIssuer>();
        _refresh = Substitute.For<ICentralRefreshTokenStore>();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(UserId);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
        _currentUser.ActiveTenantPublicId.Returns(TenantA);

        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = UserId, Email = Email });

        _jwt.IssueAccessToken(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<bool>())
            .Returns(new CentralAccessTokenResult("jwt", DateTime.UtcNow.AddMinutes(15), "jti", "full"));
        _jwt.IssueRefreshToken()
            .Returns(new CentralRefreshTokenResult("rt", "rh", DateTime.UtcNow.AddHours(12)));
    }

    private SwitchTenantCommandHandler NewHandler() => new(
        _currentUser, _identity, _memberships, _jwt, _refresh,
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        NullLogger<SwitchTenantCommandHandler>.Instance);

    [Fact]
    public async Task Membresia_no_activa_devuelve_NotActive()
    {
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActiveMembershipInfo(TenantA, "A", false, false) });

        var result = await NewHandler().Handle(new SwitchTenantCommand(TenantB), default);

        result.Error.Code.Should().Be("Membership.NotActive");
    }

    [Fact]
    public async Task Tenant_exige_MFA_y_user_sin_MFA_devuelve_MfaPolicyEnforced()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = UserId, Email = Email, TwoFactorEnabled = false });
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantB, "Coop B", false, IsMfaRequiredByTenant: true),
            });

        var result = await NewHandler().Handle(new SwitchTenantCommand(TenantB), default);

        result.Error.Code.Should().Be("Tenant.MfaPolicyEnforced");
        result.Error.Message.Should().Contain("Coop B");
    }

    [Fact]
    public async Task Switch_exitoso_emite_nuevos_tokens()
    {
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, false),
                new ActiveMembershipInfo(TenantB, "Coop B", true, false),
            });

        var result = await NewHandler().Handle(new SwitchTenantCommand(TenantB), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Tenant.TenantPublicId.Should().Be(TenantB);
        result.Value.Tenant.IsTenantAdmin.Should().BeTrue();
        await _refresh.Received(1).StoreAsync(
            Arg.Any<string>(), Arg.Any<CentralRefreshSession>(),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}
