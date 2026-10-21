using FluentAssertions;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Lending.Audit
{
    using IngenIA365ERP.Application.Tests.Lending.Audit.ComandosDePrueba;

    /// <summary>
    /// Feature 013, T0255 (T67, F9, F10; research R136; api.md §1.2, §1.12): la auditoría de Cartera. Un rechazo del validador (que corre
    /// antes que <c>AuditBehavior</c>) queda como <c>Lending.Request.Rejected</c> con el comando, los campos y los códigos, sin valores; un
    /// <c>Result.Failure</c> regulatorio queda <c>Rejected</c> con su código; el evento lleva la oficina (<see cref="IConOficina"/> o la
    /// oficina por defecto del usuario) y el dispositivo (User-Agent más <c>X-Dispositivo</c>); los eventos van a <c>Lending</c>,
    /// <c>Compliance</c> o <c>NationalValues</c> según el espacio de nombres; el intento denegado es <c>Lending.Access.Denied</c> y nunca
    /// tumba la respuesta.
    /// </summary>
    public class AuditoriaDeCarteraTests
    {
        private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
        private readonly IAuditService _mongo = Substitute.For<IAuditService>();
        private readonly List<AuditLogCommand> _enMongo = [];
        private readonly IOrigenDeLaPeticion _origen = Substitute.For<IOrigenDeLaPeticion>();
        private readonly IAlcanceDeCartera _alcance = Substitute.For<IAlcanceDeCartera>();
        private readonly RechazosDeFormaDeLaPeticion _rechazos = new();
        private Branch _norte = null!, _sur = null!;

        public AuditoriaDeCarteraTests()
        {
            _mongo.LogAsync(Arg.Do<AuditLogCommand>(_enMongo.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            _origen.UserAgent.Returns("Mozilla/5.0 (Android 14)");
            _origen.Dispositivo.Returns("MAUI Android 14 · SM-A546");
            _origen.Endpoint.Returns("POST /api/lending/attribution-matrices");
            _origen.Canal.Returns(ExecutionChannel.App);
            _norte = new Branch { Name = "Norte" };
            _sur = new Branch { Name = "Sur" };
            _db.Branches.AddRange(_norte, _sur);
            _db.SaveChanges();
            _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(
                AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { _norte.Id, _sur.Id }, OficinaPorDefecto = _sur.Id }));
        }

        private ServiceProvider Servicios()
        {
            var actor = Substitute.For<IActorActual>();
            actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new Actor(ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(),
                "Ana Analista", "ana@coop", ExecutionChannel.App, "POST /api/lending", "10.0.0.9", null)));
            return new ServiceCollection()
                .AddSingleton<IApplicationDbContext>(_db)
                .AddSingleton(CooperativaDePrueba.Actual)
                .AddSingleton(_origen)
                .AddSingleton(_alcance)
                .AddSingleton(actor)
                .AddSingleton(_mongo)
                .AddSingleton(_rechazos)
                .AddSingleton(Payroll.Common.NominaTestData.UsuarioDePrueba("ana@coop", 7))
                .BuildServiceProvider();
        }

        private AuditBehavior<T, Result> Auditoria<T>() where T : notnull =>
            new(_mongo, Payroll.Common.NominaTestData.UsuarioDePrueba("ana@coop", 7), NullLogger<AuditBehavior<T, Result>>.Instance, Servicios());

        private async Task<AuditEventDocumentLeido> UnicoEnElOutbox()
        {
            var fila = (await _db.AuditOutbox.ToListAsync()).Should().ContainSingle().Subject;
            return new AuditEventDocumentLeido(fila.Module, AuditoriaEncadenada.LeerCarga(fila.PayloadJson!));
        }

        private sealed record AuditEventDocumentLeido(string Modulo, IngenIA365ERP.Application.Common.Interfaces.Audit.AuditEventDocument Evento);

        // ------------------------------------------------------------------------------------------ rechazo de forma (F9) --

        [Fact]
        public async Task Un_rechazo_del_validador_deja_Lending_Request_Rejected_con_comando_campos_y_codigos_sin_valores()
        {
            var servicios = Servicios();
            var validacion = new ValidationBehavior<CrearMatrizDePruebaCommand, Result>([new CrearMatrizDePruebaValidator()], servicios);

            var r = await validacion.Handle(new CrearMatrizDePruebaCommand(Guid.NewGuid(), "secreto-1234567", -5m),
                _ => throw new InvalidOperationException("el handler no corre con un rechazo de forma"), CancellationToken.None);

            r.Error.Code.Should().Be("Validation.Invalid");
            var rechazo = _rechazos.Ultimo.Should().NotBeNull().And.Subject.As<RechazoDeForma>();
            rechazo.Comando.Should().Be(nameof(CrearMatrizDePruebaCommand));
            rechazo.Campos.Select(c => c.Campo).Should().BeEquivalentTo(["Code", "Ceiling"]);

            await AuditoriaDeIntentos.RegistrarRechazoDeFormaAsync(servicios, ModuloDeAuditoria.Lending, rechazo, NullLogger.Instance, CancellationToken.None);

            var (modulo, evento) = await UnicoEnElOutbox();
            modulo.Should().Be("Lending");
            evento.Action.Should().Be("Lending.Request.Rejected");
            evento.EntityType.Should().Be(nameof(CrearMatrizDePruebaCommand));
            evento.NewValuesJson.Should().Contain("\"Code\"").And.Contain("MaximumLengthValidator").And.Contain("GreaterThanValidator");
            evento.NewValuesJson.Should().NotContain("secreto-1234567", "un rechazo de forma se audita sin los valores").And.NotContain("-5");
        }

        [Fact]
        public async Task Sin_rechazo_de_forma_no_queda_anotado()
        {
            var validacion = new ValidationBehavior<CrearMatrizDePruebaCommand, Result>([new CrearMatrizDePruebaValidator()], Servicios());

            await validacion.Handle(new CrearMatrizDePruebaCommand(Guid.NewGuid(), "COMITE", 10m), _ => Task.FromResult(Result.Success()), CancellationToken.None);

            _rechazos.Ultimo.Should().BeNull();
        }

        // --------------------------------------------------------------------------------------- rechazo regulatorio --

        [Fact]
        public async Task Un_Result_Failure_regulatorio_queda_Rejected_con_su_codigo()
        {
            await Auditoria<CrearMatrizDePruebaCommand>().Handle(new CrearMatrizDePruebaCommand(Guid.NewGuid(), "COMITE", 10m),
                _ => Task.FromResult(Result.Failure("Lending.Usury.Exceeded", "La tasa supera la usura.")), CancellationToken.None);

            var evento = _enMongo.Should().ContainSingle("sin fábrica de contextos el rechazo va directo a Mongo para no perderse").Subject;
            evento.Action.Should().Be(AuditEventTypes.CommandRejected);
            evento.Module.Should().Be("Lending");
            evento.Metadata.Should().Contain("ErrorCode", "Lending.Usury.Exceeded");
        }

        // -------------------------------------------------------------------------------------- oficina y dispositivo --

        [Fact]
        public async Task Un_comando_IConOficina_lleva_su_oficina_en_el_metadato_Office()
        {
            await Auditoria<DesembolsarDePruebaCommand>().Handle(new DesembolsarDePruebaCommand(Guid.NewGuid(), _norte.PublicId),
                _ => Task.FromResult(Result.Success()), CancellationToken.None);

            (await UnicoEnElOutbox()).Evento.Metadata.Should().Contain("Office", _norte.PublicId.ToString());
        }

        [Fact]
        public async Task Sin_IConOficina_va_la_oficina_por_defecto_del_usuario()
        {
            await Auditoria<CrearMatrizDePruebaCommand>().Handle(new CrearMatrizDePruebaCommand(Guid.NewGuid(), "COMITE", 10m),
                _ => Task.FromResult(Result.Success()), CancellationToken.None);

            (await UnicoEnElOutbox()).Evento.Metadata.Should().Contain("Office", _sur.PublicId.ToString());
        }

        [Fact]
        public async Task El_dispositivo_es_el_User_Agent_mas_X_Dispositivo()
        {
            await Auditoria<CrearMatrizDePruebaCommand>().Handle(new CrearMatrizDePruebaCommand(Guid.NewGuid(), "COMITE", 10m),
                _ => Task.FromResult(Result.Success()), CancellationToken.None);

            var evento = (await UnicoEnElOutbox()).Evento;
            evento.Metadata!["Device"].Should().Contain("MAUI Android 14 · SM-A546").And.Contain("Mozilla/5.0 (Android 14)");
            evento.UserAgent.Should().Be("Mozilla/5.0 (Android 14)");
        }

        [Fact]
        public async Task Sin_X_Dispositivo_el_dispositivo_es_el_User_Agent()
        {
            _origen.Dispositivo.Returns((string?)null);

            await Auditoria<CrearMatrizDePruebaCommand>().Handle(new CrearMatrizDePruebaCommand(Guid.NewGuid(), "COMITE", 10m),
                _ => Task.FromResult(Result.Success()), CancellationToken.None);

            (await UnicoEnElOutbox()).Evento.Metadata.Should().Contain("Device", "Mozilla/5.0 (Android 14)");
        }

        // ---------------------------------------------------------------------------------------------------- módulos --

        [Theory]
        [InlineData("IngenIA365ERP.Application.Lending.Sessions", "Lending")]
        [InlineData("IngenIA365ERP.Application.Lending.Attributions", "Lending")]
        [InlineData("IngenIA365ERP.Application.Compliance.HabeasData", "Compliance")]
        [InlineData("IngenIA365ERP.Application.Common.NationalValues", "NationalValues")]
        public void Los_eventos_van_al_modulo_de_su_espacio_de_nombres(string espacio, string modulo)
        {
            ModuloDeAuditoria.Inferir(espacio).Should().Be(modulo);
            AuditoriaEncadenada.EsEncadenado(modulo).Should().BeTrue();
        }

        // ---------------------------------------------------------------------------------------------- intento denegado --

        [Fact]
        public async Task El_intento_denegado_es_Lending_Access_Denied_con_la_ruta()
        {
            await AuditoriaDeIntentos.RegistrarAccesoDenegadoAsync(Servicios(), ModuloDeAuditoria.Lending, NullLogger.Instance, CancellationToken.None);

            var (modulo, evento) = await UnicoEnElOutbox();
            modulo.Should().Be("Lending");
            evento.Action.Should().Be(AuditEventTypes.LendingAccessDenied);
            evento.Endpoint.Should().Be("POST /api/lending/attribution-matrices");
            evento.HttpStatusCode.Should().Be(404);
        }

        [Fact]
        public async Task Registrar_un_intento_nunca_propaga_un_error()
        {
            var roto = Substitute.For<IServiceProvider>();
            roto.GetService(Arg.Any<Type>()).Throws(new InvalidOperationException("contenedor desechado"));

            var registrar = () => AuditoriaDeIntentos.RegistrarAccesoDenegadoAsync(roto, ModuloDeAuditoria.Lending, NullLogger.Instance, CancellationToken.None);

            await registrar.Should().NotThrowAsync();
        }
    }
}

namespace IngenIA365ERP.Application.Tests.Lending.Audit.ComandosDePrueba
{
    using IngenIA365ERP.Application.Common.Audit;
    using IngenIA365ERP.Application.Common.Behaviors;
    using IngenIA365ERP.Application.Common.Models;
    using MediatR;

    public sealed record CrearMatrizDePruebaCommand(Guid OperationKey, string Code, decimal Ceiling) : IRequest<Result>, IOperacionIdempotente;

    public sealed class CrearMatrizDePruebaValidator : AbstractValidator<CrearMatrizDePruebaCommand>
    {
        public CrearMatrizDePruebaValidator()
        {
            RuleFor(c => c.Code).MaximumLength(10);
            RuleFor(c => c.Ceiling).GreaterThan(0m);
        }
    }

    public sealed record DesembolsarDePruebaCommand(Guid OperationKey, Guid? BranchPublicId) : IRequest<Result>, IOperacionIdempotente, IConOficina;
}
