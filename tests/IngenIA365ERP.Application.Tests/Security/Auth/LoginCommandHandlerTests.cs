using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Notifications.Contracts;
using IngenIA365ERP.Application.Security.Auth.Common;
using IngenIA365ERP.Application.Security.Auth.Login;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Auth;

public class LoginCommandHandlerTests
{
    private static (LoginCommandHandler Handler, TestApplicationDbContext Db,
                    IPasswordPolicyEnforcer Passwords, IMfaChallengeStore Challenges,
                    ISender Mediator)
        Build()
    {
        var db = TestDbContextFactory.Create();
        var passwords = Substitute.For<IPasswordPolicyEnforcer>();
        var challenges = Substitute.For<IMfaChallengeStore>();
        var mediator = Substitute.For<ISender>();
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc));

        challenges.IssueAsync(Arg.Any<MfaChallengeContext>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns("challenge-token");
        mediator.Send(Arg.Any<SendNotificationCommand>(), Arg.Any<CancellationToken>())
            .Returns(IngenIA365ERP.Application.Common.Models.Result.Success());

        var handler = new LoginCommandHandler(db, passwords, challenges, clock, mediator,
            NullLogger<LoginCommandHandler>.Instance);
        return (handler, db, passwords, challenges, mediator);
    }

    private static User SeedUser(TestApplicationDbContext db, bool isActive = true,
        bool mustChangePassword = false, int failedAttempts = 0, DateTime? lockoutEnd = null)
    {
        var user = new User
        {
            Username = "demo",
            Email = "demo@example.com",
            PasswordHash = "stored-hash",
            IsActive = isActive,
            MustChangePassword = mustChangePassword,
            FailedLoginAttempts = failedAttempts,
            LockoutEndAt = lockoutEnd
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_challenge_token()
    {
        var (handler, db, passwords, _, _) = Build();
        SeedUser(db);
        passwords.Verify("good", "stored-hash").Returns(true);

        var result = await handler.Handle(
            new LoginCommand(null, "demo", "good", "127.0.0.1", "ua"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MfaChallengeToken.Should().Be("challenge-token");
        result.Value.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public async Task Login_with_invalid_password_returns_invalid_credentials()
    {
        var (handler, db, passwords, _, _) = Build();
        SeedUser(db);
        passwords.Verify("bad", "stored-hash").Returns(false);

        var result = await handler.Handle(
            new LoginCommand(null, "demo", "bad", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_with_unknown_user_returns_invalid_credentials()
    {
        var (handler, _, passwords, _, _) = Build();
        passwords.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var result = await handler.Handle(
            new LoginCommand(null, "ghost", "x", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_when_user_is_disabled_returns_account_disabled()
    {
        var (handler, db, passwords, _, _) = Build();
        SeedUser(db, isActive: false);
        passwords.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var result = await handler.Handle(
            new LoginCommand(null, "demo", "x", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.AccountDisabled");
    }

    [Fact]
    public async Task Login_when_user_is_locked_out_returns_account_locked()
    {
        var (handler, db, _, _, _) = Build();
        SeedUser(db, lockoutEnd: new DateTime(2026, 5, 28, 13, 0, 0, DateTimeKind.Utc));

        var result = await handler.Handle(
            new LoginCommand(null, "demo", "x", null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.AccountLocked");
    }

    [Fact]
    public async Task Login_passes_must_change_password_flag_through()
    {
        var (handler, db, passwords, _, _) = Build();
        SeedUser(db, mustChangePassword: true);
        passwords.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var result = await handler.Handle(
            new LoginCommand(null, "demo", "good", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MustChangePassword.Should().BeTrue();
    }

    [Fact]
    public async Task Login_lockout_threshold_locks_account_and_notifies()
    {
        var (handler, db, passwords, _, mediator) = Build();
        // Política dev (LockoutThreshold = 5) — el cuarto fallo todavía no bloquea.
        SeedUser(db, failedAttempts: 4);
        passwords.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await handler.Handle(
            new LoginCommand(null, "demo", "bad", null, null), CancellationToken.None);

        var user = db.Users.Single();
        user.FailedLoginAttempts.Should().Be(5);
        user.LockoutEndAt.Should().NotBeNull();

        await mediator.Received(1).Send(
            Arg.Is<SendNotificationCommand>(c => c.Payload.Type == NotificationType.AccountLocked),
            Arg.Any<CancellationToken>());
    }
}
