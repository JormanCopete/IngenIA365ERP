using FluentAssertions;
using IngenIA365ERP.Application.Admin.Branches.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Admin;

/// <summary>
/// Hasta dónde alcanza quien administra sucursales.
///
/// <para>
/// Las sucursales viven en <c>ADM_Branches</c>, en la base administrativa, que
/// es compartida por diseño: el aislamiento por base de datos <b>no</b> las
/// cubre. Y <c>Admin.Branches.*</c> no está en la lista de retención
/// SaaS-global, correctamente, porque las sucursales son de la cooperativa. El
/// resultado era que todo administrador de cooperativa tenía el permiso y nada
/// acotaba su alcance.
/// </para>
///
/// <para>
/// Estas pruebas fijan la única barrera que queda.
/// </para>
/// </summary>
public class AlcanceDeSucursalesTests
{
    private static readonly Guid PublicaAlfa = Guid.NewGuid();

    private static TestAdminDbContext ConDosCooperativas()
    {
        var db = TestAdminDbContext.Create();
        db.Tenants.AddRange(
            new Tenant { Name = "Alfa", PublicId = PublicaAlfa, SchemaName = "coop_alfa", IsActive = true },
            new Tenant { Name = "Beta", PublicId = Guid.NewGuid(), SchemaName = "coop_beta", IsActive = true });
        db.SaveChanges();
        return db;
    }

    private static ICurrentTenantService Cooperativa(string? publica)
    {
        var s = Substitute.For<ICurrentTenantService>();
        s.TenantId.Returns(publica);
        return s;
    }

    private static ICurrentCentralUserContext Usuario(bool maestro)
    {
        var u = Substitute.For<ICurrentCentralUserContext>();
        u.IsGlobalMasterAdmin.Returns(maestro);
        return u;
    }

    [Fact]
    public async Task ElMaestro_AlcanzaTodasLasCooperativas()
    {
        using var db = ConDosCooperativas();

        var r = await AlcanceDeSucursales.ResolverAsync(
            db, Cooperativa(null), Usuario(maestro: true), default);

        r.Permitido.Should().BeTrue();
        r.Cooperativa.Should().BeNull("null significa sin acotar; el maestro administra el conjunto");
    }

    [Fact]
    public async Task UnAdministradorDeCooperativa_QuedaAcotadoALaSuya()
    {
        using var db = ConDosCooperativas();
        var alfa = db.Tenants.Single(t => t.PublicId == PublicaAlfa);

        var r = await AlcanceDeSucursales.ResolverAsync(
            db, Cooperativa(PublicaAlfa.ToString("N")), Usuario(maestro: false), default);

        r.Permitido.Should().BeTrue();
        r.Cooperativa.Should().Be(alfa.Id);
    }

    [Fact]
    public async Task SinCooperativaActiva_SeDENIEGA_NoSeAbreATodas()
    {
        // El punto entero. Ante la duda, denegar: la alternativa —seguir sin
        // filtro— es exactamente el fallo que esto corrige, y no da error.
        using var db = ConDosCooperativas();

        var r = await AlcanceDeSucursales.ResolverAsync(
            db, Cooperativa(null), Usuario(maestro: false), default);

        r.Permitido.Should().BeFalse();
        r.Cooperativa.Should().BeNull();
        r.Codigo.Should().Be("Session.TenantNotSelected");
    }

    [Fact]
    public async Task ConUnaCooperativaQueNoExiste_SeDeniega()
    {
        using var db = ConDosCooperativas();

        var r = await AlcanceDeSucursales.ResolverAsync(
            db, Cooperativa(Guid.NewGuid().ToString("N")), Usuario(maestro: false), default);

        r.Permitido.Should().BeFalse();
        r.Codigo.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task ConUnIdentificadorIlegible_SeDeniega()
    {
        using var db = ConDosCooperativas();

        var r = await AlcanceDeSucursales.ResolverAsync(
            db, Cooperativa("no-es-un-guid"), Usuario(maestro: false), default);

        r.Permitido.Should().BeFalse();
    }
}
