using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Storage.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Notifications;

/// <summary>
/// Feature 012, T051 (decisiones-transversales T39, T47; pregunta B5): el despachador de correo pasa por
/// <see cref="IEjecutorEnCooperativa"/> —una cooperativa a la vez, con el actor del proceso— y toma el
/// arrendamiento <c>email.dispatch</c> de esa cooperativa antes de leer nada. Sin él, dos réplicas leían el
/// mismo lote pendiente y el correo salía dos veces. Aquí, con un ejecutor y un arrendamiento de mentira;
/// el caso de dos instancias sobre la misma base real está en <c>ArrendamientosYTransaccionTests</c> (e2e).
/// </summary>
public class NotificationEmailDispatcherTests
{
    private static readonly TenantDirectoryEntry Alfa = new(1, Guid.NewGuid(), "alfa", "Coop Alfa", DatabaseName: "alfa");
    private static readonly TenantDirectoryEntry Beta = new(2, Guid.NewGuid(), "beta", "Coop Beta", DatabaseName: "beta");

    [Fact]
    public async Task Con_el_arrendamiento_manda_el_pendiente_y_lo_suelta_al_terminar()
    {
        var escenario = new Escenario(Alfa);
        var notificacion = escenario.Pendiente(Alfa, "persona@coop.test", "Aviso de prueba");

        await escenario.Despachador().DespacharUnaPasadaAsync(CancellationToken.None);

        escenario.Correos.Enviados.Should().ContainSingle(m => m.To == "persona@coop.test" && m.Subject == "Aviso de prueba");
        escenario.Bases[Alfa.PublicId].Notifications.AsNoTracking().Single(n => n.Id == notificacion).EmailStatus.Should().Be("Sent");
        escenario.Arrendamientos.Tomados.Should().Equal("alfa:email.dispatch");
        escenario.Arrendamientos.Soltados.Should().Equal("alfa:email.dispatch");
    }

    [Fact]
    public async Task Sin_el_arrendamiento_no_lee_ni_manda_nada()
    {
        var escenario = new Escenario(Alfa);
        escenario.Arrendamientos.Negar("alfa:email.dispatch");
        var notificacion = escenario.Pendiente(Alfa, "persona@coop.test", "No debe salir");

        await escenario.Despachador().DespacharUnaPasadaAsync(CancellationToken.None);

        escenario.Correos.Enviados.Should().BeEmpty("otra réplica tiene el arrendamiento de esta cooperativa");
        escenario.Bases[Alfa.PublicId].Notifications.AsNoTracking().Single(n => n.Id == notificacion).EmailStatus.Should().Be("Pending");
        escenario.Arrendamientos.Soltados.Should().BeEmpty("no se suelta lo que no se tomó");
    }

    [Fact]
    public async Task Corre_cada_cooperativa_por_el_ejecutor_con_el_actor_del_proceso()
    {
        var escenario = new Escenario(Alfa, Beta);
        escenario.Pendiente(Alfa, "a@coop.test", "Para Alfa");
        escenario.Pendiente(Beta, "b@coop.test", "Para Beta");

        await escenario.Despachador().DespacharUnaPasadaAsync(CancellationToken.None);

        escenario.Ejecutor.Llamadas.Select(l => l.Cooperativa).Should().BeEquivalentTo([Alfa.PublicId, Beta.PublicId]);
        escenario.Ejecutor.Llamadas.Should().OnlyContain(l =>
            l.Actor.Kind == ActorKind.Process && l.Origen == "Tarea:email.dispatch" && l.Actor.Origin == "Tarea:email.dispatch");
        escenario.Correos.Enviados.Select(m => m.Subject).Should().BeEquivalentTo(["Para Alfa", "Para Beta"]);
    }

    [Fact]
    public async Task Una_cooperativa_que_falla_suelta_su_arrendamiento_y_no_detiene_a_las_demas()
    {
        var escenario = new Escenario(Alfa, Beta);
        escenario.Romper(Alfa);
        escenario.Pendiente(Beta, "b@coop.test", "Para Beta");

        await escenario.Despachador().DespacharUnaPasadaAsync(CancellationToken.None);

        escenario.Arrendamientos.Soltados.Should().Contain("alfa:email.dispatch", "el arrendamiento se suelta aunque el lote falle");
        escenario.Correos.Enviados.Should().ContainSingle(m => m.Subject == "Para Beta");
    }

    [Fact]
    public async Task Mientras_la_base_no_esta_lista_no_recorre_cooperativas()
    {
        var escenario = new Escenario(Alfa);
        escenario.Pendiente(Alfa, "persona@coop.test", "Todavía no");

        await escenario.Despachador(baseLista: false).DespacharUnaPasadaAsync(CancellationToken.None);

        escenario.Ejecutor.Llamadas.Should().BeEmpty();
        escenario.Correos.Enviados.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------ dobles --

    private sealed class Escenario
    {
        private readonly TenantDirectoryEntry[] _cooperativas;
        private readonly HashSet<Guid> _rotas = [];

        public Escenario(params TenantDirectoryEntry[] cooperativas)
        {
            _cooperativas = cooperativas;
            foreach (var c in cooperativas) Bases[c.PublicId] = TestDbContextFactory.Create($"correo-{c.Identifier}-{Guid.NewGuid():N}");
            Ejecutor = new EjecutorDePrueba(this);
        }

        public Dictionary<Guid, TestApplicationDbContext> Bases { get; } = [];
        public ArrendamientosDePrueba Arrendamientos { get; } = new();
        public CorreoDePrueba Correos { get; } = new();
        public EjecutorDePrueba Ejecutor { get; }

        public void Romper(TenantDirectoryEntry cooperativa) => _rotas.Add(cooperativa.PublicId);

        public long Pendiente(TenantDirectoryEntry cooperativa, string correo, string asunto)
        {
            var db = Bases[cooperativa.PublicId];
            var usuario = new User { Username = correo, Email = correo };
            db.Users.Add(usuario);
            var n = new Notification
            {
                TenantId = cooperativa.Id, RecipientUserPublicId = usuario.PublicId, Type = "Prueba",
                Subject = asunto, Body = "Cuerpo", ChannelsMask = 2, EmailStatus = "Pending",
            };
            db.Notifications.Add(n);
            db.SaveChanges();
            db.ChangeTracker.Clear();
            return n.Id;
        }

        public NotificationEmailDispatcher Despachador(bool baseLista = true)
        {
            var raiz = new ServiceCollection();
            var directorio = NSubstitute.Substitute.For<ITenantDirectory>();
            NSubstitute.SubstituteExtensions.Returns(directorio.ListActiveAsync(NSubstitute.Arg.Any<CancellationToken>()),
                Task.FromResult<IReadOnlyList<TenantDirectoryEntry>>(_cooperativas));
            raiz.AddSingleton(directorio);
            var proveedor = raiz.BuildServiceProvider();
            return new NotificationEmailDispatcher(
                proveedor.GetRequiredService<IServiceScopeFactory>(), Ejecutor,
                NullLogger<NotificationEmailDispatcher>.Instance, () => baseLista);
        }

        public IServiceProvider ServiciosDe(TenantDirectoryEntry cooperativa)
        {
            var servicios = new ServiceCollection();
            if (!_rotas.Contains(cooperativa.PublicId))
                servicios.AddSingleton<IApplicationDbContext>(Bases[cooperativa.PublicId]);
            servicios.AddSingleton<IArrendamientos>(new ArrendamientoDeLaCooperativa(Arrendamientos, cooperativa.Identifier));
            servicios.AddSingleton<IEmailSender>(Correos);
            servicios.AddSingleton<INotificationTemplateRenderer>(new SinPlantillas());
            return servicios.BuildServiceProvider();
        }
    }

    private sealed class EjecutorDePrueba(Escenario escenario) : IEjecutorEnCooperativa
    {
        public List<(Guid Cooperativa, Actor Actor, string Origen)> Llamadas { get; } = [];

        public async Task<ResultadoEnCooperativa> EjecutarAsync(
            TenantDirectoryEntry cooperativa, Actor actor, string origen,
            Func<IServiceProvider, CancellationToken, Task> trabajo, CancellationToken ct = default)
        {
            Llamadas.Add((cooperativa.PublicId, actor, origen));
            await trabajo(escenario.ServiciosDe(cooperativa), ct);
            return ResultadoEnCooperativa.Ejecutada;
        }
    }

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

    private sealed class CorreoDePrueba : IEmailSender
    {
        public List<EmailMessage> Enviados { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken ct)
        {
            lock (Enviados) Enviados.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class SinPlantillas : INotificationTemplateRenderer
    {
        public Task<string?> RenderHtmlAsync(string notificationType, IReadOnlyDictionary<string, string?> model, CancellationToken ct) =>
            Task.FromResult<string?>(null);
    }
}
