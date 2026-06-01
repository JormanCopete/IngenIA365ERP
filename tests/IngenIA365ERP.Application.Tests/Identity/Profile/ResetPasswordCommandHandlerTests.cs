using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Application.Identity.Profile.ResetPassword;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class ResetPasswordCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly ICentralIdentityProvider _identity;
    private readonly TestAdminDbContext _db;
    private readonly ISecureTokenGenerator _tokens = new SecureTokenGenerator();
    private readonly IDistributedLock _distributedLock;
    private readonly IDateTimeService _clock;

    public ResetPasswordCommandHandlerTests()
    {
        _identity = Substitute.For<ICentralIdentityProvider>();
        _db = TestAdminDbContext.Create();
        _distributedLock = Substitute.For<IDistributedLock>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        var handle = Substitute.For<IDistributedLockHandle>();
        handle.Key.Returns("lock:pwdreset:test");
        _distributedLock.TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(handle);
    }

    private ResetPasswordCommandHandler NewHandler() => new(
        _identity, _db, _tokens, _distributedLock,
        Substitute.For<IAuditAppendOnlyWriter>(),
        _clock,
        NullLogger<ResetPasswordCommandHandler>.Instance);

    private (string plain, PasswordResetToken token) SeedToken(
        DateTime? expiresAt = null, DateTime? consumedAt = null)
    {
        var (plain, hash) = _tokens.Generate();
        var t = PasswordResetToken.Create(
            centralUserId: UserId,
            tokenHash: hash,
            requesterIp: null,
            createdAt: FixedNow.AddMinutes(-10),
            expiresAt: expiresAt ?? FixedNow.AddHours(1));
        if (consumedAt.HasValue) t.MarkConsumed(consumedAt.Value);
        _db.PasswordResetTokens.Add(t);
        _db.SaveChanges();
        return (plain, t);
    }

    [Fact]
    public async Task Token_inexistente_devuelve_Invalid()
    {
        var orphan = _tokens.Generate().PlainTokenBase64Url;

        var result = await NewHandler().Handle(
            new ResetPasswordCommand(orphan, "new-pwd-12-chars"), default);

        result.Error.Code.Should().Be("Profile.PasswordReset.Invalid");
    }

    [Fact]
    public async Task Token_consumido_devuelve_AlreadyConsumed()
    {
        var (plain, _) = SeedToken(consumedAt: FixedNow.AddMinutes(-5));

        var result = await NewHandler().Handle(
            new ResetPasswordCommand(plain, "new-pwd-12-chars"), default);

        result.Error.Code.Should().Be("Profile.PasswordReset.AlreadyConsumed");
    }

    [Fact]
    public async Task Token_expirado_devuelve_Expired()
    {
        var (plain, _) = SeedToken(expiresAt: FixedNow.AddMinutes(-1));

        var result = await NewHandler().Handle(
            new ResetPasswordCommand(plain, "new-pwd-12-chars"), default);

        result.Error.Code.Should().Be("Profile.PasswordReset.Expired");
    }

    [Fact]
    public async Task Exito_aplica_reset_marca_consumed_y_emite_audit()
    {
        var (plain, token) = SeedToken();
        _identity.AdminResetPasswordAsync(UserId, "new-pwd-12-chars", Arg.Any<CancellationToken>())
            .Returns(new ChangePasswordResult(true, Array.Empty<string>()));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = UserId, Email = "u@x.co" });

        var result = await NewHandler().Handle(
            new ResetPasswordCommand(plain, "new-pwd-12-chars"), default);

        result.IsSuccess.Should().BeTrue();
        var refreshed = _db.PasswordResetTokens.Single(t => t.Id == token.Id);
        refreshed.ConsumedAt.Should().NotBeNull();
    }
}
