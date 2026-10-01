using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Persistence.MultiTenancy;
using IngenIA365ERP.Persistence.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// La fábrica de <see cref="ErpTenantInfo"/> decide contra qué base habla la operativa
/// (feature 012, T5, T044). Fuera de una petición había una sola respuesta, la plantilla; ahora un
/// trabajo de fondo corrido por <c>IEjecutorEnCooperativa</c> trae su cooperativa en el
/// <see cref="ContextoAmbiental"/> y tiene que abrir ESA base. Un error aquí no da excepción: da
/// los datos de otra cooperativa.
/// </summary>
public class CooperativaDelAmbitoTests
{
    private const string Plantilla =
        "Host=localhost;Port=5432;Database=plantilla;Username=ingenia;Password=secreta-de-prueba";

    private static IServiceProvider Servicios(HttpContext? peticion = null)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton(Options.Create(new DatabaseOptions
        {
            Provider = "PostgreSql",
            ConnectionStrings = new Dictionary<string, string?> { ["PostgreSQL"] = Plantilla },
        }));
        servicios.AddScoped<TenantConnectionResolver>();
        var accesor = Substitute.For<IHttpContextAccessor>();
        accesor.HttpContext.Returns(peticion);
        servicios.AddSingleton(accesor);
        servicios.AddSingleton(Substitute.For<ICurrentTenantService>());
        return servicios.BuildServiceProvider().CreateScope().ServiceProvider;
    }

    [Fact]
    public void Sin_peticion_ni_contexto_va_a_la_plantilla_como_antes()
    {
        var info = CooperativaDelAmbito.Crear(Servicios());

        info.ConnectionString.Should().Be(Plantilla);
        info.SchemaName.Should().Be("dbo");
    }

    [Fact]
    public void Con_contexto_ambiental_abre_la_base_de_esa_cooperativa()
    {
        var coop = new TenantDirectoryEntry(12, Guid.Parse("0f0f0f0f-1111-2222-3333-444455556666"), "coop_a", "Coop A", DatabaseName: "erp_coop_a");

        using var _ = ContextoAmbiental.Fijar(coop, Actor.ProcesoDeIntegracion("Tarea:prueba"), "Tarea:prueba");
        var info = CooperativaDelAmbito.Crear(Servicios());

        ConnectionStringTargeting.CatalogoDe(info.ConnectionString!).Should().Be("erp_coop_a");
        info.Name.Should().Be("Coop A");
        info.PublicId.Should().Be(coop.PublicId);
        info.InternalId.Should().Be(12);
        info.Id.Should().Be(coop.PublicId.ToString("N"), "el mismo formato que TenantContextAccessor.TenantId");
    }

    [Fact]
    public void La_cadena_propia_de_la_cooperativa_manda()
    {
        const string propia = "Host=otro-servidor;Database=coop_b;Username=x;Password=y";
        var coop = new TenantDirectoryEntry(13, Guid.NewGuid(), "coop_b", "Coop B", DatabaseName: "coop_b", ConnectionString: propia);

        using var _ = ContextoAmbiental.Fijar(coop, Actor.ProcesoDeIntegracion("Tarea:prueba"), "Tarea:prueba");

        CooperativaDelAmbito.Crear(Servicios()).ConnectionString.Should().Be(propia);
    }

    [Fact]
    public void Con_contexto_de_una_cooperativa_sin_base_lanza_y_no_cae_a_la_plantilla()
    {
        var coop = new TenantDirectoryEntry(14, Guid.NewGuid(), "coop_c", "Coop C");

        using var _ = ContextoAmbiental.Fijar(coop, Actor.ProcesoDeIntegracion("Tarea:prueba"), "Tarea:prueba");

        FluentActions.Invoking(() => CooperativaDelAmbito.Crear(Servicios()))
            .Should().Throw<InvalidOperationException>().WithMessage("*Coop C*");
    }

    [Fact]
    public void Dentro_de_una_peticion_sin_cooperativa_sigue_lanzando()
    {
        var peticion = new DefaultHttpContext();
        peticion.Request.Method = "GET";
        peticion.Request.Path = "/api/core/people";

        FluentActions.Invoking(() => CooperativaDelAmbito.Crear(Servicios(peticion)))
            .Should().Throw<InvalidOperationException>().WithMessage("*sin cooperativa resuelta*");
    }
}
