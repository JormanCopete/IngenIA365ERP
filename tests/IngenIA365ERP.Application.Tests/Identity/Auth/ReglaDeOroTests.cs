using FluentAssertions;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Auth.Documento;
using IngenIA365ERP.Application.Identity.Auth.RefreshToken;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// Feature 017 (T039, regla de oro): una sesión abierta antes del despliegue —sin método sellado, de una cuenta
/// sin documento— sigue renovando: la renovación no exige ningún claim nuevo y la exigencia de documento no
/// muerde mientras la cooperativa no fije fecha, que es el defecto. Con la fecha vencida deja de renovar sin
/// invalidar la familia (no es un robo) y el ingreso le pedirá el documento. Que el relleno de verificación no
/// rota el sello lo comprueba la e2e de la regla de oro, contra la migración real.
/// </summary>
public class ReglaDeOroTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Persona = Guid.NewGuid();
    private static readonly Guid Coop = Guid.NewGuid();
    private const string Sello = "sello-de-antes";

    private readonly ICentralRefreshTokenStore _store = Substitute.For<ICentralRefreshTokenStore>();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly ITenantMembershipReader _membresias = Substitute.For<ITenantMembershipReader>();
    private readonly ICentralJwtIssuer _jwt = Substitute.For<ICentralJwtIssuer>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly TestAdminDbContext _admin = TestAdminDbContext.Create();

    public ReglaDeOroTests()
    {
        _reloj.UtcNow.Returns(Ahora);
        _jwt.IssueAccessToken(default, default!, default, default, default, default, default)
            .ReturnsForAnyArgs(new CentralAccessTokenResult("access", Ahora.AddMinutes(15), "jti", "full"));
        _jwt.IssueRefreshToken().Returns(new CentralRefreshTokenResult("refresh", "hash", Ahora.AddHours(12)));
        _identidad.FindByIdAsync(Persona, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Persona, Email = "ana@coop.test", SecurityStamp = Sello });
        _membresias.GetActiveMembershipsAsync(Persona, Arg.Any<CancellationToken>())
            .Returns([new ActiveMembershipInfo(Coop, "Coop", false, false)]);
        // La sesión de antes: sin método sellado (Ninguno), con cooperativa y el sello de siempre.
        _store.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new CentralRefreshSession(
            CentralUserId: Persona, ActiveTenantPublicId: Coop, FamilyId: Guid.NewGuid(), IssuedAt: Ahora.AddMinutes(-5),
            IpAddress: null, UserAgent: null, ReplacedByTokenHashHex: null, SecurityStamp: Sello));
    }

    private RefreshTokenCommandHandler Handler() => new(
        _store, _identidad, _membresias, _jwt, _reloj, Options.Create(new PoliticaDeSesionOptions()),
        NullLogger<RefreshTokenCommandHandler>.Instance, new ExigenciaDeDocumento(_admin, _reloj));

    [Fact]
    public async Task La_sesion_de_antes_sin_documento_renueva_mientras_la_cooperativa_no_fije_fecha()
    {
        var r = await Handler().Handle(new RefreshTokenCommand("refresh-viejo"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
    }

    [Fact]
    public async Task Una_politica_con_tipos_pero_sin_fecha_tampoco_corta()
    {
        var politica = TenantIdentityPolicy.CreateForTenant(Coop);
        politica.Cambiar(["CC"], null, 3);
        _admin.TenantIdentityPolicies.Add(politica);
        await _admin.SaveChangesAsync();

        var r = await Handler().Handle(new RefreshTokenCommand("refresh-viejo"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
    }

    [Fact]
    public async Task Vencida_la_fecha_deja_de_renovar_sin_invalidar_la_familia()
    {
        var politica = TenantIdentityPolicy.CreateForTenant(Coop);
        politica.Cambiar(["CC"], new DateOnly(2026, 11, 1), 3);
        _admin.TenantIdentityPolicies.Add(politica);
        await _admin.SaveChangesAsync();

        var r = await Handler().Handle(new RefreshTokenCommand("refresh-viejo"), default);

        r.Error.Code.Should().Be("Identity.Document.Required");
        await _store.DidNotReceiveWithAnyArgs().InvalidateFamilyAsync(default, default);
    }
}
