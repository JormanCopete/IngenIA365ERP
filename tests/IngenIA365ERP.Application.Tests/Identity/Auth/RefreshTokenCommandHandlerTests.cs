using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Auth.RefreshToken;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
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

    private RefreshTokenCommandHandler NewHandler() => new(
        _store, _identity, _memberships, _jwt, _clock,
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
        Guid? tenantId = null) => new(
            CentralUserId: UserId,
            ActiveTenantPublicId: tenantId,
            FamilyId: FamilyId,
            IssuedAt: FixedNow.AddMinutes(-5),
            IpAddress: null,
            UserAgent: null,
            ReplacedByTokenHashHex: replacedBy,
            SecurityStamp: securityStamp);

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
}
