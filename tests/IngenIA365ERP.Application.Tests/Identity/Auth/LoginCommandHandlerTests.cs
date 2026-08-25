using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Auth.Login;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// Tests del state machine de <see cref="LoginCommandHandler"/> (T072 — los 9
/// casos del spec). Mock de TODA la infra excepto <see cref="IAdminDbContext"/>
/// que se materializa con EF Core InMemory (necesario para CentralUserLoginAttempt).
/// </summary>
public class LoginCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private const string Email = "ana@cooperativa.co";
    private const string Password = "P@ssw0rd-test-12";
    private const string NormalizedEmail = "ANA@COOPERATIVA.CO";
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly TestAdminDbContext _db;
    private readonly ICentralIdentityProvider _identity;
    private readonly ITenantMembershipReader _memberships;
    private readonly ILoginAttemptCounter _attempts;
    private readonly ICentralJwtIssuer _jwt;
    private readonly ICentralRefreshTokenStore _refresh;
    private readonly IAuditAppendOnlyWriter _audit;
    private readonly IDateTimeService _clock;

    public LoginCommandHandlerTests()
    {
        _db = TestAdminDbContext.Create();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _memberships = Substitute.For<ITenantMembershipReader>();
        _attempts = Substitute.For<ILoginAttemptCounter>();
        _jwt = Substitute.For<ICentralJwtIssuer>();
        _refresh = Substitute.For<ICentralRefreshTokenStore>();
        _audit = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        // Defaults: no lockout, tokens stub.
        _attempts.CheckAsync(AmbitoDeIntentos.Password, NormalizedEmail, Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutState(false, 0, 0));
        _jwt.IssueAccessToken(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<bool>())
            .Returns(new CentralAccessTokenResult("access-jwt", FixedNow.AddMinutes(15), "jti", "full"));
        _jwt.IssueChallengeToken(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<string>(), Arg.Any<TimeSpan?>())
            .Returns(callInfo => new CentralAccessTokenResult(
                "challenge-jwt", FixedNow.AddMinutes(5), "jti", callInfo.ArgAt<string>(3)));
        _jwt.IssueRefreshToken()
            .Returns(new CentralRefreshTokenResult("refresh-token", "refresh-hash", FixedNow.AddHours(12)));
    }

    // ----- Helpers de setup -----

    private void SetupValidPassword(
        Guid centralUserId, bool mfaEnabled = false, Guid? defaultTenantId = null,
        bool esMaestro = false)
    {
        var user = new CentralUser
        {
            Id = centralUserId,
            Email = Email,
            NormalizedEmail = NormalizedEmail,
            TwoFactorEnabled = mfaEnabled,
            DefaultTenantId = defaultTenantId,
            IsGlobalMasterAdmin = esMaestro,
        };
        _identity.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _identity.FindByIdAsync(centralUserId, Arg.Any<CancellationToken>()).Returns(user);
        _identity.ValidatePasswordAsync(centralUserId, Password, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private void SetupMemberships(params ActiveMembershipInfo[] memberships)
    {
        _memberships.GetActiveMembershipsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(memberships);
    }

    private LoginCommandHandler NewHandler() => new(
        centralIdentity: _identity,
        memberships: _memberships,
        attemptCounter: _attempts,
        jwtIssuer: _jwt,
        refreshStore: _refresh,
        adminDb: _db,
        auditWriter: _audit,
        clock: _clock,
        logger: NullLogger<LoginCommandHandler>.Instance);

    // ----- Tests -----

    [Fact]
    public async Task Lockout_activo_devuelve_Identity_Locked_Soft()
    {
        _attempts.CheckAsync(AmbitoDeIntentos.Password, NormalizedEmail, Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutState(true, 60, 5));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Locked.Soft");
        result.Error.Message.Should().Contain("60");
    }

    [Fact]
    public async Task Email_inexistente_devuelve_InvalidCredentials_e_incrementa_contador()
    {
        _identity.FindByEmailAsync(Email, Arg.Any<CancellationToken>())
            .Returns((CentralUser?)null);
        _attempts.RecordFailureAsync(AmbitoDeIntentos.Password, NormalizedEmail, Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutVerdict(false, 0, 1));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.InvalidCredentials");
        await _attempts.Received(1).RecordFailureAsync(AmbitoDeIntentos.Password, NormalizedEmail, Arg.Any<CancellationToken>());

        // El intento se registró en BD.
        _db.CentralUserLoginAttempts.Should().ContainSingle(a => a.Result == LoginAttemptResult.UserNotFound);
    }

    [Fact]
    public async Task Password_incorrecta_devuelve_InvalidCredentials_e_incrementa_contador()
    {
        var userId = Guid.NewGuid();
        var user = new CentralUser { Id = userId, Email = Email, NormalizedEmail = NormalizedEmail };
        _identity.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _identity.ValidatePasswordAsync(userId, Password, Arg.Any<CancellationToken>()).Returns(false);
        _attempts.RecordFailureAsync(AmbitoDeIntentos.Password, NormalizedEmail, Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutVerdict(false, 0, 2));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.InvalidCredentials");
        _db.CentralUserLoginAttempts.Should().ContainSingle(a => a.Result == LoginAttemptResult.InvalidPassword);
    }

    [Fact]
    public async Task Cero_membresias_devuelve_NoActiveMembership()
    {
        var userId = Guid.NewGuid();
        SetupValidPassword(userId);
        SetupMemberships();

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.NoActiveMembership);
        result.Value.AccessToken.Should().BeNull();
    }

    [Fact]
    public async Task El_maestro_sin_MFA_inscrito_no_recibe_sesion_sino_challenge_de_inscripcion()
    {
        // El maestro tiene el poder más grande del sistema —crear cooperativas,
        // apagar la política de MFA de una cooperativa ajena, borrar el segundo
        // factor de cualquier persona, y saltarse el filtro de permisos entero—
        // y entraba con sólo correo y contraseña. No es que «no llegara al gate»:
        // el return de cero membresías ocurría ANTES de los gates.
        var userId = Guid.NewGuid();
        SetupValidPassword(userId, mfaEnabled: false, esMaestro: true);
        SetupMemberships();

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.MfaEnrollmentRequired);
        result.Value.AccessToken.Should().BeNull("una contraseña no puede bastar para gobernar el SaaS");
        result.Value.RefreshToken.Should().BeNull();
        result.Value.ChallengeToken.Should().NotBeNullOrEmpty(
            "el challenge tiene que permitir inscribirse en el propio login; si no, el arreglo deja fuera al dueño");
    }

    [Fact]
    public async Task El_maestro_con_MFA_inscrito_tiene_que_verificarlo()
    {
        // Este era el caso más grave: un maestro que YA había activado su segundo
        // factor seguía entrando sin él, porque el mismo return se lo saltaba.
        // Incumplía el FR-003 en la cuenta que más importa.
        var userId = Guid.NewGuid();
        SetupValidPassword(userId, mfaEnabled: true, esMaestro: true);
        SetupMemberships();

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.MfaRequired);
        result.Value.AccessToken.Should().BeNull();
        result.Value.ChallengeTokenPurpose.Should().Be(CentralJwtPurposes.MfaVerify);
    }

    [Fact]
    public async Task Una_membresia_sin_MFA_autoSelected_direct_entry()
    {
        var userId = Guid.NewGuid();
        SetupValidPassword(userId);
        SetupMemberships(new ActiveMembershipInfo(TenantA, "Coop A", IsTenantAdmin: false, IsMfaRequiredByTenant: false));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.None);
        result.Value.AutoSelected.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();
        result.Value.ActiveTenantPublicId.Should().Be(TenantA);

        await _refresh.Received(1).StoreAsync(
            Arg.Any<string>(), Arg.Any<CentralRefreshSession>(),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_membresia_con_MFA_devuelve_MfaRequired()
    {
        var userId = Guid.NewGuid();
        SetupValidPassword(userId, mfaEnabled: true);
        SetupMemberships(new ActiveMembershipInfo(TenantA, "Coop A", false, false));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.MfaRequired);
        result.Value.ChallengeToken.Should().NotBeNullOrEmpty();
        result.Value.ChallengeTokenPurpose.Should().Be("mfa-verify");
        result.Value.AccessToken.Should().BeNull();
    }

    [Fact]
    public async Task Tenant_exige_MFA_y_usuario_sin_MFA_devuelve_MfaEnrollmentRequired()
    {
        var userId = Guid.NewGuid();
        SetupValidPassword(userId, mfaEnabled: false);
        SetupMemberships(
            new ActiveMembershipInfo(TenantA, "Coop A", false, IsMfaRequiredByTenant: true),
            new ActiveMembershipInfo(TenantB, "Coop B", false, IsMfaRequiredByTenant: false));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.MfaEnrollmentRequired);
        result.Value.ChallengeTokenPurpose.Should().Be("mfa-enroll");
        result.Value.TenantsRequiringMfa.Should().ContainSingle(t => t.TenantPublicId == TenantA);
    }

    [Fact]
    public async Task Multi_tenant_sin_default_devuelve_TenantSelection()
    {
        var userId = Guid.NewGuid();
        SetupValidPassword(userId, defaultTenantId: null);
        SetupMemberships(
            new ActiveMembershipInfo(TenantA, "Coop A", false, false),
            new ActiveMembershipInfo(TenantB, "Coop B", true, false));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.TenantSelection);
        result.Value.ChallengeTokenPurpose.Should().Be("tenant-select");
        result.Value.ActiveTenants.Should().HaveCount(2);
        result.Value.AutoSelected.Should().BeFalse();
        result.Value.AccessToken.Should().BeNull();
    }

    [Fact]
    public async Task Multi_tenant_con_default_valido_autoSelected_al_default()
    {
        var userId = Guid.NewGuid();
        SetupValidPassword(userId, defaultTenantId: TenantB);
        SetupMemberships(
            new ActiveMembershipInfo(TenantA, "Coop A", false, false),
            new ActiveMembershipInfo(TenantB, "Coop B", true, false));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.None);
        result.Value.AutoSelected.Should().BeTrue();
        result.Value.ActiveTenantPublicId.Should().Be(TenantB);
    }

    [Fact]
    public async Task Multi_tenant_con_default_zombi_limpia_y_cae_a_TenantSelection()
    {
        var userId = Guid.NewGuid();
        var zombieTenantId = Guid.NewGuid(); // no en activeMemberships
        SetupValidPassword(userId, defaultTenantId: zombieTenantId);
        SetupMemberships(
            new ActiveMembershipInfo(TenantA, "Coop A", false, false),
            new ActiveMembershipInfo(TenantB, "Coop B", true, false));

        var result = await NewHandler().Handle(new LoginCommand(Email, Password), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.TenantSelection);
        // Limpieza silenciosa del default zombi.
        await _identity.Received(1).SetDefaultTenantAsync(
            userId, null, Arg.Any<CancellationToken>());
    }
}
