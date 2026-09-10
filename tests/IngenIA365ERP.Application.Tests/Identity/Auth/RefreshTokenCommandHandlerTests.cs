using FluentAssertions;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Auth.RefreshToken;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

public class RefreshTokenCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid FamilyId = Guid.NewGuid();
    private const string Email = "ana@cooperativa.co";
    private const string CurrentStamp = "stamp-actual";

    private readonly ICentralRefreshTokenStore _store;
    private readonly ICentralIdentityProvider _identity;
    private readonly ITenantMembershipReader _memberships;
    private readonly ICentralJwtIssuer _jwt;
    private readonly IDateTimeService _clock;

    public RefreshTokenCommandHandlerTests()
    {
        _store = Substitute.For<ICentralRefreshTokenStore>();
        _identity = Substitute.For<ICentralIdentityProvider>();
        _memberships = Substitute.For<ITenantMembershipReader>();
        _jwt = Substitute.For<ICentralJwtIssuer>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        _jwt.IssueAccessToken(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<MetodosMfa>())
            .Returns(new CentralAccessTokenResult("access-jwt", FixedNow.AddMinutes(15), "jti", "full"));
        _jwt.IssueRefreshToken()
            .Returns(new CentralRefreshTokenResult("new-refresh", "new-hash", FixedNow.AddHours(12)));
    }

    private readonly PoliticaDeSesionOptions _politica = new();

    private RefreshTokenCommandHandler NewHandler() => new(
        _store, _identity, _memberships, _jwt, _clock,
        Options.Create(_politica),
        NullLogger<RefreshTokenCommandHandler>.Instance);

    private static CentralUser CreateUser(string stamp = CurrentStamp) => new()
    {
        Id = UserId,
        Email = Email,
        SecurityStamp = stamp,
    };

    private static CentralRefreshSession Session(
        string? securityStamp,
        string? replacedBy = null,
        Guid? tenantId = null,
        DateTime? sessionExpiresAt = null) => new(
            CentralUserId: UserId,
            ActiveTenantPublicId: tenantId,
            FamilyId: FamilyId,
            IssuedAt: FixedNow.AddMinutes(-5),
            IpAddress: null,
            UserAgent: null,
            ReplacedByTokenHashHex: replacedBy,
            SecurityStamp: securityStamp,
            SessionExpiresAt: sessionExpiresAt);

    [Fact]
    public async Task Stamp_coincidente_rota_y_emite_tokens()
    {
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session(CurrentStamp));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-jwt");
        await _store.Received(1).StoreAsync(
            "new-hash", Arg.Is<CentralRefreshSession>(s => s.SecurityStamp == CurrentStamp),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _store.Received(1).MarkRotatedAsync(
            Arg.Any<string>(), "new-hash", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stamp_distinto_invalida_familia_y_rechaza()
    {
        // Password cambiada/reseteada tras emitir el refresh → stamp regenerado.
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session("stamp-viejo"));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.RefreshToken.Invalid");
        await _store.Received(1).InvalidateFamilyAsync(FamilyId, Arg.Any<CancellationToken>());
        await _store.DidNotReceive().StoreAsync(
            Arg.Any<string>(), Arg.Any<CentralRefreshSession>(),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sesion_legacy_sin_stamp_se_rechaza_fail_secure()
    {
        // Sesión serializada antes de agregar el campo → deserializa null.
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session(securityStamp: null));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.RefreshToken.Invalid");
        await _store.Received(1).InvalidateFamilyAsync(FamilyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_inexistente_rechaza_generico()
    {
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CentralRefreshSession?)null);

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.RefreshToken.Invalid");
    }

    [Fact]
    public async Task Reuso_de_token_rotado_invalida_familia()
    {
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session(CurrentStamp, replacedBy: "hash-del-reemplazo"));

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.RefreshToken.Reused");
        await _store.Received(1).InvalidateFamilyAsync(FamilyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Membresia_inactiva_con_tenant_activo_invalida_familia()
    {
        var tenantId = Guid.NewGuid();
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session(CurrentStamp, tenantId: tenantId));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        _memberships.GetActiveMembershipsAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ActiveMembershipInfo>());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Membership.NotActive");
        await _store.Received(1).InvalidateFamilyAsync(FamilyId, Arg.Any<CancellationToken>());
    }

    // ---------- Tope absoluto de doce horas ----------
    //
    // Antes cada rotación daba otras doce horas: con renovación silenciosa en el
    // cliente, la sesión no vencía nunca. Estas pruebas fijan que el tope viaja con
    // la sesión, que el TTL del refresh nuevo es lo que queda, y que pasado el tope
    // no hay rotación.

    [Fact]
    public async Task El_tope_de_la_sesion_se_arrastra_y_no_se_desliza()
    {
        var tope = FixedNow.AddHours(2);
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session(CurrentStamp, sessionExpiresAt: tope));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.RefreshTokenExpiresAt.Should().Be(tope, "el cliente cuenta desde ahí, no desde el token nuevo");
        await _store.Received(1).StoreAsync(
            "new-hash",
            Arg.Is<CentralRefreshSession>(s => s.SessionExpiresAt == tope && s.IssuedAt == FixedNow),
            TimeSpan.FromHours(2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_sesion_sin_tope_lo_toma_de_su_emision_y_lo_deja_explicito()
    {
        // Sesiones vivas en Redis antes de este campo, y las siete puertas de emisión,
        // que no lo fijan: IssuedAt + 12 h es exactamente el tope de su emisión.
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session(CurrentStamp));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        var esperado = FixedNow.AddMinutes(-5).Add(_politica.DuracionMaxima);
        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshTokenExpiresAt.Should().Be(esperado);
        await _store.Received(1).StoreAsync(
            "new-hash",
            Arg.Is<CentralRefreshSession>(s => s.SessionExpiresAt == esperado),
            esperado - FixedNow,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pasado_el_tope_no_hay_rotacion_y_el_codigo_lo_dice()
    {
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Session(CurrentStamp, sessionExpiresAt: FixedNow.AddSeconds(-1)));
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.RefreshToken.SessionExpired", "empieza por Identity.RefreshToken. y por eso responde 401");
        await _store.DidNotReceive().StoreAsync(
            Arg.Any<string>(), Arg.Any<CentralRefreshSession>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().InvalidateFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ---------- Inactividad ----------
    //
    // El servidor no ve cada petición, ve rotaciones: la inactividad se mide desde la
    // última. El cliente rota sola la sesión de una pestaña activa antes de llegar
    // aquí; esto es la red para la que no lo hizo.

    [Fact]
    public async Task Mas_de_la_inactividad_configurada_sin_rotar_se_rechaza_sin_invalidar_la_familia()
    {
        _politica.InactividadMinutos = 30;
        var sesion = Session(CurrentStamp) with { IssuedAt = FixedNow.AddMinutes(-31) };
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(sesion);
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Identity.RefreshToken.InactivityExpired");
        await _store.DidNotReceive().StoreAsync(
            Arg.Any<string>(), Arg.Any<CentralRefreshSession>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().InvalidateFamilyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dentro_de_la_inactividad_configurada_rota_con_normalidad()
    {
        _politica.InactividadMinutos = 30;
        var sesion = Session(CurrentStamp) with { IssuedAt = FixedNow.AddMinutes(-29) };
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(sesion);
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [Fact]
    public async Task El_limite_de_inactividad_es_el_configurado_no_uno_fijo()
    {
        _politica.InactividadMinutos = 10;
        var sesion = Session(CurrentStamp) with { IssuedAt = FixedNow.AddMinutes(-11) };
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(sesion);
        _identity.FindByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser());

        var result = await NewHandler().Handle(new RefreshTokenCommand("refresh-plano"), default);

        result.Error.Code.Should().Be("Identity.RefreshToken.InactivityExpired");
    }

    [Fact]
    public void La_politica_rechaza_valores_que_dejarian_a_todos_fuera()
    {
        new PoliticaDeSesionOptions { InactividadMinutos = 1 }.EsValida(out var m1).Should().BeFalse(m1);
        new PoliticaDeSesionOptions { DuracionMaximaHoras = 0 }.EsValida(out var m2).Should().BeFalse(m2);
        new PoliticaDeSesionOptions { InactividadMinutos = 13 * 60, DuracionMaximaHoras = 12 }.EsValida(out var m3).Should().BeFalse(m3);
        new PoliticaDeSesionOptions().EsValida(out _).Should().BeTrue("30 minutos y 12 horas son los valores por defecto");
    }
}
