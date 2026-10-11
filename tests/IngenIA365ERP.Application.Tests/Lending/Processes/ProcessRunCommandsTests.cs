using FluentAssertions;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Processes;

/// <summary>
/// Feature 013, US4, T0475 (api.md §16.4; data-model §22.1; T5, T8): <see cref="RequestLendingProcessRunCommand"/> sólo encola un <c>DailyCycle</c>
/// (202) con la persona como quien lo ordenó; otra corrida de la misma clase y fecha pedida o corriendo responde
/// <c>Lending.Process.AlreadyRunning</c>; el cierre y la corrección no se ordenan aquí (<c>Lending.ProcessRun.KindNotOrderable</c>);
/// <see cref="RetryLendingProcessRunCommand"/> con motivo sólo vuelve a encolar una corrida <c>Failed</c> o <c>CompletedWithErrors</c>
/// (<c>Lending.ProcessRun.NotRetryable</c>) y retoma desde la marca de cada crédito. La lista y el detalle no traen datos del deudor.
/// </summary>
public class ProcessRunCommandsTests : IDisposable
{
    private readonly GobiernoDePrueba _g = new();

    public ProcessRunCommandsTests()
    {
        var jefe = _g.Usuario(_g.Persona("Juana", "Cartera", "9001"), "jefe@coop.test");
        _g.ActuarComo(jefe);
    }

    public void Dispose() => _g.Dispose();

    private RequestLendingProcessRunCommandHandler Pedir() => new(_g.Db, _g.Actor, _g.Reloj);
    private RetryLendingProcessRunCommandHandler Reintentar() => new(_g.Db, _g.Actor, _g.Reloj);

    private static RequestLendingProcessRunCommand Orden(LendingProcessKind clase, DateOnly? fecha = null) =>
        new(clase, fecha) { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task Ordenar_el_ciclo_solo_encola_con_la_persona_como_quien_lo_pidio()
    {
        var r = await Pedir().Handle(Orden(LendingProcessKind.DailyCycle), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(nameof(LendingProcessRunStatus.Requested));
        var corrida = await _g.Db.LendingProcessRuns.SingleAsync();
        corrida.Should().BeEquivalentTo(new
        {
            Kind = LendingProcessKind.DailyCycle, ProcessDate = (DateOnly?)GobiernoDePrueba.Hoy, Status = LendingProcessRunStatus.Requested,
            RequestedByKind = ActorKind.Person, RequestedByName = "jefe@coop.test", ProcessedLoans = 0,
        });
        corrida.ExecutionOrigin.Should().StartWith("Orden:");
        corrida.RequestedByUserId.Should().NotBeNull();
        (await _g.Db.LoanTransactions.CountAsync()).Should().Be(0, "sólo encola: no causa nada");
    }

    [Fact]
    public async Task Otra_de_la_misma_clase_y_fecha_pedida_o_corriendo_es_AlreadyRunning_y_una_terminada_AlreadyCompleted()
    {
        await Pedir().Handle(Orden(LendingProcessKind.DailyCycle), default);

        (await Pedir().Handle(Orden(LendingProcessKind.DailyCycle), default)).Error.Code.Should().Be(ErroresDeCorridas.CodigoEnCurso);

        var corrida = await _g.Db.LendingProcessRuns.SingleAsync();
        corrida.Status = LendingProcessRunStatus.Running;
        await _g.Db.SaveChangesAsync();
        (await Pedir().Handle(Orden(LendingProcessKind.DailyCycle), default)).Error.Code.Should().Be(ErroresDeCorridas.CodigoEnCurso);

        corrida.Status = LendingProcessRunStatus.Completed;
        await _g.Db.SaveChangesAsync();
        (await Pedir().Handle(Orden(LendingProcessKind.DailyCycle), default)).Error.Code.Should().Be(ErroresDeCorridas.CodigoYaTerminada);
        (await Pedir().Handle(Orden(LendingProcessKind.DailyCycle, GobiernoDePrueba.Hoy.AddDays(-1)), default)).IsSuccess.Should().BeTrue(
            "otra fecha es otra corrida");
    }

    [Theory]
    [InlineData(LendingProcessKind.MonthlyClose)]
    [InlineData(LendingProcessKind.CloseReopen)]
    [InlineData(LendingProcessKind.NationalValueCorrection)]
    public async Task El_cierre_su_reapertura_y_la_correccion_no_se_ordenan_aqui(LendingProcessKind clase)
    {
        var r = await Pedir().Handle(Orden(clase), default);

        r.Error.Code.Should().Be(ErroresDeCorridas.CodigoClaseNoOrdenable);
        (await _g.Db.LendingProcessRuns.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(LendingProcessRunStatus.Requested, false)]
    [InlineData(LendingProcessRunStatus.Running, false)]
    [InlineData(LendingProcessRunStatus.Completed, false)]
    [InlineData(LendingProcessRunStatus.Failed, true)]
    [InlineData(LendingProcessRunStatus.CompletedWithErrors, true)]
    public async Task Reintentar_con_motivo_solo_una_fallida_o_con_errores(LendingProcessRunStatus estado, bool reintentable)
    {
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.DailyCycle, ProcessDate = GobiernoDePrueba.Hoy, Status = estado, RequestedByKind = ActorKind.Process,
            ExecutionOrigin = "Tarea:cartera.diario:2026-12-01", RequestedAt = GobiernoDePrueba.Ahora.AddHours(-10), CursorLoanId = 40, FailedLoans = 2,
            InformationCutoffAt = GobiernoDePrueba.Ahora.AddHours(-10),
        };
        _g.Db.LendingProcessRuns.Add(corrida);
        await _g.Db.SaveChangesAsync();

        var r = await Reintentar().Handle(new RetryLendingProcessRunCommand(corrida.PublicId, "se replicó la usura") { OperationKey = Guid.NewGuid() }, default);

        if (!reintentable)
        {
            r.Error.Code.Should().Be(ErroresDeCorridas.CodigoNoReintentable);
            return;
        }
        r.IsSuccess.Should().BeTrue();
        var leida = await _g.Db.LendingProcessRuns.AsNoTracking().SingleAsync();
        leida.Should().BeEquivalentTo(new
        {
            Status = LendingProcessRunStatus.Requested, RetryCount = 1, Reason = "se replicó la usura", CursorLoanId = (int?)null, FailedLoans = 0,
            InformationCutoffAt = (DateTime?)null, RequestedByKind = ActorKind.Person,
        }, "retoma desde la marca de agua de cada crédito");
    }

    [Fact]
    public async Task La_lista_y_el_detalle_muestran_el_avance_y_los_errores_sin_datos_del_deudor()
    {
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.DailyCycle, ProcessDate = GobiernoDePrueba.Hoy, Status = LendingProcessRunStatus.CompletedWithErrors,
            RequestedByKind = ActorKind.Process, ExecutionOrigin = "Tarea:cartera.diario:2026-12-01", RequestedAt = GobiernoDePrueba.Ahora,
            TotalLoans = 3, ProcessedLoans = 2, FailedLoans = 1,
        };
        _g.Db.LendingProcessRuns.Add(corrida);
        await _g.Db.SaveChangesAsync();
        _g.Db.LendingProcessRunErrors.Add(new LendingProcessRunError
        {
            ProcessRunId = corrida.Id, LoanId = null, BatchNumber = 1, AttemptNumber = 1, Step = "Causacion", ErrorCode = "NationalValues.Missing",
            ErrorMessage = "Falta USURA", OccurredAt = GobiernoDePrueba.Ahora,
        });
        await _g.Db.SaveChangesAsync();

        var lista = await new ListLendingProcessRunsQueryHandler(_g.Db).Handle(new ListLendingProcessRunsQuery(LendingProcessKind.DailyCycle), default);
        var detalle = await new GetLendingProcessRunQueryHandler(_g.Db).Handle(new GetLendingProcessRunQuery(corrida.PublicId), default);

        lista.Value.Items.Single().Should().BeEquivalentTo(new { Kind = "DailyCycle", Status = "CompletedWithErrors", Progress = new { Done = 3, Total = 3 }, Errors = 1 });
        detalle.Value.Errors.Items.Single().Should().BeEquivalentTo(new { LoanPublicId = (Guid?)null, Code = "NationalValues.Missing", Attempts = 1 });
        (await new GetLendingProcessRunQueryHandler(_g.Db).Handle(new GetLendingProcessRunQuery(Guid.NewGuid()), default)).Error.Code
            .Should().Be(ErroresDeCorridas.CodigoInexistente);
    }
}
