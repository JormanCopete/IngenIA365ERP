using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Application.Lending.Processes.Tareas;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Processes;

/// <summary>
/// Feature 013, US4, T0479 (DT §2.13, data-model §22.5; SC-020, C123): la tarea <c>cartera.vigilancia</c> sale si Cartera no está activada; pasada la
/// tolerancia levanta <c>Cartera.ProcesoDiarioNoCorrio</c> si el ciclo de ayer no terminó, <c>Cartera.CreditoSinCausar</c> por cada error vigente, y
/// <c>Cartera.TareaDiariaNoCorrio</c> por cada tarea diaria registrada de una entrega vigente cuya última marca (<see cref="MarcaDeTareaDiariaDeCartera"/>,
/// <c>LND_ProcessRuns</c> <c>ScheduledTask</c>) es anterior a ayer; todo idempotente por el <c>DedupKey</c>. La marca la escribe un comando, una por
/// tarea y día.
/// </summary>
public class VigilanciaDeCarteraTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 12, 10);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public VigilanciaDeCarteraTests()
    {
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        A(Hoy, new TimeOnly(7, 0));
        _db.LendingSetups.Add(new LendingSetup { ActivationDate = new DateOnly(2026, 12, 1) });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void A(DateOnly dia, TimeOnly hora)
    {
        _reloj.HoyLocal.Returns(dia);
        var local = new DateTimeOffset(dia.ToDateTime(hora), TimeSpan.FromHours(-5));
        _reloj.AhoraLocal.Returns(local);
        _reloj.UtcNow.Returns(local.UtcDateTime);
    }

    private static readonly ITareaProgramada[] TareasRegistradas = [new TareaDeGirosSinResultado(), new TareaDeGarantiasPorVencer(), new VigilanciaDeCartera()];

    private Task<Result<VigilanciaRealizada>> VigilarAsync(params ITareaProgramada[] tareas) =>
        new WatchLendingProcessesCommandHandler(_db, _alertas, _reloj, tareas.Length == 0 ? TareasRegistradas : tareas).Handle(new WatchLendingProcessesCommand(), default);

    private void Ciclo(DateOnly fecha, LendingProcessRunStatus estado)
    {
        _db.LendingProcessRuns.Add(new LendingProcessRun
        {
            Kind = LendingProcessKind.DailyCycle, ProcessDate = fecha, Status = estado, RequestedByKind = ActorKind.Process,
            ExecutionOrigin = $"Tarea:cartera.diario:{fecha:yyyy-MM-dd}", RequestedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    private async Task MarcarAsync(string tarea, DateOnly dia)
    {
        _reloj.HoyLocal.Returns(dia);
        await new RegisterDailyLendingTaskRunCommandHandler(_db, new MarcaDeTareaDiariaDeCartera(_db, _reloj)).Handle(new RegisterDailyLendingTaskRunCommand(tarea), default);
        _reloj.HoyLocal.Returns(Hoy);
    }

    [Fact]
    public async Task Sin_Cartera_activada_no_hace_nada()
    {
        (await _db.LendingSetups.SingleAsync()).ActivationDate = null;
        await _db.SaveChangesAsync();

        var r = await VigilarAsync();

        r.Value.Should().BeEquivalentTo(new VigilanciaRealizada(false, 0, []));
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task El_ciclo_de_ayer_sin_terminar_alerta_ProcesoDiarioNoCorrio_pasada_la_tolerancia_y_es_idempotente()
    {
        Ciclo(Hoy.AddDays(-1), LendingProcessRunStatus.Running);
        A(Hoy, new TimeOnly(5, 0));
        (await VigilarAsync()).Value.CicloDeAyerSinTerminar.Should().BeFalse("antes de la tolerancia todavía puede terminar");

        A(Hoy, new TimeOnly(7, 0));
        await VigilarAsync();
        await VigilarAsync();

        _levantadas.Where(a => a.TypeCode == TiposDeAlerta.ProcesoDiarioNoCorrio).Should().HaveCount(2)
            .And.OnlyContain(a => a.DedupKey == $"{TiposDeAlerta.ProcesoDiarioNoCorrio}:{Hoy.AddDays(-1):yyyy-MM-dd}", "el DedupKey suma la ocurrencia");
    }

    [Fact]
    public async Task Con_el_ciclo_de_ayer_completo_y_las_tareas_al_dia_no_alerta()
    {
        Ciclo(Hoy.AddDays(-1), LendingProcessRunStatus.CompletedWithErrors);
        await MarcarAsync(TareaDeGirosSinResultado.NombreDeLaTarea, Hoy.AddDays(-1));
        await MarcarAsync(TareaDeGarantiasPorVencer.NombreDeLaTarea, Hoy);

        var r = await VigilarAsync();

        r.Value.Should().BeEquivalentTo(new VigilanciaRealizada(false, 0, []));
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Los_creditos_con_error_vigente_alertan_CreditoSinCausar()
    {
        Ciclo(Hoy.AddDays(-1), LendingProcessRunStatus.CompletedWithErrors);
        var credito = new Loan { Number = "CR-1", DebtorPersonId = 1, BranchId = 1, CreditLineId = 1, Status = LoanStatus.Active, OpenedOn = Hoy };
        _db.Loans.Add(credito);
        await _db.SaveChangesAsync();
        var corrida = await _db.LendingProcessRuns.FirstAsync();
        _db.LendingProcessRunErrors.Add(new LendingProcessRunError { ProcessRunId = corrida.Id, LoanId = credito.Id, BatchNumber = 1, AttemptNumber = 1, Step = "Causacion",
            ErrorCode = "X", ErrorMessage = "falló", OccurredAt = DateTime.UtcNow });
        _db.LendingProcessRunErrors.Add(new LendingProcessRunError { ProcessRunId = corrida.Id, LoanId = credito.Id, BatchNumber = 1, AttemptNumber = 2, Step = "Causacion",
            ErrorCode = "X", ErrorMessage = "resuelto", OccurredAt = DateTime.UtcNow, ResolvedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var r = await VigilarAsync(new TareaDeGirosSinResultado());

        r.Value.CreditosSinCausar.Should().Be(1);
        _levantadas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.CreditoSinCausar).Which.DedupKey.Should().Be($"{TiposDeAlerta.CreditoSinCausar}:{credito.Id}");
    }

    [Theory]
    [InlineData("cartera.giros-sin-resultado")]
    [InlineData("cartera.garantias-por-vencer")]
    public async Task Una_tarea_diaria_sin_su_marca_de_ayer_alerta_TareaDiariaNoCorrio(string tarea)
    {
        Ciclo(Hoy.AddDays(-1), LendingProcessRunStatus.Completed);
        var otra = tarea == TareaDeGirosSinResultado.NombreDeLaTarea ? TareaDeGarantiasPorVencer.NombreDeLaTarea : TareaDeGirosSinResultado.NombreDeLaTarea;
        await MarcarAsync(otra, Hoy.AddDays(-1));
        await MarcarAsync(tarea, Hoy.AddDays(-3));

        var r = await VigilarAsync();

        r.Value.TareasSinCorrer.Should().Equal(tarea);
        _levantadas.Should().ContainSingle(a => a.TypeCode == TiposDeAlerta.TareaDiariaNoCorrio)
            .Which.DedupKey.Should().Be($"{TiposDeAlerta.TareaDiariaNoCorrio}:{tarea}:{Hoy.AddDays(-1):yyyy-MM-dd}");
    }

    [Fact]
    public async Task No_vigila_las_tareas_que_no_estan_registradas_ni_las_de_una_entrega_todavia_no_vigente()
    {
        Ciclo(Hoy.AddDays(-1), LendingProcessRunStatus.Completed);

        var r = await VigilarAsync(new TareaDeGirosSinResultado());

        r.Value.TareasSinCorrer.Should().Equal(TareaDeGirosSinResultado.NombreDeLaTarea);
        MarcaDeTareaDiariaDeCartera.Diarias.Where(t => t.Entrega == Domain.Lending.EntregaDeCartera.K2).Select(t => t.Nombre)
            .Should().BeEquivalentTo(["cartera.archivos-de-descuento", "cartera.giros-de-pagaduria"]);
    }

    [Fact]
    public async Task La_marca_es_una_por_tarea_y_dia_y_la_escribe_el_comando()
    {
        await MarcarAsync(TareaDeGirosSinResultado.NombreDeLaTarea, Hoy);
        await MarcarAsync(TareaDeGirosSinResultado.NombreDeLaTarea, Hoy);

        var marca = await _db.LendingProcessRuns.SingleAsync(r => r.Kind == LendingProcessKind.ScheduledTask);
        marca.Should().BeEquivalentTo(new
        {
            ProcessDate = (DateOnly?)Hoy, Status = LendingProcessRunStatus.Completed, ExecutionOrigin = "Tarea:cartera.giros-sin-resultado",
            RequestedByKind = ActorKind.Process,
        });
        await FluentActions.Invoking(() => new MarcaDeTareaDiariaDeCartera(_db, _reloj).MarcarAsync("cartera.otra", default))
            .Should().ThrowAsync<ArgumentException>();
        new VigilanciaDeCartera().DebeCorrer(new DateTimeOffset(2026, 12, 10, 8, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 12, 10, 7, 30, 0, TimeSpan.FromHours(-5))).Should().BeFalse("cada hora");
    }
}
