using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Profile.Common;
using IngenIA365ERP.Application.Identity.Profile.ConfirmMfaEnrollment;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class ConfirmMfaEnrollmentCommandHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private const string Email = "u@x.co";
    private const string Secret = "ABCDEFGHIJKLMNOP";
    private static readonly Guid TenantA = Guid.NewGuid();

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

    /// <summary>
    /// El elevador va REAL, armado con los mismos sustitutos. Un doble diría
    /// «se llamó al elevador», que es una afirmación sobre la estructura del
    /// código; así las pruebas siguen afirmando qué sale por la puerta.
    /// </summary>
    private IElevadorDeSesionTrasInscripcion Elevador() =>
        new ElevadorDeSesionTrasInscripcion(
            _identity, _memberships, _jwt, _refresh,
            NullLogger<ElevadorDeSesionTrasInscripcion>.Instance);

    private ConfirmMfaEnrollmentCommandHandler NewHandler() => new(
        _currentUser, _identity, _pending, _memberships, Elevador(),
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

    /// <summary>
    /// La rama que no tenía ninguna prueba, y por eso el defecto del cliente
    /// —tokens emitidos y descartados— vivió sin que nada lo señalara.
    /// </summary>
    [Fact]
    public async Task Inscripcion_forzada_con_una_cooperativa_eleva_la_sesion()
    {
        PrepararConfirmacionValida();
        _currentUser.Purpose.Returns(CentralJwtPurposes.MfaEnroll);
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = UserId, Email = Email, TwoFactorEnabled = true });
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActiveMembershipInfo(TenantA, "Coop A", false, true) });

        var result = await NewHandler().Handle(new ConfirmMfaEnrollmentCommand("123456"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-jwt");
        result.Value.RefreshToken.Should().Be("refresh-token");
        result.Value.ActiveTenantPublicId.Should().Be(TenantA);
        result.Value.ActiveTenantName.Should().Be("Coop A");
    }

    /// <summary>
    /// Con dos cooperativas hay que elegir, y esa pantalla necesita otro token que
    /// aquí no se emite. Devolver uno operativo sin cooperativa activa metería a la
    /// persona en el sistema sin haber elegido en cuál está.
    /// </summary>
    [Fact]
    public async Task Con_varias_cooperativas_no_se_eleva_nada()
    {
        PrepararConfirmacionValida();
        _currentUser.Purpose.Returns(CentralJwtPurposes.MfaEnroll);
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = UserId, Email = Email, TwoFactorEnabled = true });
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ActiveMembershipInfo(TenantA, "Coop A", false, true),
                new ActiveMembershipInfo(Guid.NewGuid(), "Coop B", false, true),
            });

        var result = await NewHandler().Handle(new ConfirmMfaEnrollmentCommand("123456"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecoveryCodes.Should().NotBeEmpty("el autenticador quedó inscrito igual");
        result.Value.AccessToken.Should().BeNull();
    }

    private void PrepararConfirmacionValida()
    {
        _pending.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new MfaPendingEnrollment(Secret, DateTime.UtcNow));
        _identity.ConfirmMfaSetupAsync(UserId, Secret, "123456", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new MfaConfirmResult(true, Array.Empty<string>(), new[] { "code1", "code2" }));

        _jwt.IssueAccessToken(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<MetodosMfa>())
            .Returns(new CentralAccessTokenResult(
                "access-jwt", DateTime.UtcNow.AddMinutes(15), "jti", "full"));
        _jwt.IssueRefreshToken()
            .Returns(new CentralRefreshTokenResult(
                "refresh-token", "refresh-hash", DateTime.UtcNow.AddHours(12)));
    }
}
