using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Mensajeria;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Mensajeria;

/// <summary>
/// Feature 023 (contracts/mensajeria.md §4): el camino de un aviso a su manejador. Versión o tipo desconocidos → ilegible; cooperativa que no
/// está activa → se descarta; el manejador corre dentro del ejecutor por cooperativa con un origen que el ejecutor admite (lo destapó la e2e
/// con RabbitMQ: «Cola:…» lanzaba y todo aviso volvía a la cola); un manejador que no es por cooperativa corre sin ejecutor.
/// </summary>
public class ConsumoDeAvisosTests
{
    private static readonly TenantDirectoryEntry Cooperativa = new(7, Guid.NewGuid(), "coop", "Cooperativa de prueba");

    private sealed class Manejador(string tipo, bool porCooperativa = true) : IManejadorDeAviso
    {
        public string Tipo => tipo;

        public bool PorCooperativa => porCooperativa;

        public int Veces { get; private set; }

        public Task ManejarAsync(Aviso aviso, IServiceProvider servicios, CancellationToken ct)
        {
            Veces++;
            return Task.CompletedTask;
        }
    }

    private static (ConsumoDeAvisos Consumo, IEjecutorEnCooperativa Ejecutor, List<string> Origenes) Crear(params IManejadorDeAviso[] manejadores)
    {
        var directorio = Substitute.For<ITenantDirectory>();
        directorio.ListActiveAsync(Arg.Any<CancellationToken>()).Returns([Cooperativa]);
        var servicios = new ServiceCollection().AddSingleton(directorio).BuildServiceProvider();
        var ejecutor = Substitute.For<IEjecutorEnCooperativa>();
        var origenes = new List<string>();
        ejecutor.EjecutarAsync(Arg.Any<TenantDirectoryEntry>(), Arg.Any<Actor>(), Arg.Any<string>(),
                Arg.Any<Func<IServiceProvider, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async c =>
            {
                origenes.Add(c.ArgAt<string>(2));
                await c.ArgAt<Func<IServiceProvider, CancellationToken, Task>>(3)(servicios, default);
                return ResultadoEnCooperativa.Ejecutada;
            });
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(DateTime.UtcNow);
        return (new ConsumoDeAvisos(servicios.GetRequiredService<IServiceScopeFactory>(), ejecutor, manejadores, reloj,
            NullLogger<ConsumoDeAvisos>.Instance), ejecutor, origenes);
    }

    [Fact]
    public async Task El_manejador_corre_en_la_cooperativa_con_un_origen_que_el_ejecutor_admite()
    {
        var manejador = new Manejador(TiposDeAviso.FeEmitir);
        var (consumo, _, origenes) = Crear(manejador);

        var r = await consumo.ConsumirAsync(Aviso.De(TiposDeAviso.FeEmitir, Cooperativa.PublicId, Guid.NewGuid(), DateTime.UtcNow, "prueba"), default);

        r.Should().Be(ResultadoDeConsumo.Atendido);
        manejador.Veces.Should().Be(1);
        origenes.Should().ContainSingle().Which.Should().StartWith("Tarea:");
        var crearActor = () => Actor.ProcesoDeIntegracion(origenes.Single());
        crearActor.Should().NotThrow("el ejecutor valida el origen del actor de proceso");
    }

    [Fact]
    public async Task Una_cooperativa_que_no_esta_activa_se_descarta_sin_ejecutar()
    {
        var manejador = new Manejador(TiposDeAviso.FeEmitir);
        var (consumo, ejecutor, _) = Crear(manejador);

        var r = await consumo.ConsumirAsync(Aviso.De(TiposDeAviso.FeEmitir, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, "prueba"), default);

        r.Should().Be(ResultadoDeConsumo.CooperativaInactiva);
        manejador.Veces.Should().Be(0);
        await ejecutor.DidNotReceiveWithAnyArgs().EjecutarAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task Una_version_o_un_tipo_desconocidos_son_ilegibles()
    {
        var (consumo, _, _) = Crear(new Manejador(TiposDeAviso.FeEmitir));

        (await consumo.ConsumirAsync(new Aviso(99, TiposDeAviso.FeEmitir, Cooperativa.PublicId, "x", DateTime.UtcNow, "prueba"), default))
            .Should().Be(ResultadoDeConsumo.Ilegible);
        (await consumo.ConsumirAsync(Aviso.De(TiposDeAviso.FeSeguimiento, Cooperativa.PublicId, "x", DateTime.UtcNow, "prueba"), default))
            .Should().Be(ResultadoDeConsumo.Ilegible, "nadie atiende ese tipo en esta prueba");
    }

    [Fact]
    public async Task Un_manejador_que_no_es_por_cooperativa_corre_sin_el_ejecutor()
    {
        var manejador = new Manejador(TiposDeAviso.IntegracionEntregar, porCooperativa: false);
        var (consumo, ejecutor, _) = Crear(manejador);

        var r = await consumo.ConsumirAsync(Aviso.De(TiposDeAviso.IntegracionEntregar, Cooperativa.PublicId, "*", DateTime.UtcNow, "prueba"), default);

        r.Should().Be(ResultadoDeConsumo.Atendido);
        manejador.Veces.Should().Be(1);
        await ejecutor.DidNotReceiveWithAnyArgs().EjecutarAsync(default!, default!, default!, default!, default);
    }
}
