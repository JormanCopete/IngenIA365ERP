using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Persistence.Interceptors;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit
{
    using IngenIA365ERP.Application.Tests.AuditoriaDePrueba.Core;
    using IngenIA365ERP.Application.Tests.AuditoriaDePrueba.Parameters;

    /// <summary>
    /// T017, segunda mitad (feature 012; decisiones-transversales T36, T37; contracts/api.md §1.6): lo que la
    /// auditoría de comandos y de entidades hace sin transacción real (InMemory). Que un rechazo sobreviva al
    /// rollback y que el reenviador selle en los dos motores lo prueba la e2e <c>IntegridadDeAuditoriaTests</c>.
    /// </summary>
    public class AuditoriaDeComandosTests
    {
        private static readonly Guid Cooperativa = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        private static readonly string Flujo = $"{Cooperativa:N}:10y";

        private readonly string _base = Guid.NewGuid().ToString();
        private readonly TestApplicationDbContext _db;
        private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
        private readonly List<AuditLogCommand> _enMongo = [];
        private readonly ICurrentUserService _usuario = Payroll.Common.NominaTestData.UsuarioDePrueba("ana@demo", 7);
        private readonly IOrigenDeLaPeticion _origen = Substitute.For<IOrigenDeLaPeticion>();
        private Actor _actor = new(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Ana Operadora", "ana@demo", ExecutionChannel.Web, "POST /prueba", "10.0.0.9", null);

        public AuditoriaDeComandosTests()
        {
            _db = TestDbContextFactory.Create(_base);
            _auditoria.LogAsync(Arg.Do<AuditLogCommand>(_enMongo.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            _origen.Ip.Returns("10.0.0.9");
            _origen.UserAgent.Returns("Prueba/1.0");
            _origen.Endpoint.Returns("POST /api/prueba");
            _origen.Canal.Returns(ExecutionChannel.App);
            _origen.Origen.Returns("POST /api/prueba");
        }

        // ----------------------------------------------------------- módulos --

        [Theory]
        [InlineData("IngenIA365ERP.Application.Common.Integration", "Integration")]
        [InlineData("IngenIA365ERP.Domain.Entities.Integration.Transactions", "Integration")]
        [InlineData("IngenIA365ERP.Application.Common.Approvals", "Approvals")]
        [InlineData("IngenIA365ERP.Application.Common.Alerts", "Alerts")]
        [InlineData("IngenIA365ERP.Application.Common.Parameters", "Parameters")]
        [InlineData("IngenIA365ERP.Application.ElectronicInvoicing.Emission", "ElectronicInvoicing")]
        [InlineData("IngenIA365ERP.Application.Core.Taxes", "Taxes")]
        [InlineData("IngenIA365ERP.Domain.Entities.Core.Taxes", "Taxes")]
        [InlineData("IngenIA365ERP.Application.Core.PaymentMeans", "PaymentMeans")]
        [InlineData("IngenIA365ERP.Domain.Entities.Core.Payments", "PaymentMeans")]
        [InlineData("IngenIA365ERP.Application.Core.People", "Core")]
        [InlineData("IngenIA365ERP.Application.Inventory.Integration", "Inventory")]
        [InlineData("IngenIA365ERP.Application.Accounting.Inventory", "Accounting")]
        [InlineData("IngenIA365ERP.Application.Lending.Payments.Commands.ProcessPayment", "Lending")]
        [InlineData("IngenIA365ERP.Application.Audit.RegisterOptionAccess", "Navigation")]
        [InlineData("IngenIA365ERP.Application.Identity.Auth.WebAuthn", "General")]
        [InlineData(null, "General")]
        public void Una_sola_inferencia_reconoce_los_modulos_nuevos_antes_que_Core(string? ns, string modulo)
        {
            ModuloDeAuditoria.Inferir(ns).Should().Be(modulo);
        }

        [Fact]
        public void Los_modulos_encadenados_son_los_de_T38_sin_Contabilidad()
        {
            AuditoriaEncadenada.Modulos.Should().BeEquivalentTo(
                ["Inventory", "ElectronicInvoicing", "Integration", "Approvals", "Alerts", "Parameters", "Taxes", "PaymentMeans", "Navigation"]);
            AuditoriaEncadenada.EsEncadenado("Accounting").Should().BeFalse("pregunta C1");
            AuditoriaEncadenada.Flujo(Cooperativa).Should().Be(Flujo);
        }

        // ------------------------------------------------ no encadenados (Mongo) --

        [Fact]
        public async Task El_evento_lleva_motivo_clave_actor_canal_e_IP()
        {
            var clave = Guid.NewGuid();
            var comando = new AnularFichaCommand(clave, "Se registró dos veces");

            var r = await Behavior<AnularFichaCommand, Result>().Handle(comando, _ => Task.FromResult(Result.Success()), CancellationToken.None);

            r.IsSuccess.Should().BeTrue();
            var evento = _enMongo.Should().ContainSingle().Subject;
            evento.Module.Should().Be("Core");
            evento.Action.Should().Be("AnularFicha");
            evento.IpAddress.Should().Be("10.0.0.9");
            evento.UserAgent.Should().Be("Prueba/1.0");
            evento.Endpoint.Should().Be("POST /api/prueba");
            evento.HttpMethod.Should().Be("POST");
            evento.Metadata.Should().Contain("Reason", "Se registró dos veces")
                .And.Contain("OperationKey", clave.ToString())
                .And.Contain("ActorKind", "Person")
                .And.Contain("Actor", "Ana Operadora")
                .And.Contain("Channel", "app");
        }

        [Fact]
        public async Task Un_resultado_fallido_queda_como_Rejected_con_su_codigo()
        {
            var comando = new AnularFichaCommand(Guid.NewGuid(), "motivo");

            await Behavior<AnularFichaCommand, Result>().Handle(comando,
                _ => Task.FromResult(Result.Failure("Core.Ficha.NoEncontrada", "No existe.")), CancellationToken.None);

            var evento = _enMongo.Should().ContainSingle().Subject;
            evento.Action.Should().Be(AuditEventTypes.CommandRejected);
            evento.EntityType.Should().Be(nameof(AnularFichaCommand));
            evento.Metadata.Should().Contain("ErrorCode", "Core.Ficha.NoEncontrada");
        }

        [Fact]
        public async Task Una_operacion_de_punto_de_venta_es_del_canal_pos()
        {
            await Behavior<CobrarEnCajaCommand, Result>().Handle(new CobrarEnCajaCommand(Guid.NewGuid()),
                _ => Task.FromResult(Result.Success()), CancellationToken.None);

            _enMongo.Should().ContainSingle().Which.Metadata.Should().Contain("Channel", "pos");
        }

        [Fact]
        public async Task En_segundo_plano_el_actor_es_el_proceso_con_su_origen()
        {
            _actor = Actor.ProcesoDeIntegracion("Tarea:prueba");
            var origen = Substitute.For<IOrigenDeLaPeticion>();
            origen.Canal.Returns(ExecutionChannel.Process);
            origen.Origen.Returns("Tarea:prueba");

            await Behavior<AnularFichaCommand, Result>(origen: origen).Handle(new AnularFichaCommand(Guid.NewGuid(), "m"),
                _ => Task.FromResult(Result.Success()), CancellationToken.None);

            _enMongo.Should().ContainSingle().Which.Metadata.Should()
                .Contain("ActorKind", "Process").And.Contain("Channel", "proceso").And.Contain("Origin", "Tarea:prueba");
        }

        // ------------------------------------------------------- encadenados --

        [Fact]
        public async Task En_un_modulo_encadenado_el_evento_va_al_outbox_y_no_a_Mongo()
        {
            var comando = new AddParametroCommand(Guid.NewGuid(), "INV", "true", "Cambio de política");

            var r = await Behavior<AddParametroCommand, Result>().Handle(comando, _ => Task.FromResult(Result.Success()), CancellationToken.None);

            r.IsSuccess.Should().BeTrue();
            _enMongo.Should().BeEmpty("para los módulos encadenados no hay doble registro");
            var fila = (await _db.AuditOutbox.ToListAsync()).Should().ContainSingle().Subject;
            fila.Stream.Should().Be(Flujo);
            fila.Module.Should().Be("Parameters");
            fila.Forwarded.Should().BeFalse();
            fila.Seq.Should().BeNull("lo sella el reenviador");
            fila.OccurredAt.Ticks.Should().Be(fila.OccurredAt.Ticks - fila.OccurredAt.Ticks % TimeSpan.TicksPerMillisecond, "a milisegundos, como Mongo");
            var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
            evento.Action.Should().Be("AddParametro");
            evento.TenantId.Should().Be(Cooperativa.ToString("N"));
            evento.Metadata.Should().Contain("Reason", "Cambio de política").And.Contain("Channel", "app");
            evento.NewValuesJson.Should().Contain("INV");
        }

        [Fact]
        public async Task Un_rechazo_encadenado_se_escribe_por_un_contexto_aparte()
        {
            var comando = new AddParametroCommand(Guid.NewGuid(), "INV", "tal vez", "m");
            var aparte = TestDbContextFactory.Create(_base);
            var fabrica = Substitute.For<ITenantDbContextFactory>();
            var ambito = Substitute.For<ITenantDbScope>();
            ambito.Db.Returns(aparte);
            fabrica.AbrirLaDelAmbito().Returns(ambito);

            var r = await Behavior<AddParametroCommand, Result>(fabrica: fabrica).Handle(comando,
                _ => Task.FromResult(Result.Failure("Parameters.ValueNotAllowed", "No admitido.")), CancellationToken.None);

            r.IsFailure.Should().BeTrue();
            fabrica.Received(1).AbrirLaDelAmbito();
            var fila = (await aparte.AuditOutbox.ToListAsync()).Should().ContainSingle().Subject;
            var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
            evento.Action.Should().Be("Rejected");
            evento.Metadata.Should().Contain("ErrorCode", "Parameters.ValueNotAllowed");
            _enMongo.Should().BeEmpty();
        }

        [Fact]
        public async Task Sin_contexto_aparte_un_rechazo_encadenado_no_se_pierde_va_a_Mongo()
        {
            await Behavior<AddParametroCommand, Result>().Handle(new AddParametroCommand(Guid.NewGuid(), "INV", "x", "m"),
                _ => Task.FromResult(Result.Failure("Parameters.ValueNotAllowed", "No admitido.")), CancellationToken.None);

            _enMongo.Should().ContainSingle().Which.Action.Should().Be("Rejected");
        }

        [Fact]
        public async Task Sin_cooperativa_un_modulo_encadenado_sigue_por_Mongo()
        {
            await Behavior<AddParametroCommand, Result>(conCooperativa: false).Handle(new AddParametroCommand(Guid.NewGuid(), "INV", "true", "m"),
                _ => Task.FromResult(Result.Success()), CancellationToken.None);

            _enMongo.Should().ContainSingle().Which.Module.Should().Be("Parameters");
            (await _db.AuditOutbox.CountAsync()).Should().Be(0);
        }

        // -------------------------------------------------------- interceptor --

        [Fact]
        public async Task El_interceptor_omite_SinDiff_y_enmascara_NoAuditar()
        {
            await using var db = Contexto(out var enMongo);
            db.Add(new FilaTecnica { Nombre = "arrendamiento" });
            db.Add(new ConSecreto { Nombre = "canal", Clave = "s3cr3t0" });
            await db.SaveChangesAsync();

            enMongo.Should().ContainSingle("la fila técnica no deja diferencias");
            var evento = enMongo[0];
            evento.EntityType.Should().Be(nameof(ConSecreto));
            evento.Module.Should().Be("Core");
            var nuevos = (Dictionary<string, object?>)evento.NewValues!;
            nuevos["Clave"].Should().Be(NoAuditarAttribute.Mascara);
            nuevos["Nombre"].Should().Be("canal");

            var secreto = await db.Set<ConSecreto>().SingleAsync();
            secreto.Clave = "otro";
            enMongo.Clear();
            await db.SaveChangesAsync();
            var cambio = enMongo.Should().ContainSingle().Subject;
            cambio.ChangedFields.Should().Contain("Clave", "el cambio se registra");
            ((Dictionary<string, object?>)cambio.OldValues!)["Clave"].Should().Be(NoAuditarAttribute.Mascara);
            ((Dictionary<string, object?>)cambio.NewValues!)["Clave"].Should().Be(NoAuditarAttribute.Mascara);
        }

        [Fact]
        public async Task El_interceptor_deja_las_diferencias_encadenadas_en_el_outbox_del_mismo_guardado()
        {
            await using var db = Contexto(out var enMongo);
            db.Add(new ParametroDePrueba { Clave = "Existencias.StockNegativoPermitido", Valor = "true" });
            await db.SaveChangesAsync();

            enMongo.Should().BeEmpty();
            var fila = (await db.Set<AuditOutboxEntry>().ToListAsync()).Should().ContainSingle().Subject;
            fila.Module.Should().Be("Parameters");
            fila.Stream.Should().Be(Flujo);
            var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
            evento.Action.Should().Be("Create");
            evento.EntityType.Should().Be(nameof(ParametroDePrueba));
            evento.NewValuesJson.Should().Contain("StockNegativoPermitido");
        }

        // ------------------------------------------------------------ ayudantes --

        private AuditBehavior<TPedido, TRespuesta> Behavior<TPedido, TRespuesta>(
            bool conCooperativa = true, ITenantDbContextFactory? fabrica = null, IOrigenDeLaPeticion? origen = null)
            where TPedido : notnull
        {
            var servicios = new ServiceCollection();
            servicios.AddSingleton<IApplicationDbContext>(_db);
            var cooperativa = Substitute.For<ICurrentTenantService>();
            cooperativa.TenantId.Returns(conCooperativa ? Cooperativa.ToString("N") : null);
            servicios.AddSingleton(cooperativa);
            servicios.AddSingleton(origen ?? _origen);
            var actor = Substitute.For<IActorActual>();
            actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_actor));
            servicios.AddSingleton(actor);
            if (fabrica is not null) servicios.AddSingleton(fabrica);
            return new AuditBehavior<TPedido, TRespuesta>(_auditoria, _usuario,
                NullLogger<AuditBehavior<TPedido, TRespuesta>>.Instance, servicios.BuildServiceProvider());
        }

        private ContextoConInterceptor Contexto(out List<AuditLogCommand> enMongo)
        {
            var capturados = new List<AuditLogCommand>();
            var auditoria = Substitute.For<IAuditService>();
            auditoria.LogAsync(Arg.Do<AuditLogCommand>(capturados.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var cooperativa = Substitute.For<ICurrentTenantService>();
            cooperativa.TenantId.Returns(Cooperativa.ToString("N"));
            var interceptor = new AuditableEntityInterceptor(_usuario, cooperativa, auditoria,
                NullLogger<AuditableEntityInterceptor>.Instance, _origen);
            var opciones = new DbContextOptionsBuilder<ContextoConInterceptor>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options;
            enMongo = capturados;
            return new ContextoConInterceptor(opciones);
        }
    }

    public sealed class ContextoConInterceptor(DbContextOptions<ContextoConInterceptor> opciones) : DbContext(opciones)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FilaTecnica>().Ignore(e => e.RowVersion);
            modelBuilder.Entity<ConSecreto>().Ignore(e => e.RowVersion);
            modelBuilder.Entity<ParametroDePrueba>().Ignore(e => e.RowVersion);
            modelBuilder.Entity<AuditOutboxEntry>().Ignore(e => e.RowVersion);
        }
    }
}

namespace IngenIA365ERP.Application.Tests.AuditoriaDePrueba.Core
{
    public sealed record AnularFichaCommand(Guid OperationKey, string Reason) : IRequest<Result>, IOperacionIdempotente, IConMotivo;

    public sealed record CobrarEnCajaCommand(Guid CashSessionPublicId) : IRequest<Result>, IOperacionDePuntoDeVenta;

    [SinDiffDeAuditoria]
    public sealed class FilaTecnica : AuditableEntity
    {
        public string Nombre { get; set; } = string.Empty;
    }

    public sealed class ConSecreto : AuditableEntity
    {
        public string Nombre { get; set; } = string.Empty;

        [NoAuditar]
        public string Clave { get; set; } = string.Empty;
    }
}

namespace IngenIA365ERP.Application.Tests.AuditoriaDePrueba.Parameters
{
    public sealed record AddParametroCommand(Guid OperationKey, string Module, string Value, string Reason)
        : IRequest<Result>, IOperacionIdempotente, IConMotivo;

    public sealed class ParametroDePrueba : AuditableEntity
    {
        public string Clave { get; set; } = string.Empty;
        public string Valor { get; set; } = string.Empty;
    }
}
