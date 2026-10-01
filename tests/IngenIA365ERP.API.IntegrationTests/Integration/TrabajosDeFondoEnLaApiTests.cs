using FluentAssertions;
using IngenIA365ERP.API.Integration;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Persistence.Initialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// Los trabajos de fondo de la API sin contenedores (feature 012, T049, T050; decisiones-transversales
/// T10, T47): la configuración <c>Integration</c> y el <see cref="ProgramadorDeTareas"/>. Lo que no se
/// negocia: el programador no toca nada hasta que la base está lista, corre por
/// <see cref="IEjecutorEnCooperativa"/> una cooperativa a la vez, no hace nada sin el arrendamiento
/// <c>scheduled.tasks</c> de esa cooperativa, cada tarea firma como «Proceso de integración» con origen
/// <c>Tarea:{nombre}</c>, y una tarea que falla no detiene a las demás.
/// </summary>
public class TrabajosDeFondoEnLaApiTests
{
    private static readonly TenantDirectoryEntry Alfa = new(1, Guid.NewGuid(), "alfa", "Coop Alfa", DatabaseName: "alfa");
    private static readonly TenantDirectoryEntry Beta = new(2, Guid.NewGuid(), "beta", "Coop Beta", DatabaseName: "beta");

    // ------------------------------------------------------------------ IntegrationOptions (T049) --

    [Fact]
    public void Los_valores_por_defecto_son_los_de_la_decision_T10_y_son_validos()
    {
        var o = new IntegrationOptions();

        o.Dispatcher.Enabled.Should().BeTrue();
        o.Dispatcher.IntervalSeconds.Should().Be(5);
        o.Dispatcher.BudgetSeconds.Should().Be(60);
        o.Dispatcher.LeaseTtlSeconds.Should().Be(120);
        o.Dispatcher.BatchSize.Should().Be(100);
        o.Retries.BaseDelaySeconds.Should().Be(15);
        o.Retries.MaxDelayMinutes.Should().Be(15);
        o.Retries.AlertAfterAttempts.Should().Be(3);
        o.Retries.AlertAfterMinutes.Should().Be(15);
        o.AuditForwarder.Enabled.Should().BeTrue();
        o.ScheduledTasks.Enabled.Should().BeTrue();
        o.EmailDispatcher.Enabled.Should().BeTrue();
        o.Problemas().Should().BeEmpty();
    }

    [Fact]
    public void Un_cero_o_un_arrendamiento_mas_corto_que_el_presupuesto_no_arranca()
    {
        var o = new IntegrationOptions();
        o.Dispatcher.IntervalSeconds = 0;
        o.Dispatcher.BatchSize = 0;
        o.Dispatcher.LeaseTtlSeconds = 30;
        o.Retries.BaseDelaySeconds = 0;
        o.ScheduledTasks.IntervalSeconds = 0;
        o.EmailDispatcher.IntervalSeconds = -1;

        var problemas = o.Problemas();

        problemas.Should().Contain(p => p.Contains("Integration:Dispatcher:IntervalSeconds"));
        problemas.Should().Contain(p => p.Contains("Integration:Dispatcher:BatchSize"));
        problemas.Should().Contain(p => p.Contains("Integration:Dispatcher:LeaseTtlSeconds"),
            "un arrendamiento que vence antes de terminar el presupuesto deja entrar a otra réplica a mitad de la tanda");
        problemas.Should().Contain(p => p.Contains("Integration:Retries:BaseDelaySeconds"));
        problemas.Should().Contain(p => p.Contains("Integration:ScheduledTasks:IntervalSeconds"));
        problemas.Should().Contain(p => p.Contains("Integration:EmailDispatcher:IntervalSeconds"));
    }

    [Fact]
    public void El_appsettings_de_la_api_trae_la_seccion_Integration_completa()
    {
        var archivo = BuscarArriba(Path.Combine("src", "Presentation", "IngenIA365ERP.API", "appsettings.json"));
        var config = new ConfigurationBuilder().AddJsonFile(archivo).Build();

        var seccion = config.GetSection(IntegrationOptions.SectionName);
        seccion.Exists().Should().BeTrue();
        foreach (var clave in new[] { "Dispatcher:Enabled", "Dispatcher:IntervalSeconds", "Dispatcher:BudgetSeconds", "Dispatcher:LeaseTtlSeconds",
                     "Dispatcher:BatchSize", "Retries:BaseDelaySeconds", "Retries:MaxDelayMinutes", "Retries:AlertAfterAttempts",
                     "Retries:AlertAfterMinutes", "AuditForwarder:Enabled", "ScheduledTasks:Enabled", "EmailDispatcher:Enabled" })
        {
            seccion[clave].Should().NotBeNull($"Integration:{clave} está en appsettings.json");
        }

        var enlazada = seccion.Get<IntegrationOptions>()!;
        enlazada.Problemas().Should().BeEmpty();
    }

    // ------------------------------------------------------------------ ProgramadorDeTareas (T050) --

    [Fact]
    public async Task Mientras_la_base_no_esta_lista_no_hace_nada()
    {
        var e = new Escenario([Alfa], lista: false);
        var tarea = new TareaDePrueba("reorden");

        await e.Programador(tarea).CorrerUnaPasadaAsync(CancellationToken.None);

        e.Ejecutor.Llamadas.Should().BeEmpty();
        tarea.Corridas.Should().BeEmpty();
    }

    [Fact]
    public async Task Corre_cada_tarea_por_el_ejecutor_con_el_actor_del_proceso_y_suelta_el_arrendamiento()
    {
        var e = new Escenario([Alfa, Beta]);
        var reorden = new TareaDePrueba("reorden");
        var quiebre = new TareaDePrueba("quiebre");

        await e.Programador(reorden, quiebre).CorrerUnaPasadaAsync(CancellationToken.None);

        reorden.Corridas.Should().BeEquivalentTo([Alfa.PublicId, Beta.PublicId]);
        quiebre.Corridas.Should().BeEquivalentTo([Alfa.PublicId, Beta.PublicId]);
        var deTareas = e.Ejecutor.Llamadas.Where(l => l.Origen is "Tarea:reorden" or "Tarea:quiebre").ToList();
        deTareas.Should().HaveCount(4);
        deTareas.Should().OnlyContain(l => l.Actor.Kind == ActorKind.Process && l.Actor.Origin == l.Origen
                                           && l.Actor.Name == Actor.NombreDelProceso && l.Actor.Ip == null);
        e.Arrendamientos.Tomados.Should().BeEquivalentTo(["alfa:scheduled.tasks", "beta:scheduled.tasks"]);
        e.Arrendamientos.Soltados.Should().BeEquivalentTo(["alfa:scheduled.tasks", "beta:scheduled.tasks"]);
    }

    [Fact]
    public async Task Sin_el_arrendamiento_de_la_cooperativa_no_corre_sus_tareas()
    {
        var e = new Escenario([Alfa, Beta]);
        e.Arrendamientos.Negar("alfa:scheduled.tasks");
        var tarea = new TareaDePrueba("reorden");

        await e.Programador(tarea).CorrerUnaPasadaAsync(CancellationToken.None);

        tarea.Corridas.Should().Equal(Beta.PublicId);
        e.Arrendamientos.Soltados.Should().Equal("beta:scheduled.tasks");
    }

    [Fact]
    public async Task Una_tarea_que_falla_no_detiene_a_las_demas_ni_a_las_otras_cooperativas()
    {
        var e = new Escenario([Alfa, Beta]);
        var rota = new TareaDePrueba("rota") { Lanzar = true };
        var sana = new TareaDePrueba("sana");

        await e.Programador(rota, sana).CorrerUnaPasadaAsync(CancellationToken.None);

        sana.Corridas.Should().BeEquivalentTo([Alfa.PublicId, Beta.PublicId]);
        e.Arrendamientos.Soltados.Should().BeEquivalentTo(["alfa:scheduled.tasks", "beta:scheduled.tasks"]);
    }

    [Fact]
    public async Task Pregunta_a_cada_tarea_si_le_toca_con_la_hora_local_y_su_ultima_corrida()
    {
        var e = new Escenario([Alfa]);
        var nocturna = new TareaDePrueba("verificacion") { SoloUnaVez = true };
        var programador = e.Programador(nocturna);

        await programador.CorrerUnaPasadaAsync(CancellationToken.None);
        await programador.CorrerUnaPasadaAsync(CancellationToken.None);

        nocturna.Corridas.Should().HaveCount(1, "la segunda pasada le dice su última corrida y ella decide no correr");
        nocturna.Preguntas.Should().HaveCount(2);
        nocturna.Preguntas[0].Ultima.Should().BeNull();
        nocturna.Preguntas[1].Ultima.Should().Be(((IDateTimeService)e.Reloj).AhoraLocal);
        nocturna.Preguntas[0].Ahora.Offset.Should().Be(TimeSpan.FromHours(-5));
    }

    [Fact]
    public async Task La_pasada_manual_se_limita_a_la_cooperativa_pedida()
    {
        var e = new Escenario([Alfa, Beta]);
        var tarea = new TareaDePrueba("reorden");

        await e.Programador(tarea).CorrerUnaPasadaAsync(Beta.PublicId, CancellationToken.None);

        tarea.Corridas.Should().Equal(Beta.PublicId);
    }

    [Fact]
    public async Task Una_cooperativa_omitida_por_el_ejecutor_no_corre_tareas()
    {
        var e = new Escenario([Alfa, Beta]);
        e.Ejecutor.Omitir(Alfa.PublicId);
        var tarea = new TareaDePrueba("reorden");

        await e.Programador(tarea).CorrerUnaPasadaAsync(CancellationToken.None);

        tarea.Corridas.Should().Equal(Beta.PublicId);
    }

    // ------------------------------------------------------------------------------------ dobles --

    private static string BuscarArriba(string relativo)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidato = Path.Combine(dir.FullName, relativo);
            if (File.Exists(candidato)) return candidato;
        }
        throw new FileNotFoundException(relativo);
    }

    private sealed class Escenario(TenantDirectoryEntry[] cooperativas, bool lista = true)
    {
        public ArrendamientosDePrueba Arrendamientos { get; } = new();
        public EjecutorDePrueba Ejecutor { get; } = new();
        public RelojFijo Reloj { get; } = new();

        public ProgramadorDeTareas Programador(params ITareaProgramada[] tareas)
        {
            var raiz = new ServiceCollection();
            var directorio = Substitute.For<ITenantDirectory>();
            directorio.ListActiveAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<TenantDirectoryEntry>>(cooperativas));
            raiz.AddSingleton(directorio);
            var proveedor = raiz.BuildServiceProvider();
            Ejecutor.Arrendamientos = Arrendamientos;

            var readiness = new DatabaseReadiness();
            if (lista) readiness.MarkReady();

            return new ProgramadorDeTareas(
                proveedor.GetRequiredService<IServiceScopeFactory>(), Ejecutor, tareas, readiness, Reloj,
                Options.Create(new IntegrationOptions()), NullLogger<ProgramadorDeTareas>.Instance);
        }
    }

    private sealed class EjecutorDePrueba : IEjecutorEnCooperativa
    {
        private readonly HashSet<Guid> _omitidas = [];
        public ArrendamientosDePrueba Arrendamientos { get; set; } = new();
        public List<(Guid Cooperativa, Actor Actor, string Origen)> Llamadas { get; } = [];
        public void Omitir(Guid cooperativa) => _omitidas.Add(cooperativa);

        public async Task<ResultadoEnCooperativa> EjecutarAsync(
            TenantDirectoryEntry cooperativa, Actor actor, string origen,
            Func<IServiceProvider, CancellationToken, Task> trabajo, CancellationToken ct = default)
        {
            if (_omitidas.Contains(cooperativa.PublicId)) return ResultadoEnCooperativa.Omitida;
            Llamadas.Add((cooperativa.PublicId, actor, origen));
            var servicios = new ServiceCollection();
            servicios.AddSingleton<IArrendamientos>(new ArrendamientoDeLaCooperativa(Arrendamientos, cooperativa.Identifier));
            servicios.AddSingleton(new CooperativaEnCurso(cooperativa.PublicId));
            await trabajo(servicios.BuildServiceProvider(), ct);
            return ResultadoEnCooperativa.Ejecutada;
        }
    }

    private sealed record CooperativaEnCurso(Guid PublicId);

    private sealed class ArrendamientosDePrueba
    {
        private readonly HashSet<string> _negados = [];
        public List<string> Tomados { get; } = [];
        public List<string> Soltados { get; } = [];
        public void Negar(string clave) => _negados.Add(clave);
        public bool Tomar(string clave)
        {
            if (_negados.Contains(clave)) return false;
            Tomados.Add(clave);
            return true;
        }
    }

    private sealed class ArrendamientoDeLaCooperativa(ArrendamientosDePrueba registro, string cooperativa) : IArrendamientos
    {
        public Task<bool> ArrendarAsync(string nombre, TimeSpan? duracion = null, CancellationToken ct = default) =>
            Task.FromResult(registro.Tomar($"{cooperativa}:{nombre}"));

        public Task<bool> RenovarAsync(string nombre, TimeSpan? duracion = null, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task SoltarAsync(string nombre, CancellationToken ct = default)
        {
            registro.Soltados.Add($"{cooperativa}:{nombre}");
            return Task.CompletedTask;
        }
    }

    private sealed class TareaDePrueba(string nombre) : ITareaProgramada
    {
        public string Nombre => nombre;
        public bool Lanzar { get; init; }
        public bool SoloUnaVez { get; init; }
        public List<Guid> Corridas { get; } = [];
        public List<(DateTimeOffset Ahora, DateTimeOffset? Ultima)> Preguntas { get; } = [];

        public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida)
        {
            Preguntas.Add((ahoraLocal, ultimaCorrida));
            return !SoloUnaVez || ultimaCorrida is null;
        }

        public Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
        {
            if (Lanzar) throw new InvalidOperationException("La tarea de prueba falla a propósito.");
            Corridas.Add(servicios.GetRequiredService<CooperativaEnCurso>().PublicId);
            return Task.CompletedTask;
        }
    }

    private sealed class RelojFijo : IDateTimeService
    {
        public DateTime UtcNow { get; } = new(2026, 9, 25, 7, 0, 0, DateTimeKind.Utc);
        public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
    }
}
