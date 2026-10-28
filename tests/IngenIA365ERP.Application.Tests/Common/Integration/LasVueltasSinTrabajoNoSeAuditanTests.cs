using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// Incidente del 2026-10-26: el despachador envía <see cref="ScheduleIntegrationBatchesCommand"/> en cada vuelta (cada 5 s) y cada
/// vuelta dejaba un evento en <c>COR_AuditOutbox</c> aunque no programara nada —827.840 de ruido en una cooperativa—. Una vuelta sin
/// trabajo ya no audita; una con trabajo audita con el detalle de lo programado (Principio X: nada con efecto deja de auditarse).
/// Lo mismo para la réplica de valores nacionales, que el programador envía cada minuto.
/// </summary>
public class LasVueltasSinTrabajoNoSeAuditanTests
{
    private static readonly Guid Cooperativa = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
    private readonly IAuditService _mongo = Substitute.For<IAuditService>();

    [Fact]
    public async Task Una_vuelta_sin_trabajo_no_audita()
    {
        var r = await Programar(new LotesProgramadosDto([], []));

        r.IsSuccess.Should().BeTrue();
        (await _db.AuditOutbox.CountAsync()).Should().Be(0, "una vuelta que no programó nada no deja evento");
        await _mongo.DidNotReceiveWithAnyArgs().LogAsync(default(AuditLogCommand)!, default);
    }

    [Fact]
    public async Task Una_vuelta_que_solo_ve_atrasados_no_audita()
    {
        var atrasado = new LoteProgramadoDto(Guid.NewGuid(), 7, "COMPRA|HoraDiaria|23:00|Resumido", new DateTime(2026, 10, 25, 23, 0, 0), 3);

        await Programar(new LotesProgramadosDto([], [atrasado]));

        (await _db.AuditOutbox.CountAsync()).Should().Be(0, "el comando no toca los atrasados; la alerta se audita sola");
    }

    [Fact]
    public async Task Una_vuelta_con_trabajo_audita_con_el_detalle_de_lo_programado()
    {
        var lote = new LoteProgramadoDto(Guid.NewGuid(), 42, "COMPRA|HoraDiaria|23:00|Resumido", new DateTime(2026, 10, 26, 23, 0, 0), 5);

        await Programar(new LotesProgramadosDto([lote], []));

        var fila = (await _db.AuditOutbox.ToListAsync()).Should().ContainSingle().Subject;
        fila.Module.Should().Be(ModuloDeAuditoria.Integration);
        var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
        evento.Action.Should().Be("ScheduleIntegrationBatches");
        evento.NewValuesJson.Should().Contain("\"lateToleranceMinutes\":30", "sigue diciendo qué se pidió")
            .And.Contain(lote.BatchPublicId.ToString())
            .And.Contain("\"number\":42")
            .And.Contain("COMPRA|HoraDiaria|23:00|Resumido")
            .And.Contain("\"messages\":5");
    }

    [Fact]
    public async Task Un_rechazo_se_sigue_auditando_aunque_no_haya_efecto()
    {
        await Behavior<ScheduleIntegrationBatchesCommand, Result<LotesProgramadosDto>>().Handle(new ScheduleIntegrationBatchesCommand(30),
            _ => Task.FromResult(Result.Failure<LotesProgramadosDto>("Integration.Prueba", "Falló.")), CancellationToken.None);

        await _mongo.Received(1).LogAsync(Arg.Is<AuditLogCommand>(e => e.Action == AuditEventTypes.CommandRejected), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void El_resultado_de_la_pasada_dice_si_hubo_efecto()
    {
        var lote = new LoteProgramadoDto(Guid.NewGuid(), 1, "C", DateTime.Today, 1);

        new LotesProgramadosDto([], []).HuboEfecto.Should().BeFalse();
        new LotesProgramadosDto([], [lote]).HuboEfecto.Should().BeFalse();
        new LotesProgramadosDto([lote], []).HuboEfecto.Should().BeTrue();
    }

    [Fact]
    public async Task Una_replica_sin_novedad_no_audita_y_una_que_copio_si()
    {
        var comando = new ReplicateNationalValuesCommand(NationalValueReplicationOrigin.ScheduledTask);
        var behavior = Behavior<ReplicateNationalValuesCommand, Result<ResultadoDeReplicacion>>();

        await behavior.Handle(comando, _ => Task.FromResult(Result.Success(ResultadoDeReplicacion.SinNovedad)), CancellationToken.None);
        (await _db.AuditOutbox.CountAsync()).Should().Be(0, "la pasada de cada minuto sin novedad no deja evento");

        var copio = new ResultadoDeReplicacion(true, null, 2, 0, 10, 11, Guid.NewGuid(), new Dictionary<string, EfectoAplicado>());
        await behavior.Handle(comando, _ => Task.FromResult(Result.Success(copio)), CancellationToken.None);

        var fila = (await _db.AuditOutbox.ToListAsync()).Should().ContainSingle().Subject;
        fila.Module.Should().Be(ModuloDeAuditoria.NationalValues);
        AuditoriaEncadenada.LeerCarga(fila.PayloadJson!).NewValuesJson.Should().Contain("\"valores\":2");
    }

    [Fact]
    public void Una_replica_con_la_central_caida_es_un_hecho_que_se_audita()
    {
        ResultadoDeReplicacion.SinNovedad.HuboEfecto.Should().BeFalse();
        ResultadoDeReplicacion.Fallida(false, "La central no respondió.").HuboEfecto.Should().BeTrue();
        ResultadoDeReplicacion.Fallida(true, "Un efecto falló.").HuboEfecto.Should().BeTrue();
    }

    // ------------------------------------------------------------ ayudantes --

    private Task<Result<LotesProgramadosDto>> Programar(LotesProgramadosDto resultado) =>
        Behavior<ScheduleIntegrationBatchesCommand, Result<LotesProgramadosDto>>().Handle(new ScheduleIntegrationBatchesCommand(30),
            _ => Task.FromResult(Result.Success(resultado)), CancellationToken.None);

    private AuditBehavior<TPedido, TRespuesta> Behavior<TPedido, TRespuesta>() where TPedido : notnull
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IApplicationDbContext>(_db);
        var cooperativa = Substitute.For<ICurrentTenantService>();
        cooperativa.TenantId.Returns(Cooperativa.ToString("N"));
        servicios.AddSingleton(cooperativa);
        var origen = Substitute.For<IOrigenDeLaPeticion>();
        origen.Canal.Returns(ExecutionChannel.Process);
        origen.Origen.Returns("Tarea:integration.dispatch");
        servicios.AddSingleton(origen);
        var actor = Substitute.For<IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(Actor.ProcesoDeIntegracion("Tarea:integration.dispatch")));
        servicios.AddSingleton(actor);
        return new AuditBehavior<TPedido, TRespuesta>(_mongo, Payroll.Common.NominaTestData.UsuarioDePrueba("proceso", 0),
            NullLogger<AuditBehavior<TPedido, TRespuesta>>.Instance, servicios.BuildServiceProvider());
    }
}
