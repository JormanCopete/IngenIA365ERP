using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Profile.ConfirmMfaEnrollment;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class ConfirmMfaEnrollmentCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private const string Email = "u@x.co";
    private const string Secret = "ABCDEFGHIJKLMNOP";

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;
    private readonly IMfaPendingStore _pending;
    private readonly ITenantMembershipReader _memberships;
    private readonly ICentralJwtIssuer _jwt;
    private readonly ICentralRefreshTokenStore _refresh;

    public ConfirmMfaEnrollmentCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _pending = Substitute.For<IMfaPendingStore>();
        _memberships = Substitute.For<ITenantMembershipReader>();
        _jwt = Substitute.For<ICentralJwtIssuer>();
        _refresh = Substitute.For<ICentralRefreshTokenStore>();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(UserId);
        _currentUser.Email.Returns(Email);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
    }

    private ConfirmMfaEnrollmentCommandHandler NewHandler() => new(
        _currentUser, _identity, _pending, _memberships, _jwt, _refresh,
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        NullLogger<ConfirmMfaEnrollmentCommandHandler>.Instance);

    [Fact]
    public async Task Sin_secret_pendiente_devuelve_NoPendingEnrollment()
    {
        _pending.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns((MfaPendingEnrollment?)null);

        var result = await NewHandler().Handle(new ConfirmMfaEnrollmentCommand("123456"), default);

        result.Error.Code.Should().Be("Profile.Mfa.NoPendingEnrollment");
    }

    [Fact]
    public async Task Codigo_invalido_devuelve_error()
    {
        _pending.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new MfaPendingEnrollment(Secret, DateTime.UtcNow));
        _identity.ConfirmMfaSetupAsync(UserId, Secret, "999999", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new MfaConfirmResult(false, new[] { "Profile.Mfa.InvalidCode" }));

        var result = await NewHandler().Handle(new ConfirmMfaEnrollmentCommand("999999"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Profile.Mfa.InvalidCode");
    }

    [Fact]
    public async Task Exito_purpose_full_devuelve_recovery_codes_sin_tokens()
    {
        var codes = new[] { "code1", "code2" };
        _pending.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new MfaPendingEnrollment(Secret, DateTime.UtcNow));
        _identity.ConfirmMfaSetupAsync(UserId, Secret, "123456", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new MfaConfirmResult(true, Array.Empty<string>(), codes));

        var result = await NewHandler().Handle(new ConfirmMfaEnrollmentCommand("123456"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecoveryCodes.Should().BeEquivalentTo(codes);
        result.Value.AccessToken.Should().BeNull("purpose=full → no se eleva sesión");
        await _pending.Received(1).ClearAsync(UserId, Arg.Any<CancellationToken>());
    }
}
