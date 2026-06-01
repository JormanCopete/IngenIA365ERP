using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Profile.SetDefaultTenant;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class SetDefaultTenantCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantA = Guid.NewGuid();

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;
    private readonly ITenantMembershipReader _memberships;

    public SetDefaultTenantCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _memberships = Substitute.For<ITenantMembershipReader>();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(UserId);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
    }

    private SetDefaultTenantCommandHandler NewHandler() => new(
        _currentUser, _identity, _memberships,
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        NullLogger<SetDefaultTenantCommandHandler>.Instance);

    [Fact]
    public async Task Tenant_sin_membresia_activa_devuelve_NotActiveMembership()
    {
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ActiveMembershipInfo>());

        var result = await NewHandler().Handle(new SetDefaultTenantCommand(TenantA), default);

        result.Error.Code.Should().Be("Profile.DefaultTenant.NotActiveMembership");
        await _identity.DidNotReceive().SetDefaultTenantAsync(
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tenant_con_membresia_activa_aplica()
    {
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActiveMembershipInfo(TenantA, "A", false, false) });

        var result = await NewHandler().Handle(new SetDefaultTenantCommand(TenantA), default);

        result.IsSuccess.Should().BeTrue();
        await _identity.Received(1).SetDefaultTenantAsync(
            UserId, TenantA, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Null_limpia_preferencia_sin_validar()
    {
        var result = await NewHandler().Handle(new SetDefaultTenantCommand(null), default);

        result.IsSuccess.Should().BeTrue();
        await _identity.Received(1).SetDefaultTenantAsync(
            UserId, null, Arg.Any<CancellationToken>());
    }
}
