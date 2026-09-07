using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Auth.Common;
using IngenIA365ERP.Application.Identity.Auth.Login;
using IngenIA365ERP.Application.Identity.Auth.MfaVerify;
using IngenIA365ERP.Application.Identity.Auth.Recuperacion;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

public class MfaVerifyCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantA = Guid.NewGuid();
    private const string Email = "ana@cooperativa.co";

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly ICentralIdentityProvider _identity;
    private readonly ITenantMembershipReader _memberships;
    private readonly ICentralJwtIssuer _jwt;
    private readonly ICentralRefreshTokenStore _refresh;
    private readonly IAuditAppendOnlyWriter _audit;
    private readonly IDateTimeService _clock;

    public MfaVerifyCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _memberships = Substitute.For<ITenantMembershipReader>();
        _jwt = Substitute.For<ICentralJwtIssuer>();
        _refresh = Substitute.For<ICentralRefreshTokenStore>();
        _audit = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        // Default: usuario autenticado con challenge token correcto (purpose=mfa-verify).
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(UserId);
        _currentUser.Email.Returns(Email);
        _currentUser.Purpose.Returns(CentralJwtPurposes.MfaVerify);
        _currentUser.IsGlobalMasterAdmin.Returns(false);

        _jwt.IssueAccessToken(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<MetodosMfa>())
            .Returns(new CentralAccessTokenResult("access-jwt", FixedNow.AddMinutes(15), "jti", "full"));
        _jwt.IssueChallengeToken(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<string>(), Arg.Any<MetodosMfa>(), Arg.Any<TimeSpan?>())
            .Returns(new CentralAccessTokenResult("challenge-jwt", FixedNow.AddMinutes(5), "jti", "tenant-select"));
        _jwt.IssueRefreshToken()
            .Returns(new CentralRefreshTokenResult("refresh-token", "refresh-hash", FixedNow.AddHours(12)));

        // Por defecto el contador no bloquea. Sin esto el sustituto devuelve null
        // y el handler revienta: la prueba tiene que decir que contesta, igual
        // que dice que contesta el proveedor de identidad.
        _intentos.CheckAsync(Arg.Any<AmbitoDeIntentos>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutState(IsLocked: false, RetryAfterSeconds: 0, FailureCount: 0));
        _intentos.RecordFailureAsync(Arg.Any<AmbitoDeIntentos>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LoginLockoutVerdict(ShouldLock: false, LockSeconds: 0, FailureCount: 1));

        _politicaPlataforma.MetodosAceptadosAsync(Arg.Any<CancellationToken>())
            .Returns(ConversionDeMetodosMfa.Todos);
    }

    /// <summary>
    /// Contador de intentos del segundo factor. Por defecto no bloquea: cada
    /// prueba que quiera el bloqueo lo dice explicitamente.
    /// </summary>
    private readonly ILoginAttemptCounter _intentos = Substitute.For<ILoginAttemptCounter>();

    /// <summary>
    /// Política de la plataforma. Se configura a «todos los métodos» en el
    /// constructor: el maestro no es el sujeto de estas pruebas, y una máscara
    /// restrictiva por accidente cambiaría su rama sin que ninguna de ellas lo
    /// dijera.
    /// </summary>
    private readonly IPoliticaDePlataforma _politicaPlataforma =
        Substitute.For<IPoliticaDePlataforma>();

    /// <summary>
    /// El emisor REAL, cableado con los mismos sustitutos.
    ///
    /// <para>
    /// La decisión de qué sesión emitir —auto-seleccionar, pedir cooperativa, o la
    /// salida del maestro global— se extrajo a su propia clase cuando el ingreso
    /// con passkey se convirtió en el segundo llamador. Sustituirla aquí dejaría
    /// estas pruebas afirmando que «se llamó al emisor», que no es lo que
    /// verifican: verifican qué sale por la puerta. Con el emisor real siguen
    /// probando exactamente lo mismo que antes de la extracción.
    /// </para>
    /// </summary>
    private IEmisorDeSesionTrasSegundoFactor Emisor() =>
        new EmisorDeSesionTrasSegundoFactor(
            _identity, _memberships, _jwt, _refresh, _politicaPlataforma,
            Substitute.For<ICanceladorDeRecuperacionesAlEntrar>(), _clock);

    private MfaVerifyCommandHandler NewHandler() => new(
        _currentUser, _identity, Emisor(), _audit, _intentos, _clock,
        NullLogger<MfaVerifyCommandHandler>.Instance);

    private CentralUser CreateUser(bool mfaEnabled = true) => new()
    {
        Id = UserId,
        Email = Email,
        TwoFactorEnabled = mfaEnabled,
    };

    [Fact]
    public async Task Codigo_valido_con_1_tenant_emite_tokens_operativos()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActiveMembershipInfo(TenantA, "Coop A", false, true) });

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.None);
        result.Value.AutoSelected.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Codigo_invalido_devuelve_MfaInvalid()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "999999", Arg.Any<CancellationToken>()).Returns(false);

        var result = await NewHandler().Handle(new MfaVerifyCommand("999999"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.MfaInvalid");
    }

    [Fact]
    public async Task Purpose_distinto_devuelve_WrongTokenPurpose()
    {
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.WrongTokenPurpose");
    }

    [Fact]
    public async Task Sin_autenticacion_devuelve_Unauthenticated()
    {
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.CentralUserId.Returns((Guid?)null);

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.Unauthenticated");
    }

    // -------- Feature 003 (US3): recovery codes --------

    [Fact]
    public async Task Recovery_code_valido_emite_tokens_y_reporta_restantes()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.RedeemRecoveryCodeAsync(UserId, "AB12-CD34", Arg.Any<CancellationToken>()).Returns(true);
        _identity.CountRecoveryCodesAsync(UserId, Arg.Any<CancellationToken>()).Returns(9);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActiveMembershipInfo(TenantA, "Coop A", false, true) });

        var result = await NewHandler().Handle(
            new MfaVerifyCommand("AB12-CD34", UseRecoveryCode: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.None);
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RecoveryCodesRemaining.Should().Be(9);
        await _identity.DidNotReceive().VerifyMfaCodeAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recovery_code_invalido_devuelve_MfaInvalid_generico()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.RedeemRecoveryCodeAsync(UserId, "XXXX-YYYY", Arg.Any<CancellationToken>()).Returns(false);

        var result = await NewHandler().Handle(
            new MfaVerifyCommand("XXXX-YYYY", UseRecoveryCode: true), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.MfaInvalid");
    }

    [Fact]
    public async Task Verificacion_TOTP_no_reporta_recovery_codes_restantes()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActiveMembershipInfo(TenantA, "Coop A", false, true) });

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecoveryCodesRemaining.Should().BeNull();
    }

    [Fact]
    public async Task Usuario_sin_MFA_devuelve_MfaNotEnabled()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser(mfaEnabled: false));

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.MfaNotEnabled");
    }

    // ---------- Política de métodos ----------

    /// <summary>
    /// El caso que convierte esta etapa en una mejora de seguridad o en un
    /// encierro, según cómo se resuelva.
    ///
    /// <para>
    /// Verifica bien con su TOTP —su identidad está probada— pero su única
    /// cooperativa sólo acepta passkeys. Lo que NO puede pasar es un fallo: se
    /// quedaría con un desafío ya consumido y sin ningún token con el que inscribir
    /// la llave que le exigen. Y tampoco un <c>tenant-select</c>: no hay ninguna
    /// otra cooperativa que elegir.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Con_TOTP_donde_solo_aceptan_passkeys_se_le_pide_inscribir_y_se_le_dice_cual()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(
                    TenantA, "Coop A", false, true, MetodosMfa.WebAuthn),
            });

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.IsSuccess.Should().BeTrue("un rechazo lo dejaría sin ningún token con el que arreglarlo");
        result.Value.Challenge.Should().Be(LoginChallenges.MfaEnrollmentRequired);
        result.Value.ChallengeTokenPurpose.Should().Be(CentralJwtPurposes.MfaEnroll);
        result.Value.ChallengeToken.Should().NotBeNullOrEmpty();
        result.Value.AccessToken.Should().BeNull("todavía no está dentro de ninguna cooperativa");
        result.Value.MetodosAceptados.Should().BeEquivalentTo(["WebAuthn"],
            "sin esto la pantalla ofrece los dos botones y el que no sirve lo encierra otra vez");
    }

    [Fact]
    public async Task Con_TOTP_donde_aceptan_TOTP_entra_normalmente()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, true, MetodosMfa.Totp),
            });

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.Value.Challenge.Should().Be(LoginChallenges.None);
        result.Value.AccessToken.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Un código de recuperación demuestra identidad pero no demuestra tener ningún
    /// método concreto — es literalmente lo que se usa cuando NO se tiene. Contra
    /// una cooperativa que restringe, lleva a inscribir.
    ///
    /// <para>
    /// Lo que se afirma aquí es que el código <b>no se malgasta</b>: se consumió, sí,
    /// pero a cambio de un token de inscripción. Consumirlo y devolver un error
    /// sería quemar uno de los diez que tiene sin darle nada.
    /// </para>
    /// </summary>
    [Fact]
    public async Task El_codigo_de_recuperacion_no_se_gasta_para_nada_cuando_la_cooperativa_restringe()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.RedeemRecoveryCodeAsync(UserId, "REC-123", Arg.Any<CancellationToken>()).Returns(true);
        _identity.CountRecoveryCodesAsync(UserId, Arg.Any<CancellationToken>()).Returns(9);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, true, MetodosMfa.WebAuthn),
            });

        var result = await NewHandler().Handle(
            new MfaVerifyCommand("REC-123", UseRecoveryCode: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Challenge.Should().Be(LoginChallenges.MfaEnrollmentRequired);
        result.Value.ChallengeToken.Should().NotBeNullOrEmpty(
            "el código se consumió; tiene que haber comprado algo");
    }

    /// <summary>
    /// Con la máscara sin restricciones, el código de recuperación sigue abriendo
    /// sesión como siempre. Esta etapa no cambia esa regla, y la prueba está para
    /// que no cambie por accidente: es el camino de vuelta de quien perdió el
    /// teléfono un viernes.
    /// </summary>
    [Fact]
    public async Task El_codigo_de_recuperacion_sigue_entrando_donde_no_se_restringe()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.RedeemRecoveryCodeAsync(UserId, "REC-123", Arg.Any<CancellationToken>()).Returns(true);
        _identity.CountRecoveryCodesAsync(UserId, Arg.Any<CancellationToken>()).Returns(9);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActiveMembershipInfo(TenantA, "Coop A", false, true) });

        var result = await NewHandler().Handle(
            new MfaVerifyCommand("REC-123", UseRecoveryCode: true), default);

        result.Value.Challenge.Should().Be(LoginChallenges.None);
        result.Value.RecoveryCodesRemaining.Should().Be(9);
    }

    /// <summary>
    /// Con dos cooperativas y sólo una que lo admite, se le enseña el selector con
    /// las dos y una marca de cuál sirve. Esconder la otra le quitaría la única
    /// pista de por qué desapareció.
    /// </summary>
    [Fact]
    public async Task Con_dos_cooperativas_se_listan_ambas_marcando_cual_lo_admite()
    {
        var tenantB = Guid.NewGuid();
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, true, MetodosMfa.WebAuthn),
                new ActiveMembershipInfo(tenantB, "Coop B", false, true, MetodosMfa.Totp),
            });

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.Value.Challenge.Should().Be(LoginChallenges.TenantSelection);
        result.Value.ActiveTenants.Should().HaveCount(2);
        result.Value.ActiveTenants!.Single(t => t.TenantPublicId == TenantA)
            .AdmiteTuMetodo.Should().BeFalse();
        result.Value.ActiveTenants!.Single(t => t.TenantPublicId == tenantB)
            .AdmiteTuMetodo.Should().BeTrue();
    }

    /// <summary>
    /// Y si NINGUNA lo admite, no se le enseña un selector donde toda opción falla:
    /// se le dice qué inscribir. La unión de lo que aceptan las exigentes.
    /// </summary>
    [Fact]
    public async Task Con_dos_cooperativas_y_ninguna_que_lo_admita_se_le_pide_inscribir()
    {
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _identity.VerifyMfaCodeAsync(UserId, "123456", Arg.Any<CancellationToken>()).Returns(true);
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, true, MetodosMfa.WebAuthn),
                new ActiveMembershipInfo(Guid.NewGuid(), "Coop B", false, true, MetodosMfa.WebAuthn),
            });

        var result = await NewHandler().Handle(new MfaVerifyCommand("123456"), default);

        result.Value.Challenge.Should().Be(LoginChallenges.MfaEnrollmentRequired);
        result.Value.MetodosAceptados.Should().BeEquivalentTo(["WebAuthn"]);
    }
}
