using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Behaviors;

/// <summary>
/// T017 (feature 012; decisiones-transversales T13; contracts/api.md §2.3): lo de
/// <see cref="IdempotencyBehavior{TRequest, TResponse}"/> que no depende de la transacción. Sobre InMemory,
/// que ignora las transacciones: que un fallo revierta la fila y que un duplicado concurrente espere en el
/// índice único lo prueba la e2e <c>IdempotenciaDeOperacionesTests</c> (T021) en los dos motores.
/// </summary>
public class IdempotencyBehaviorTests
{
    public sealed record AltaDePrueba(Guid OperationKey, string Clave, string Valor, int Orden) : IRequest<Result<RespuestaDePrueba>>, IOperacionIdempotente;

    public sealed record OtraAltaDePrueba(Guid OperationKey, string Clave, string Valor, int Orden) : IRequest<Result<RespuestaDePrueba>>, IOperacionIdempotente;

    public sealed record AnularDePrueba(Guid OperationKey, string Reason) : IRequest<Result>, IOperacionIdempotente;

    public sealed record ComandoSinMarcador(string Valor) : IRequest<Result<RespuestaDePrueba>>;

    public sealed record RespuestaDePrueba(Guid PublicId, string Valor, decimal Monto);

    private static readonly Guid Central = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAuditService _auditoria = Substitute.For<IAuditService>();
    private readonly EstadoDeLaOperacion _estado = new();
    private Actor _actor = Persona(Central, 7, "Ana Operadora");

    private static Actor Persona(Guid central, int userId, string nombre) =>
        new(ActorKind.Person, userId, Guid.NewGuid(), central, nombre, "ana@coop.test", ExecutionChannel.Web, "POST /prueba", "10.0.0.1", null);

    private IServiceProvider Servicios(bool conBase = true)
    {
        var servicios = new ServiceCollection();
        if (conBase) servicios.AddSingleton<IApplicationDbContext>(_db);
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_actor));
        servicios.AddSingleton(actor);
        servicios.AddSingleton(_auditoria);
        servicios.AddSingleton(_estado);
        return servicios.BuildServiceProvider();
    }

    private IdempotencyBehavior<TPedido, TRespuesta> Behavior<TPedido, TRespuesta>(bool conBase = true) where TPedido : notnull =>
        new(Servicios(conBase), NullLogger<IdempotencyBehavior<TPedido, TRespuesta>>.Instance);

    private static RespuestaDePrueba Respuesta(string valor) => new(Guid.NewGuid(), valor, 1234.56m);

    [Fact]
    public async Task Sin_clave_responde_Operation_KeyRequired_sin_ejecutar()
    {
        var behavior = Behavior<AltaDePrueba, Result<RespuestaDePrueba>>();
        var ejecutado = false;

        var r = await behavior.Handle(new AltaDePrueba(Guid.Empty, "X", "1", 1), _ =>
        {
            ejecutado = true;
            return Task.FromResult(Result.Success(Respuesta("1")));
        }, CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeOperacion.CodigoClaveRequerida);
        ejecutado.Should().BeFalse();
        (await _db.OperationKeys.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_clave_un_comando_sin_valor_tambien_responde_KeyRequired()
    {
        var behavior = Behavior<AnularDePrueba, Result>();

        var r = await behavior.Handle(new AnularDePrueba(Guid.Empty, "motivo"), _ => Task.FromResult(Result.Success()), CancellationToken.None);

        r.Error.Code.Should().Be("Operation.KeyRequired");
    }

    [Fact]
    public async Task Un_comando_sin_el_marcador_pasa_sin_tocar_COR_OperationKeys()
    {
        // Sin base registrada: si el behavior la pidiera para un comando no marcado, fallaría al resolverla
        // (el login corre sin cooperativa y pasa por aquí).
        var behavior = Behavior<ComandoSinMarcador, Result<RespuestaDePrueba>>(conBase: false);
        var llamadas = 0;

        var r = await behavior.Handle(new ComandoSinMarcador("a"), _ =>
        {
            llamadas++;
            return Task.FromResult(Result.Success(Respuesta("a")));
        }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        llamadas.Should().Be(1);
        (await _db.OperationKeys.CountAsync()).Should().Be(0);
        _estado.EsRepeticion.Should().BeFalse();
    }

    [Fact]
    public void La_huella_no_cambia_con_el_orden_de_las_propiedades()
    {
        var a = HuellaDeOperacion.Calcular("AddParameterVersionCommand", new { module = "INV", key = "K", value = "true", nested = new { b = 2, a = 1 } });
        var b = HuellaDeOperacion.Calcular("AddParameterVersionCommand", new { value = "true", nested = new { a = 1, b = 2 }, key = "K", module = "INV" });

        a.Should().Be(b);
        a.Should().MatchRegex("^[0-9a-f]{64}$", "es char(64) en hexadecimal");
    }

    [Fact]
    public void La_huella_cambia_con_el_contenido_y_con_la_operacion_pero_no_con_la_clave()
    {
        var clave1 = new AltaDePrueba(Guid.NewGuid(), "K", "true", 1);
        var clave2 = clave1 with { OperationKey = Guid.NewGuid() };
        var otroValor = clave1 with { Valor = "false" };

        HuellaDeOperacion.Calcular("Alta", clave1).Should().Be(HuellaDeOperacion.Calcular("Alta", clave2), "la clave no es contenido");
        HuellaDeOperacion.Calcular("Alta", clave1).Should().NotBe(HuellaDeOperacion.Calcular("Alta", otroValor));
        HuellaDeOperacion.Calcular("Alta", clave1).Should().NotBe(HuellaDeOperacion.Calcular("Otra", clave1));
    }

    [Fact]
    public void La_huella_respeta_el_orden_de_los_arreglos()
    {
        var a = HuellaDeOperacion.Calcular("Doc", new { lines = new[] { 1, 2 } });
        var b = HuellaDeOperacion.Calcular("Doc", new { lines = new[] { 2, 1 } });

        a.Should().NotBe(b, "dos líneas en otro orden son otro documento");
    }

    [Fact]
    public async Task La_primera_vez_ejecuta_y_guarda_la_clave_con_el_resultado()
    {
        var behavior = Behavior<AltaDePrueba, Result<RespuestaDePrueba>>();
        var pedido = new AltaDePrueba(Guid.NewGuid(), "K", "true", 1);
        var respuesta = Respuesta("true");

        var r = await behavior.Handle(pedido, _ => Task.FromResult(Result.Success(respuesta)), CancellationToken.None);

        r.Value.Should().Be(respuesta);
        var fila = await _db.OperationKeys.SingleAsync();
        fila.Key.Should().Be(pedido.OperationKey);
        fila.Operation.Should().Be(nameof(AltaDePrueba));
        fila.RequestSha256.Should().Be(HuellaDeOperacion.Calcular(nameof(AltaDePrueba), pedido));
        fila.CentralUserId.Should().Be(Central);
        fila.UserId.Should().Be(7, "es SEC_Users.Id de IActorActual, no el entero del token");
        fila.ActorName.Should().Be("Ana Operadora");
        fila.ResultJson.Should().Contain("1234.56");
        _estado.EsRepeticion.Should().BeFalse();
    }

    [Fact]
    public async Task La_repeticion_devuelve_el_resultado_guardado_sin_ejecutar_y_lo_marca()
    {
        var behavior = Behavior<AltaDePrueba, Result<RespuestaDePrueba>>();
        var pedido = new AltaDePrueba(Guid.NewGuid(), "K", "true", 1);
        var primera = Respuesta("true");
        await behavior.Handle(pedido, _ => Task.FromResult(Result.Success(primera)), CancellationToken.None);
        var ejecutadoOtraVez = false;

        var r = await behavior.Handle(pedido, _ =>
        {
            ejecutadoOtraVez = true;
            return Task.FromResult(Result.Success(Respuesta("otra")));
        }, CancellationToken.None);

        ejecutadoOtraVez.Should().BeFalse();
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(primera, "la repetición responde lo mismo que la primera vez");
        _estado.EsRepeticion.Should().BeTrue();
        (await _db.OperationKeys.CountAsync()).Should().Be(1);
        await _auditoria.Received(1).LogAsync(
            Arg.Is<AuditLogCommand>(c => c.Action == AuditEventTypes.OperationReplayed
                && c.Metadata!["OperationKey"] == pedido.OperationKey.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_repeticion_de_un_comando_sin_valor_responde_exito()
    {
        var behavior = Behavior<AnularDePrueba, Result>();
        var pedido = new AnularDePrueba(Guid.NewGuid(), "Ya no se necesita");
        await behavior.Handle(pedido, _ => Task.FromResult(Result.Success()), CancellationToken.None);

        var r = await behavior.Handle(pedido, _ => throw new InvalidOperationException("no debía ejecutarse"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        _estado.EsRepeticion.Should().BeTrue();
    }

    [Fact]
    public async Task La_misma_clave_con_otro_contenido_es_Operation_KeyReused()
    {
        var behavior = Behavior<AltaDePrueba, Result<RespuestaDePrueba>>();
        var pedido = new AltaDePrueba(Guid.NewGuid(), "K", "true", 1);
        await behavior.Handle(pedido, _ => Task.FromResult(Result.Success(Respuesta("true"))), CancellationToken.None);

        var r = await behavior.Handle(pedido with { Valor = "false" }, _ => Task.FromResult(Result.Success(Respuesta("false"))), CancellationToken.None);

        r.Error.Code.Should().Be(ErroresDeOperacion.CodigoClaveReutilizada);
        var datos = r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data;
        datos.GetType().GetProperty("operation")!.GetValue(datos).Should().Be(nameof(AltaDePrueba));
        datos.GetType().GetProperty("firstUsedAt")!.GetValue(datos).Should().BeOfType<DateTime>();
        _estado.EsRepeticion.Should().BeFalse();
    }

    [Fact]
    public async Task La_misma_clave_en_otra_operacion_es_Operation_KeyReused()
    {
        var clave = Guid.NewGuid();
        await Behavior<AltaDePrueba, Result<RespuestaDePrueba>>()
            .Handle(new AltaDePrueba(clave, "K", "true", 1), _ => Task.FromResult(Result.Success(Respuesta("true"))), CancellationToken.None);

        var r = await Behavior<OtraAltaDePrueba, Result<RespuestaDePrueba>>()
            .Handle(new OtraAltaDePrueba(clave, "K", "true", 1), _ => Task.FromResult(Result.Success(Respuesta("true"))), CancellationToken.None);

        r.Error.Code.Should().Be("Operation.KeyReused");
    }

    [Fact]
    public async Task La_misma_clave_de_otro_usuario_es_Operation_KeyReused()
    {
        var pedido = new AltaDePrueba(Guid.NewGuid(), "K", "true", 1);
        await Behavior<AltaDePrueba, Result<RespuestaDePrueba>>()
            .Handle(pedido, _ => Task.FromResult(Result.Success(Respuesta("true"))), CancellationToken.None);
        _actor = Persona(Guid.NewGuid(), 8, "Otro Usuario");

        var r = await Behavior<AltaDePrueba, Result<RespuestaDePrueba>>()
            .Handle(pedido, _ => Task.FromResult(Result.Success(Respuesta("true"))), CancellationToken.None);

        r.Error.Code.Should().Be("Operation.KeyReused");
    }

    [Fact]
    public async Task Un_fallo_no_guarda_resultado()
    {
        var behavior = Behavior<AltaDePrueba, Result<RespuestaDePrueba>>();
        var pedido = new AltaDePrueba(Guid.NewGuid(), "K", "Lunar", 1);

        var r = await behavior.Handle(pedido,
            _ => Task.FromResult(Result.Failure<RespuestaDePrueba>("Parameters.ValueNotAllowed", "no admitido")), CancellationToken.None);

        r.Error.Code.Should().Be("Parameters.ValueNotAllowed");
        // En un motor real la transacción revierte y la fila no queda (e2e T021); InMemory no revierte,
        // pero el resultado nunca se escribe para un fallo.
        (await _db.OperationKeys.Where(k => k.ResultJson != null).CountAsync()).Should().Be(0);
    }
}
