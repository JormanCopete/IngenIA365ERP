using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Security.Auth.VerifyMfa;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Auth;

public class VerifyMfaCommandHandlerTests
{
    private static (VerifyMfaCommandHandler Handler, TestApplicationDbContext Db,
                    IMfaChallengeStore Challenges, ITotpService Totp,
                    IMfaBackupCodeGenerator BackupCodes, IAccessTokenIssuer Tokens,
                    IRefreshTokenStore RefreshStore)
        Build(int userId = 1, bool mfaEnabled = true, string? protectedSecret = "protected")
    {
        var db = TestDbContextFactory.Create();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "demo",
            PasswordHash = "hash",
            IsActive = true,
            IsMfaEnabled = mfaEnabled,
            MfaSecret = protectedSecret
        });
        db.SaveChanges();

        var challenges = Substitute.For<IMfaChallengeStore>();
        var totp = Substitute.For<ITotpService>();
        var backupCodes = Substitute.For<IMfaBackupCodeGenerator>();
        var tokens = Substitute.For<IAccessTokenIssuer>();
        var refreshStore = Substitute.For<IRefreshTokenStore>();
        var now = new DateTime(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(now);

        tokens.IssueAccessToken(Arg.Any<AccessTokenClaims>())
            .Returns(new AccessTokenIssueResult("access-jwt", now.AddMinutes(30), "jti"));
        tokens.IssueRefreshToken()
            .Returns(new RefreshTokenIssueResult("rt", "rt-hash", now.AddHours(12)));

        var handler = new VerifyMfaCommandHandler(
            db, challenges, totp, backupCodes, tokens, refreshStore, clock);
        return (handler, db, challenges, totp, backupCodes, tokens, refreshStore);
    }

    [Fact]
    public async Task Returns_expired_when_challenge_is_unknown()
    {
        var (handler, _, challenges, _, _, _, _) = Build();
        challenges.ConsumeAsync("bad", Arg.Any<CancellationToken>())
            .Returns((MfaChallengeContext?)null);

        var result = await handler.Handle(
            new VerifyMfaCommand("bad", "123456", false, null, null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaChallengeExpired");
    }

    [Fact]
    public async Task Returns_invalid_totp_when_code_does_not_match()
    {
        var (handler, _, challenges, totp, _, _, _) = Build();
        challenges.ConsumeAsync("ok", Arg.Any<CancellationToken>())
            .Returns(new MfaChallengeContext(1, Guid.NewGuid(), "demo", null, Guid.NewGuid(),
                "demo-tenant", DateTime.UtcNow));
        totp.UnprotectSecret("protected").Returns("plain-secret");
        totp.Verify("plain-secret", "000000").Returns(false);

        var result = await handler.Handle(
            new VerifyMfaCommand("ok", "000000", false, null, null, null, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidMfaCode");
    }

    [Fact]
    public async Task Returns_tokens_when_totp_is_valid_and_within_window()
    {
        var (handler, db, challenges, totp, _, _, _) = Build();
        challenges.ConsumeAsync("ok", Arg.Any<CancellationToken>())
            .Returns(new MfaChallengeContext(1, db.Users.Single().PublicId, "demo", null, Guid.NewGuid(),
                "demo-tenant", DateTime.UtcNow));
        totp.UnprotectSecret("protected").Returns("plain-secret");
        totp.Verify("plain-secret", "123456").Returns(true);

        var result = await handler.Handle(
            new VerifyMfaCommand("ok", "123456", false, null, null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-jwt");
        result.Value.RefreshToken.Should().Be("rt");
        db.RefreshTokens.Should().HaveCount(1);
    }

    [Fact]
    public async Task Backup_code_is_single_use()
    {
        var (handler, db, challenges, _, backupCodes, _, _) = Build();
        var batchId = Guid.NewGuid();
        var code = new MfaBackupCode { UserId = 1, BatchId = batchId, CodeHash = "bcrypt-hash" };
        db.MfaBackupCodes.Add(code);
        db.SaveChanges();

        challenges.ConsumeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MfaChallengeContext(1, db.Users.Single().PublicId, "demo", null, Guid.NewGuid(),
                "demo-tenant", DateTime.UtcNow),
                new MfaChallengeContext(1, db.Users.Single().PublicId, "demo", null, Guid.NewGuid(),
                "demo-tenant", DateTime.UtcNow));
        backupCodes.Verify("ABCDE-FGHJK", "bcrypt-hash").Returns(true);

        // 1) Primer uso → éxito
        var r1 = await handler.Handle(
            new VerifyMfaCommand("t1", null, true, "ABCDE-FGHJK", null, null, null),
            CancellationToken.None);
        r1.IsSuccess.Should().BeTrue();
        db.MfaBackupCodes.Single().UsedAt.Should().NotBeNull();

        // 2) Segundo uso del mismo código → rechazo (ya está marcado UsedAt)
        var r2 = await handler.Handle(
            new VerifyMfaCommand("t2", null, true, "ABCDE-FGHJK", null, null, null),
            CancellationToken.None);
        r2.IsFailure.Should().BeTrue();
        r2.Error.Code.Should().Be("Auth.InvalidBackupCode");
    }
}
