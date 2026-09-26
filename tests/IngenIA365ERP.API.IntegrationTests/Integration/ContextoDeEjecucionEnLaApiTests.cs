using System.Net;
using System.Security.Claims;
using FluentAssertions;
using IngenIA365ERP.API.Integration;
using IngenIA365ERP.API.Middleware;
using IngenIA365ERP.API.Services;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// Las piezas de la API que leen el contexto de ejecución (feature 012, T038, T041, T042, T044,
/// T046). No necesitan contenedores: se construyen a mano con un accesor de petición propio (el
/// <c>HttpContextAccessor</c> real guarda la petición en un AsyncLocal estático) y el <see cref="ContextoAmbiental"/>. Lo que no se negocia: con petición, nada cambia; sin
/// petición y con contexto, todos dicen la cooperativa y el actor del trabajo; sin ninguno de los
/// dos, nadie inventa una cooperativa.
/// </summary>
public class ContextoDeEjecucionEnLaApiTests
{
    private static readonly TenantDirectoryEntry Coop =
        new(21, Guid.Parse("12345678-aaaa-bbbb-cccc-1234567890ab"), "coop_ctx", "Coop Contexto", DatabaseName: "coop_ctx");

    private static IDisposable ConProceso(string origen = "Tarea:contexto") =>
        ContextoAmbiental.Fijar(Coop, Actor.ProcesoDeIntegracion(origen), origen);

    // ------------------------------------------------------------------------- reloj (T038) --

    [Fact]
    public void El_reloj_de_la_api_usa_la_zona_de_Bogota()
    {
        var reloj = new DateTimeService(Options.Create(new PlataformaOptions()), NullLogger<DateTimeService>.Instance);

        var local = reloj.ALocal(new DateTime(2026, 9, 25, 3, 30, 0, DateTimeKind.Utc));

        local.Offset.Should().Be(TimeSpan.FromHours(-5));
        DateOnly.FromDateTime(local.DateTime).Should().Be(new DateOnly(2026, 9, 24));
        reloj.HoyLocal.Should().Be(DateOnly.FromDateTime(reloj.AhoraLocal.DateTime));
    }

    [Fact]
    public void Una_zona_que_no_existe_cae_a_menos_cinco_fijo_con_un_aviso()
    {
        var logger = new LoggerQueCuenta<DateTimeService>();
        var reloj = new DateTimeService(Options.Create(new PlataformaOptions { ZonaHoraria = "Marte/Olympus_Mons" }), logger);

        reloj.ALocal(new DateTime(2026, 1, 1, 2, 0, 0, DateTimeKind.Utc)).Offset.Should().Be(TimeSpan.FromHours(-5));
        _ = reloj.HoyLocal;
        _ = reloj.AhoraLocal;
        logger.Avisos.Should().Be(1, "el aviso sale una sola vez, al construir el servicio");
    }

    // --------------------------------------------------------------------- origen (T042) --

    [Theory]
    [InlineData("app", ExecutionChannel.App)]
    [InlineData("APP", ExecutionChannel.App)]
    [InlineData("web", ExecutionChannel.Web)]
    [InlineData("pos", ExecutionChannel.Web)]
    [InlineData(null, ExecutionChannel.Web)]
    public void El_canal_sale_de_X_Canal_y_lo_desconocido_es_web(string? cabecera, ExecutionChannel esperado)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/api/inventory/documents";
        http.Request.Headers.UserAgent = "Pruebas/1.0";
        if (cabecera is not null) http.Request.Headers["X-Canal"] = cabecera;
        var accesor = new AccesorFijo { HttpContext = http };
        var ip = Substitute.For<IIpAddressAccessor>();
        ip.IpAddress.Returns("190.1.2.3");

        var origen = new OrigenDeLaPeticion(accesor, ip);

        origen.Canal.Should().Be(esperado);
        origen.Ip.Should().Be("190.1.2.3");
        origen.UserAgent.Should().Be("Pruebas/1.0");
        origen.Endpoint.Should().Be("POST /api/inventory/documents");
        origen.Origen.Should().Be("POST /api/inventory/documents");
    }

    [Fact]
    public void En_segundo_plano_el_origen_es_el_del_contexto_sin_ip_y_canal_proceso()
    {
        var origen = new OrigenDeLaPeticion(new AccesorFijo(), Substitute.For<IIpAddressAccessor>());

        using var _ = ConProceso("Lote:LT-9");

        origen.Canal.Should().Be(ExecutionChannel.Process);
        origen.Ip.Should().BeNull();
        origen.UserAgent.Should().BeNull();
        origen.Endpoint.Should().BeNull();
        origen.Origen.Should().Be("Lote:LT-9");
    }

    // ------------------------------------------------------------------ accesores (T044) --

    [Fact]
    public void Sin_peticion_los_accesores_leen_el_contexto_ambiental()
    {
        var accesor = new AccesorFijo();
        var cooperativa = new TenantContextAccessor(accesor);
        var usuario = new CurrentUserService(accesor);
        var central = new CurrentCentralUserContextAccessor(accesor);
        var ip = new IpAddressAccessor(accesor);

        cooperativa.TenantId.Should().BeNull("sin petición ni contexto no hay cooperativa");
        usuario.TenantId.Should().BeNull();

        using var _ = ConProceso();

        cooperativa.TenantId.Should().Be(Coop.PublicId.ToString("N"));
        cooperativa.TenantName.Should().Be("Coop Contexto");
        usuario.TenantId.Should().Be("21", "ICurrentUserService.TenantId es el Id interno");
        usuario.UserName.Should().Be(Actor.NombreDelProceso);
        usuario.UserId.Should().BeNull("UserId no cambia (pregunta B1)");
        central.ActiveTenantPublicId.Should().Be(Coop.PublicId);
        central.CentralUserId.Should().BeNull();
        central.IsGlobalMasterAdmin.Should().BeFalse();
        central.IsAuthenticated.Should().BeFalse();
        ip.IpAddress.Should().BeNull("el proceso no tiene IP");
    }

    [Fact]
    public void Con_peticion_el_contexto_ambiental_no_se_mira()
    {
        var http = new DefaultHttpContext();
        var otra = Guid.NewGuid();
        http.Items["TenantPublicId"] = otra;
        http.Items["TenantId"] = 3;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "ana@coop.test")], "prueba"));
        var accesor = new AccesorFijo { HttpContext = http };

        using var _ = ConProceso();

        new TenantContextAccessor(accesor).TenantId.Should().Be(otra.ToString("N"));
        new CurrentUserService(accesor).TenantId.Should().Be("3");
        new CurrentUserService(accesor).UserName.Should().Be("ana@coop.test");
    }

    // ---------------------------------------------------------------------- actor (T041) --

    [Fact]
    public async Task Sin_peticion_el_actor_es_el_del_contexto_y_sin_contexto_lanza()
    {
        var actor = new ActorDeLaPeticion(new AccesorFijo(), Substitute.For<IOrigenDeLaPeticion>(), Substitute.For<ICurrentCentralUserContext>());

        await FluentActions.Invoking(() => actor.ObtenerAsync()).Should().ThrowAsync<InvalidOperationException>();

        using var _ = ConProceso("Mensaje:77");
        var resuelto = await actor.ObtenerAsync();
        resuelto.Kind.Should().Be(ActorKind.Process);
        resuelto.Origin.Should().Be("Mensaje:77");
        resuelto.UserId.Should().BeNull();
    }

    // ------------------------------------------------------------------- ejecutor (T046) --

    [Fact]
    public async Task El_ejecutor_lanza_dentro_de_una_peticion_o_con_una_entrada_sin_base()
    {
        var accesor = new AccesorFijo();
        var ejecutor = new EjecutorEnCooperativa(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            accesor,
            NullLogger<EjecutorEnCooperativa>.Instance);
        var corrio = false;
        Task Trabajo(IServiceProvider _, CancellationToken __) { corrio = true; return Task.CompletedTask; }

        await FluentActions.Invoking(() => ejecutor.EjecutarAsync(
                new TenantDirectoryEntry(1, Guid.NewGuid(), "x", "Sin base"), Actor.ProcesoDeIntegracion("Tarea:x"), "Tarea:x", Trabajo))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*no trae base*");

        accesor.HttpContext = new DefaultHttpContext();
        await FluentActions.Invoking(() => ejecutor.EjecutarAsync(Coop, Actor.ProcesoDeIntegracion("Tarea:x"), "Tarea:x", Trabajo))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*petición*");
        accesor.HttpContext = null;

        corrio.Should().BeFalse();
        ContextoAmbiental.Activo.Should().BeFalse();
    }

    /// <summary>Un accesor de petición por prueba: no comparte nada con las demás.</summary>
    private sealed class AccesorFijo : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    private sealed class LoggerQueCuenta<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public int Avisos { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == Microsoft.Extensions.Logging.LogLevel.Warning) Avisos++;
        }
    }
}
