using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Saas.ForceMfaReset;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Saas;

public class ForceMfaResetCommandHandlerTests
{
    private static readonly Guid MasterId = Guid.NewGuid();
    private static readonly Guid TargetId = Guid.NewGuid();

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;

    public ForceMfaResetCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(MasterId);
        _currentUser.IsGlobalMasterAdmin.Returns(true);
        _currentUser.Email.Returns("master@saas.co");
    }

    private ForceMfaResetCommandHandler NewHandler() => new(
        _currentUser, _identity,
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        NullLogger<ForceMfaResetCommandHandler>.Instance);

    [Fact]
    public async Task No_master_rechaza()
    {
        _currentUser.IsGlobalMasterAdmin.Returns(false);

        var result = await NewHandler().Handle(
            new ForceMfaResetCommand(TargetId, "razón válida del reset"), default);

        result.Error.Code.Should().Be("Saas.MasterOnly");
    }

    [Fact]
    public async Task Usuario_inexistente_devuelve_UserNotFound()
    {
        _identity.FindByIdAsync(TargetId, Arg.Any<CancellationToken>()).Returns((CentralUser?)null);

        var result = await NewHandler().Handle(
            new ForceMfaResetCommand(TargetId, "razón válida del reset"), default);

        result.Error.Code.Should().Be("Identity.UserNotFound");
    }

    [Fact]
    public async Task Reset_exitoso_invoca_provider()
    {
        _identity.FindByIdAsync(TargetId, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = TargetId, Email = "u@x.co" });

        var result = await NewHandler().Handle(
            new ForceMfaResetCommand(TargetId, "perdió el celular durante audit"), default);

        result.IsSuccess.Should().BeTrue();
        await _identity.Received(1).ResetMfaAsync(TargetId, Arg.Any<CancellationToken>());
    }
}
