using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Application.Invitations.AcceptInvitation;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Invitations;

public class AcceptInvitationCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid InviterId = Guid.NewGuid();
    private const string InvitedEmail = "nuevo@cooperativa.co";

    private readonly TestAdminDbContext _db;
    private readonly ICentralIdentityProvider _identity;
    private readonly ICentralJwtIssuer _jwt;
    private readonly ITenantUserProvisioner _provisioner;
    private readonly IDistributedLock _distributedLock;
    private readonly ICentralRefreshTokenStore _refreshStore = Substitute.For<ICentralRefreshTokenStore>();
    private readonly ICurrentCentralUserContext _currentUser;
    private readonly IAuditAppendOnlyWriter _auditWriter;
    private readonly IDateTimeService _clock;
    private readonly ISecureTokenGenerator _tokens = new SecureTokenGenerator();

    public AcceptInvitationCommandHandlerTests()
    {
        _db = TestAdminDbContext.Create();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _jwt = Substitute.For<ICentralJwtIssuer>();
        _provisioner = Substitute.For<ITenantUserProvisioner>();
        _distributedLock = Substitute.For<IDistributedLock>();
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _auditWriter = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        SeedTenant();
        SetupDefaultLockAcquired();
        SetupDefaultJwtIssue();
        SetupDefaultProvisioner();
    }

    // ----- Rama NEW (registration) -----

    [Fact]
    public async Task Rama_nueva_crea_central_user_y_emite_JWT()
    {
        var (token, hash) = (_tokens.Generate().PlainTokenBase64Url, default(byte[])!);
        var (plain, h) = (token, _tokens.HashPlainToken(token));
        var invitation = SeedPendingInvitation(h);

        var newCentralUserId = Guid.NewGuid();
        _identity.FindByEmailAsync(InvitedEmail, Arg.Any<CancellationToken>())
            .Returns((Domain.Entities.Admin.CentralUser?)null);
        _identity.CreateUserAsync(InvitedEmail, "password-de-12-chars", true, Arg.Any<CancellationToken>())
            .Returns(new CreateCentralUserResult(true, newCentralUserId, Array.Empty<string>()));
        _identity.FindByIdAsync(newCentralUserId, Arg.Any<CancellationToken>())
            .Returns(new Domain.Entities.Admin.CentralUser
            {
                Id = newCentralUserId,
                Email = InvitedEmail,
                IsGlobalMasterAdmin = false
            });

        var handler = NewHandler();
        var result = await handler.Handle(
            new AcceptInvitationCommand(plain, Registration: new NewRegistrationInput("password-de-12-chars")),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CentralUserId.Should().Be(newCentralUserId);
        result.Value.ActiveTenantPublicId.Should().Be(TenantId);

        var refreshedInvitation = _db.Invitations.Single(i => i.PublicId == invitation.PublicId);
        refreshedInvitation.Status.Should().Be(InvitationStatus.Accepted);

        var membership = _db.TenantMemberships.Single(m => m.CentralUserId == newCentralUserId);
        membership.Status.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Rama_nueva_con_password_Pwned_falla()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        SeedPendingInvitation(_tokens.HashPlainToken(plain));

        _identity.FindByEmailAsync(InvitedEmail, Arg.Any<CancellationToken>())
            .Returns((Domain.Entities.Admin.CentralUser?)null);
        _identity.CreateUserAsync(InvitedEmail, Arg.Any<string>(), true, Arg.Any<CancellationToken>())
            .Returns(new CreateCentralUserResult(false, null, new[] { "Identity.Password.Pwned" }));

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, Registration: new NewRegistrationInput("password-comprometida")),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Password.Pwned");
    }

    // ----- Gate MFA en la aceptación (FR-003b/FR-003c) -----

    [Fact]
    public async Task Rama_nueva_con_politica_MFA_activa_devuelve_MfaEnrollmentRequired_sin_tokens()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        SeedPendingInvitation(_tokens.HashPlainToken(plain));

        var policy = TenantMfaPolicy.CreateForTenant(TenantId);
        policy.Enable(InviterId, FixedNow);
        _db.TenantMfaPolicies.Add(policy);
        _db.SaveChanges();

        var newCentralUserId = Guid.NewGuid();
        _identity.FindByEmailAsync(InvitedEmail, Arg.Any<CancellationToken>())
            .Returns((Domain.Entities.Admin.CentralUser?)null);
        _identity.CreateUserAsync(InvitedEmail, "password-de-12-chars", true, Arg.Any<CancellationToken>())
            .Returns(new CreateCentralUserResult(true, newCentralUserId, Array.Empty<string>()));
        _identity.FindByIdAsync(newCentralUserId, Arg.Any<CancellationToken>())
            .Returns(new Domain.Entities.Admin.CentralUser
            {
                Id = newCentralUserId,
                Email = InvitedEmail,
                TwoFactorEnabled = false,
            });
        _jwt.IssueChallengeToken(
                newCentralUserId, InvitedEmail, false, "mfa-enroll", Arg.Any<TimeSpan?>())
            .Returns(new CentralAccessTokenResult("enroll-challenge-jwt", FixedNow.AddMinutes(5), "jti", "mfa-enroll"));

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, Registration: new NewRegistrationInput("password-de-12-chars")),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be("MfaEnrollmentRequired");
        result.Value.ChallengeToken.Should().Be("enroll-challenge-jwt");
        result.Value.AccessToken.Should().BeNull();
        result.Value.RefreshToken.Should().BeNull();

        // La membresía queda activa igual — el gate solo retiene la sesión.
        _db.TenantMemberships.Single(m => m.CentralUserId == newCentralUserId)
            .Status.Should().Be(MembershipStatus.Active);
        await _refreshStore.DidNotReceive().StoreAsync(
            Arg.Any<string>(), Arg.Any<CentralRefreshSession>(),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rama_existente_con_MFA_activo_devuelve_MfaRequired_sin_tokens()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        SeedPendingInvitation(_tokens.HashPlainToken(plain));

        var existingId = Guid.NewGuid();
        var existing = new Domain.Entities.Admin.CentralUser
        {
            Id = existingId,
            Email = InvitedEmail,
            TwoFactorEnabled = true,
        };
        _identity.FindByEmailAsync(InvitedEmail, Arg.Any<CancellationToken>()).Returns(existing);
        _identity.FindByIdAsync(existingId, Arg.Any<CancellationToken>()).Returns(existing);
        _identity.ValidatePasswordAsync(existingId, "right-password", Arg.Any<CancellationToken>())
            .Returns(true);
        _jwt.IssueChallengeToken(
                existingId, InvitedEmail, false, "mfa-verify", Arg.Any<TimeSpan?>())
            .Returns(new CentralAccessTokenResult("verify-challenge-jwt", FixedNow.AddMinutes(5), "jti", "mfa-verify"));

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, ExistingCredentials: new ExistingCredentialsInput("right-password")),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be("MfaRequired");
        result.Value.ChallengeToken.Should().Be("verify-challenge-jwt");
        result.Value.AccessToken.Should().BeNull();
    }

    // ----- Rama EXISTING (credentials) -----

    [Fact]
    public async Task Rama_existente_valida_password_y_reutiliza_identidad()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        SeedPendingInvitation(_tokens.HashPlainToken(plain));

        var existingId = Guid.NewGuid();
        var existing = new Domain.Entities.Admin.CentralUser
        {
            Id = existingId,
            Email = InvitedEmail,
            IsGlobalMasterAdmin = false
        };
        _identity.FindByEmailAsync(InvitedEmail, Arg.Any<CancellationToken>()).Returns(existing);
        _identity.FindByIdAsync(existingId, Arg.Any<CancellationToken>()).Returns(existing);
        _identity.ValidatePasswordAsync(existingId, "right-password", Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, ExistingCredentials: new ExistingCredentialsInput("right-password")),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CentralUserId.Should().Be(existingId);
    }

    [Fact]
    public async Task Rama_existente_password_invalida_devuelve_InvalidCredentials()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        SeedPendingInvitation(_tokens.HashPlainToken(plain));

        var existingId = Guid.NewGuid();
        _identity.FindByEmailAsync(InvitedEmail, Arg.Any<CancellationToken>())
            .Returns(new Domain.Entities.Admin.CentralUser { Id = existingId, Email = InvitedEmail });
        _identity.ValidatePasswordAsync(existingId, "wrong", Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, ExistingCredentials: new ExistingCredentialsInput("wrong")),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.InvalidCredentials");
    }

    // ----- Token validation -----

    [Fact]
    public async Task Token_inexistente_devuelve_NotFound()
    {
        var orphanToken = _tokens.Generate().PlainTokenBase64Url;

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(orphanToken, Registration: new NewRegistrationInput("password-de-12-chars")),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.NotFound");
    }

    [Fact]
    public async Task Token_expirado_devuelve_Expired()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        var invitation = SeedPendingInvitation(_tokens.HashPlainToken(plain));
        // Adelantar el reloj más allá del expiry de la invitación.
        _clock.UtcNow.Returns(invitation.ExpiresAt.AddMinutes(1));

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, Registration: new NewRegistrationInput("password-de-12-chars")),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.Expired");
    }

    [Fact]
    public async Task Token_ya_aceptado_devuelve_AlreadyAccepted()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        var invitation = SeedPendingInvitation(_tokens.HashPlainToken(plain));
        invitation.MarkAccepted(Guid.NewGuid(), FixedNow);
        await _db.SaveChangesAsync();

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, Registration: new NewRegistrationInput("password-de-12-chars")),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.AlreadyAccepted");
    }

    [Fact]
    public async Task Lock_busy_devuelve_LockBusy()
    {
        var plain = _tokens.Generate().PlainTokenBase64Url;
        SeedPendingInvitation(_tokens.HashPlainToken(plain));

        _distributedLock
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((IDistributedLockHandle?)null);

        var result = await NewHandler().Handle(
            new AcceptInvitationCommand(plain, Registration: new NewRegistrationInput("password-de-12-chars")),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Invitation.LockBusy");
    }

    // ----- Helpers -----

    private void SeedTenant()
    {
        var tenant = new Domain.Entities.Admin.Tenant
        {
            PublicId = TenantId,
            Name = "Coop Test",
            SchemaName = "tenant_test",
            IsActive = true,
            ContactEmail = "admin@test.co",
        };
        _db.Tenants.Add(tenant);
        _db.SaveChanges();
    }

    private Invitation SeedPendingInvitation(byte[] tokenHash)
    {
        var invitation = Invitation.Create(
            email: InvitedEmail,
            normalizedEmail: InvitedEmail.ToUpperInvariant(),
            tenantId: TenantId,
            invitedByUserId: InviterId,
            inviteAsTenantAdmin: false,
            tokenHash: tokenHash,
            createdAt: FixedNow,
            expiresAt: FixedNow.AddDays(7));
        _db.Invitations.Add(invitation);
        _db.SaveChanges();
        return invitation;
    }

    private void SetupDefaultLockAcquired()
    {
        var handle = Substitute.For<IDistributedLockHandle>();
        handle.Key.Returns("lock:invitation:test");
        _distributedLock
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(handle);
    }

    private void SetupDefaultJwtIssue()
    {
        _jwt.IssueAccessToken(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<bool>())
            .Returns(new CentralAccessTokenResult("access-jwt", FixedNow.AddMinutes(15), "jti", "full"));

        _jwt.IssueRefreshToken()
            .Returns(new CentralRefreshTokenResult("refresh-token", "refresh-hash", FixedNow.AddHours(12)));
    }

    private void SetupDefaultProvisioner()
    {
        _provisioner.EnsureExistsAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(),
                Arg.Any<int>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(42));
    }

    private AcceptInvitationCommandHandler NewHandler() =>
        new(
            db: _db,
            centralIdentity: _identity,
            jwtIssuer: _jwt,
            refreshStore: _refreshStore,
            tenantUserProvisioner: _provisioner,
            distributedLock: _distributedLock,
            tokens: _tokens,
            clock: _clock,
            currentUser: _currentUser,
            auditWriter: _auditWriter,
            logger: NullLogger<AcceptInvitationCommandHandler>.Instance);
}
