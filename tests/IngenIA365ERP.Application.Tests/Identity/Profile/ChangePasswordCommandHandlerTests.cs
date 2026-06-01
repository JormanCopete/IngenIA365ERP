using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Identity.Profile.ChangePassword;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class ChangePasswordCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private const string Email = "ana@cooperativa.co";

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;
    private readonly IPasswordChangedNotifier _notifier;
    private readonly IAuditAppendOnlyWriter _audit;
    private readonly IDateTimeService _clock;

    public ChangePasswordCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _notifier = Substitute.For<IPasswordChangedNotifier>();
        _audit = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(new DateTime(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc));

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(UserId);
        _currentUser.Email.Returns(Email);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);

        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = UserId, Email = Email });
    }

    private ChangePasswordCommandHandler NewHandler() => new(
        _currentUser, _identity, _notifier, _audit, _clock,
        NullLogger<ChangePasswordCommandHandler>.Instance);

    [Fact]
    public async Task Sin_autenticacion_rechaza()
    {
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.CentralUserId.Returns((Guid?)null);

        var result = await NewHandler().Handle(
            new ChangePasswordCommand("old", "new-12-chars-long"), default);

        result.Error.Code.Should().Be("Identity.Unauthenticated");
    }

    [Fact]
    public async Task Current_password_invalida_devuelve_InvalidCredentials()
    {
        _identity.ChangePasswordAsync(UserId, "wrong", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(false, new[] { "Identity.PasswordMismatch" }));

        var result = await NewHandler().Handle(
            new ChangePasswordCommand("wrong", "new-12-chars-long"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.PasswordMismatch");
    }

    [Fact]
    public async Task Password_Pwned_devuelve_error_Pwned()
    {
        _identity.ChangePasswordAsync(UserId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(false, new[] { "Identity.Password.Pwned" }));

        var result = await NewHandler().Handle(
            new ChangePasswordCommand("old", "weak-password-pwned"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Password.Pwned");
    }

    [Fact]
    public async Task Cambio_exitoso_dispara_notificacion_fail_soft()
    {
        _identity.ChangePasswordAsync(UserId, "old", "new-12-chars-long", Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(true, Array.Empty<string>()));

        var result = await NewHandler().Handle(
            new ChangePasswordCommand("old", "new-12-chars-long", IpAddress: "1.2.3.4"), default);

        result.IsSuccess.Should().BeTrue();
        await _notifier.Received(1).NotifyAsync(
            Email, Arg.Any<string>(), Arg.Any<DateTime>(),
            "1.2.3.4", Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cambio_exitoso_aunque_notificacion_falle()
    {
        _identity.ChangePasswordAsync(UserId, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(true, Array.Empty<string>()));
        _notifier.NotifyAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("SMTP fail")));

        var result = await NewHandler().Handle(
            new ChangePasswordCommand("old", "new-12-chars-long"), default);

        result.IsSuccess.Should().BeTrue("notificación es fail-soft");
    }
}
