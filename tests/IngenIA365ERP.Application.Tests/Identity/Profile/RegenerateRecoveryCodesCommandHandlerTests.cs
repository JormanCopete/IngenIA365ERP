using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Profile.RegenerateRecoveryCodes;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class RegenerateRecoveryCodesCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private const string Email = "gina@cooperativa.co";

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;
    private readonly IAuditAppendOnlyWriter _audit;
    private readonly IDateTimeService _clock;

    public RegenerateRecoveryCodesCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _audit = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(UserId);
        _currentUser.Email.Returns(Email);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
    }

    private RegenerateRecoveryCodesCommandHandler NewHandler() => new(
        _currentUser, _identity, _audit, _clock,
        NullLogger<RegenerateRecoveryCodesCommandHandler>.Instance);

    private CentralUser CreateUser(bool mfaEnabled = true) => new()
    {
        Id = UserId,
        Email = Email,
        TwoFactorEnabled = mfaEnabled,
    };

    private static readonly IReadOnlyList<string> NewCodes =
        ["AA11-BB22", "CC33-DD44", "EE55-FF66"];

    [Fact]
    public async Task Con_password_valido_regenera_y_devuelve_codigos()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.ValidatePasswordAsync(UserId, "Pass-123!", Arg.Any<CancellationToken>()).Returns(true);
        _identity.RegenerateRecoveryCodesAsync(UserId, Arg.Any<CancellationToken>()).Returns(NewCodes);

        var result = await NewHandler().Handle(
            new RegenerateRecoveryCodesCommand(CurrentPassword: "Pass-123!"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecoveryCodes.Should().BeEquivalentTo(NewCodes);
    }

    [Fact]
    public async Task Con_TOTP_valido_regenera_sin_password()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _identity.RegenerateRecoveryCodesAsync(UserId, Arg.Any<CancellationToken>()).Returns(NewCodes);

        var result = await NewHandler().Handle(
            new RegenerateRecoveryCodesCommand(TotpCode: "123456"), default);

        result.IsSuccess.Should().BeTrue();
        await _identity.DidNotReceive().ValidatePasswordAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Password_incorrecto_devuelve_InvalidCredentials()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.ValidatePasswordAsync(UserId, "mala", Arg.Any<CancellationToken>()).Returns(false);

        var result = await NewHandler().Handle(
            new RegenerateRecoveryCodesCommand(CurrentPassword: "mala"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.InvalidCredentials");
        await _identity.DidNotReceive().RegenerateRecoveryCodesAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_MFA_activo_devuelve_NotEnabled()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser(mfaEnabled: false));

        var result = await NewHandler().Handle(
            new RegenerateRecoveryCodesCommand(CurrentPassword: "Pass-123!"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.Mfa.NotEnabled");
    }

    [Fact]
    public async Task Purpose_distinto_de_full_devuelve_WrongTokenPurpose()
    {
        _currentUser.Purpose.Returns(CentralJwtPurposes.MfaVerify);

        var result = await NewHandler().Handle(
            new RegenerateRecoveryCodesCommand(CurrentPassword: "Pass-123!"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.WrongTokenPurpose");
    }

    [Fact]
    public void Validator_exige_exactamente_un_factor()
    {
        var validator = new RegenerateRecoveryCodesCommandValidator();

        validator.Validate(new RegenerateRecoveryCodesCommand()).IsValid.Should().BeFalse();
        validator.Validate(new RegenerateRecoveryCodesCommand("pass", "123456")).IsValid.Should().BeFalse();
        validator.Validate(new RegenerateRecoveryCodesCommand(CurrentPassword: "pass")).IsValid.Should().BeTrue();
        validator.Validate(new RegenerateRecoveryCodesCommand(TotpCode: "123456")).IsValid.Should().BeTrue();
    }
}
