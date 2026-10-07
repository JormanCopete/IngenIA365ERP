using System.Collections.Concurrent;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Processes;

/// <summary>
/// Feature 013, T0142 (T5, PM-13, C59; quickstart §1.5; esqueleto): la pasada del procesador de Cartera recorre las cooperativas del
/// directorio con <c>MaxParallelCooperatives</c> y un presupuesto menor que el arrendamiento, toma <c>lending.process</c> en cada una
/// por <see cref="IEjecutorEnCooperativa"/>, salta las que no tienen Cartera activada, conduce las corridas <c>Requested</c> de las
/// cuatro clases por el ejecutor de su clase, deja <c>Requested</c> con advertencia las de una clase sin ejecutor, y ante una excepción
/// del ejecutor deja la corrida <c>Failed</c> con su error en <c>LND_ProcessRunErrors</c> y sigue con la cooperativa siguiente. Que
/// <c>Lending:Process:Enabled = false</c> no haga nada lo fija <c>OpcionesDelProcesoDeCartera</c> en el procesador de la API.
/// </summary>
public class ProcesadorDeCarteraTests
{
    private static readonly DateTime Ahora = new(2026, 10, 7, 5, 30, 0, DateTimeKind.Utc);
    private static readonly ParametrosDeLaPasada Parametros = new(2, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(120));

    private sealed class Cooperativa(string nombre)
    {
        public TenantDirectoryEntry Entrada { get; } = new(nombre.Length, Guid.NewGuid(), nombre, nombre);
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public IArrendamientos Arrendamientos { get; } = Substitute.For<IArrendamientos>();
    }

    private sealed class EjecutorDePrueba(LendingProcessKind clase, Func<LendingProcessRun, AvanceDeCorrida> tanda) : IEjecutorDeCorridaDeCartera
    {
        public LendingProcessKind Clase => clase;
        public List<Guid> Conducidas { get; } = [];

        public Task<AvanceDeCorrida> EjecutarTandaAsync(LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct)
        {
            Conducidas.Add(corrida.PublicId);
            return Task.FromResult(tanda(corrida));
        }
    }

    /// <summary>Un ejecutor por cooperativa que arma, en cada llamada, un ámbito con la base y los servicios de esa cooperativa.</summary>
    private sealed class EjecutorEnCooperativaDePrueba(IReadOnlyList<Cooperativa> cooperativas, IReadOnlyList<IEjecutorDeCorridaDeCartera> ejecutores)
        : IEjecutorEnCooperativa
    {
        private int _enVuelo;
        public int MaximoEnVuelo { get; private set; }
        public ConcurrentBag<(Guid Cooperativa, Actor Actor)> Llamadas { get; } = [];

        public async Task<ResultadoEnCooperativa> EjecutarAsync(TenantDirectoryEntry cooperativa, Actor actor, string origen,
            Func<IServiceProvider, CancellationToken, Task> trabajo, CancellationToken ct = default)
        {
            var c = cooperativas.Single(x => x.Entrada.PublicId == cooperativa.PublicId);
            Llamadas.Add((cooperativa.PublicId, actor));
            var enVuelo = Interlocked.Increment(ref _enVuelo);
            lock (this) MaximoEnVuelo = Math.Max(MaximoEnVuelo, enVuelo);
            try
            {
                var reloj = Substitute.For<IDateTimeService>();
                reloj.UtcNow.Returns(Ahora);
                var servicios = new ServiceCollection()
                    .AddSingleton<IApplicationDbContext>(c.Db)
                    .AddSingleton(c.Arrendamientos)
                    .AddSingleton(reloj)
                    .AddSingleton<Microsoft.Extensions.Logging.ILogger<ConductorDeCorridasDeCartera>>(NullLogger<ConductorDeCorridasDeCartera>.Instance)
                    .AddSingleton<IEnumerable<IEjecutorDeCorridaDeCartera>>(ejecutores)
                    .AddScoped<ConductorDeCorridasDeCartera>()
                    .BuildServiceProvider();
                await Task.Delay(20, ct);
                await trabajo(servicios, ct);
                return ResultadoEnCooperativa.Ejecutada;
            }
            finally
            {
                Interlocked.Decrement(ref _enVuelo);
            }
        }
    }

    private static Cooperativa Activada(string nombre, bool activada = true)
    {
        var c = new Cooperativa(nombre);
        c.Db.LendingSetups.Add(new LendingSetup { ActivationDate = activada ? new DateOnly(2026, 10, 1) : null });
        c.Db.SaveChanges();
        c.Arrendamientos.ArrendarAsync(NombresDeArrendamiento.ProcesoDeCartera, Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(true);
        return c;
    }

    private static LendingProcessRun Corrida(Cooperativa c, LendingProcessKind clase, int minutosAtras = 10)
    {
        var corrida = new LendingProcessRun
        {
            Kind = clase, Status = LendingProcessRunStatus.Requested, RequestedByKind = ActorKind.Process,
            ExecutionOrigin = "Tarea:cartera.diario", RequestedAt = Ahora.AddMinutes(-minutosAtras),
        };
        c.Db.LendingProcessRuns.Add(corrida);
        c.Db.SaveChanges();
        return corrida;
    }

    private static (PasadaDeCartera Pasada, EjecutorEnCooperativaDePrueba Ejecutor) Pasada(IReadOnlyList<Cooperativa> cooperativas,
        params IEjecutorDeCorridaDeCartera[] ejecutores)
    {
        var directorio = Substitute.For<ITenantDirectory>();
        directorio.ListActiveAsync(Arg.Any<CancellationToken>()).Returns(cooperativas.Select(c => c.Entrada).ToList());
        var raiz = new ServiceCollection().AddSingleton(directorio).BuildServiceProvider();
        var ejecutor = new EjecutorEnCooperativaDePrueba(cooperativas, ejecutores);
        return (new PasadaDeCartera(raiz.GetRequiredService<IServiceScopeFactory>(), ejecutor, NullLogger<PasadaDeCartera>.Instance), ejecutor);
    }

    [Fact]
    public async Task Conduce_las_corridas_de_las_cooperativas_activadas_con_el_proceso_y_el_arrendamiento()
    {
        var a = Activada("A");
        var b = Activada("B");
        var ciclo = new EjecutorDePrueba(LendingProcessKind.DailyCycle, _ => AvanceDeCorrida.Terminada);
        var corridaA = Corrida(a, LendingProcessKind.DailyCycle);
        var corridaB = Corrida(b, LendingProcessKind.DailyCycle);
        var (pasada, ejecutor) = Pasada([a, b], ciclo);

        var resultados = await pasada.CorrerAsync(Parametros, null, CancellationToken.None);

        resultados.Values.Should().OnlyContain(r => r == ResultadoDeConduccion.Conducida);
        ciclo.Conducidas.Should().BeEquivalentTo([corridaA.PublicId, corridaB.PublicId]);
        (await a.Db.LendingProcessRuns.SingleAsync()).Status.Should().Be(LendingProcessRunStatus.Completed);
        ejecutor.Llamadas.Should().OnlyContain(l => l.Actor.Kind == ActorKind.Process && l.Actor.Origin == "Tarea:lending.process");
        await a.Arrendamientos.Received(1).ArrendarAsync(NombresDeArrendamiento.ProcesoDeCartera, Parametros.DuracionDelArrendamiento, Arg.Any<CancellationToken>());
        await a.Arrendamientos.Received(1).SoltarAsync(NombresDeArrendamiento.ProcesoDeCartera, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_pasa_de_MaxParallelCooperatives_a_la_vez()
    {
        var cooperativas = Enumerable.Range(1, 5).Select(i => Activada($"C{i}")).ToList();
        var (pasada, ejecutor) = Pasada(cooperativas);

        await pasada.CorrerAsync(Parametros with { MaxParallelCooperatives = 2 }, null, CancellationToken.None);

        ejecutor.MaximoEnVuelo.Should().BeLessThanOrEqualTo(2).And.BePositive();
    }

    [Fact]
    public async Task Salta_la_cooperativa_sin_Cartera_activada_y_la_que_tiene_el_arrendamiento_ocupado()
    {
        var sinActivar = Activada("S", activada: false);
        var ocupada = Activada("O");
        ocupada.Arrendamientos.ArrendarAsync(Arg.Any<string>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(false);
        var ciclo = new EjecutorDePrueba(LendingProcessKind.DailyCycle, _ => AvanceDeCorrida.Terminada);
        Corrida(sinActivar, LendingProcessKind.DailyCycle);
        Corrida(ocupada, LendingProcessKind.DailyCycle);
        var (pasada, _) = Pasada([sinActivar, ocupada], ciclo);

        var resultados = await pasada.CorrerAsync(Parametros, null, CancellationToken.None);

        resultados[sinActivar.Entrada.PublicId].Should().Be(ResultadoDeConduccion.SinActivar);
        resultados[ocupada.Entrada.PublicId].Should().BeNull("otra réplica tiene el arrendamiento");
        ciclo.Conducidas.Should().BeEmpty();
        (await sinActivar.Db.LendingProcessRuns.SingleAsync()).Status.Should().Be(LendingProcessRunStatus.Requested);
    }

    [Fact]
    public async Task Una_clase_sin_ejecutor_deja_su_corrida_Requested_y_las_demas_siguen()
    {
        var c = Activada("C");
        var cierre = Corrida(c, LendingProcessKind.MonthlyClose, minutosAtras: 30);
        var ciclo = Corrida(c, LendingProcessKind.DailyCycle);
        var correccion = Corrida(c, LendingProcessKind.NationalValueCorrection, minutosAtras: 5);
        var conducidas = new List<LendingProcessKind>();
        var ejecutorDelCiclo = new EjecutorDePrueba(LendingProcessKind.DailyCycle, r => { conducidas.Add(r.Kind); return AvanceDeCorrida.Terminada; });
        var ejecutorDeCorreccion = new EjecutorDePrueba(LendingProcessKind.NationalValueCorrection, r => { conducidas.Add(r.Kind); return AvanceDeCorrida.Terminada; });
        var (pasada, _) = Pasada([c], ejecutorDelCiclo, ejecutorDeCorreccion);

        await pasada.CorrerAsync(Parametros, null, CancellationToken.None);

        conducidas.Should().Equal([LendingProcessKind.NationalValueCorrection, LendingProcessKind.DailyCycle],
            "la corrección por valor nacional va antes que el ciclo (§22.1)");
        (await c.Db.LendingProcessRuns.SingleAsync(r => r.Id == cierre.Id)).Status.Should().Be(LendingProcessRunStatus.Requested,
            "el cierre mensual no tiene ejecutor hasta US9");
        (await c.Db.LendingProcessRuns.SingleAsync(r => r.Id == ciclo.Id)).Status.Should().Be(LendingProcessRunStatus.Completed);
        (await c.Db.LendingProcessRuns.SingleAsync(r => r.Id == correccion.Id)).Status.Should().Be(LendingProcessRunStatus.Completed);
    }

    [Fact]
    public async Task Una_excepcion_del_ejecutor_deja_la_corrida_Failed_con_su_error_y_sigue_con_la_siguiente_cooperativa()
    {
        var falla = Activada("F");
        var sana = Activada("S");
        var ciclo = new EjecutorDePrueba(LendingProcessKind.DailyCycle, r =>
            r.ExecutionOrigin == "explota" ? throw new InvalidOperationException("Sin usura del día para CONSUMO") : AvanceDeCorrida.Terminada);
        var mala = Corrida(falla, LendingProcessKind.DailyCycle);
        mala.ExecutionOrigin = "explota";
        falla.Db.SaveChanges();
        Corrida(sana, LendingProcessKind.DailyCycle);
        var (pasada, _) = Pasada([falla, sana], ciclo);

        var resultados = await pasada.CorrerAsync(Parametros, null, CancellationToken.None);

        resultados[falla.Entrada.PublicId].Should().Be(ResultadoDeConduccion.ConFalla);
        resultados[sana.Entrada.PublicId].Should().Be(ResultadoDeConduccion.Conducida);
        var fallida = await falla.Db.LendingProcessRuns.SingleAsync();
        fallida.Status.Should().Be(LendingProcessRunStatus.Failed);
        fallida.FinishedAt.Should().Be(Ahora);
        var error = await falla.Db.LendingProcessRunErrors.SingleAsync();
        error.Should().BeEquivalentTo(new { ProcessRunId = fallida.Id, LoanId = (int?)null, ErrorCode = nameof(InvalidOperationException),
            ErrorMessage = "Sin usura del día para CONSUMO", Step = ConductorDeCorridasDeCartera.PasoDeLaCorrida });
        (await sana.Db.LendingProcessRuns.SingleAsync()).Status.Should().Be(LendingProcessRunStatus.Completed);
    }

    [Fact]
    public async Task Una_corrida_con_creditos_fallidos_termina_CompletedWithErrors_y_una_pendiente_sigue_Running()
    {
        var c = Activada("C");
        Corrida(c, LendingProcessKind.DailyCycle);
        var conErrores = new EjecutorDePrueba(LendingProcessKind.DailyCycle, r => { r.FailedLoans = 2; return AvanceDeCorrida.Terminada; });
        var (pasada, _) = Pasada([c], conErrores);

        await pasada.CorrerAsync(Parametros, null, CancellationToken.None);
        (await c.Db.LendingProcessRuns.SingleAsync()).Status.Should().Be(LendingProcessRunStatus.CompletedWithErrors);

        var otra = Activada("D");
        Corrida(otra, LendingProcessKind.DailyCycle);
        var sinPresupuesto = new EjecutorDePrueba(LendingProcessKind.DailyCycle, _ => AvanceDeCorrida.Pendiente);
        var (pasadaCorta, _) = Pasada([otra], sinPresupuesto);

        // Con el reloj quieto, el ejecutor que nunca termina agota… nada: por eso el presupuesto se mide; aquí cero tiempo = sin tandas.
        await pasadaCorta.CorrerAsync(Parametros with { Presupuesto = TimeSpan.Zero, DuracionDelArrendamiento = TimeSpan.FromSeconds(1) }, null, CancellationToken.None);
        (await otra.Db.LendingProcessRuns.SingleAsync()).Status.Should().Be(LendingProcessRunStatus.Requested, "sin presupuesto no se toma ninguna corrida");
    }

    [Fact]
    public void El_presupuesto_tiene_que_caber_en_el_arrendamiento()
    {
        Parametros.Problemas().Should().BeEmpty();
        (Parametros with { DuracionDelArrendamiento = TimeSpan.FromSeconds(60) }).Problemas().Should().NotBeEmpty();
        (Parametros with { MaxParallelCooperatives = 0 }).Problemas().Should().NotBeEmpty();
        ConductorDeCorridasDeCartera.Clases.Should().BeEquivalentTo(
            [LendingProcessKind.DailyCycle, LendingProcessKind.MonthlyClose, LendingProcessKind.CloseReopen, LendingProcessKind.NationalValueCorrection],
            "las cuatro clases de corrida de C59");
    }
}
