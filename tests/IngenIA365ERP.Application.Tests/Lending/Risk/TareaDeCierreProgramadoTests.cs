using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0685 (T0707; pregunta E6: por defecto manual con alerta): <c>cartera.cierre-programado</c> ordena la corrida <c>MonthlyClose</c>
/// del mes anterior sólo con <c>Riesgo.CierreProgramado</c> encendido y desde <c>Riesgo.DiaLimiteCierre</c>, como el proceso; con el mes ya cerrado
/// o con su corrida pedida, nada; si un requisito falla, alerta <c>Cartera.CierreProgramadoFallido</c> con el motivo, una por mes.
/// </summary>
public class TareaDeCierreProgramadoTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task ProgramadoAsync()
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.RiesgoCierreProgramado, ScopeKind = ParameterScopeKind.None, Value = "true",
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = _c.G.UtcNow.AddDays(-10),
        });
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
    }

    private Task<Result<CierreProgramadoRealizado>> CorrerAsync()
    {
        _c.G.ActuarComoProceso();
        return new RunScheduledMonthlyCloseCommandHandler(_c.Db, _c.Requisitos(), _c.G.Actor, _alertas, _c.G.Reloj)
            .Handle(new RunScheduledMonthlyCloseCommand(), default);
    }

    [Fact]
    public async Task Apagado_por_defecto_no_ordena_nada()
    {
        await _c.CausarDiciembreAsync();
        _c.IrA(new DateOnly(2027, 1, 10));

        (await CorrerAsync()).Value.Should().Be(new CierreProgramadoRealizado(false, null, null));
        (await _c.Db.LendingProcessRuns.CountAsync(r => r.Kind == LendingProcessKind.MonthlyClose)).Should().Be(0);
    }

    [Fact]
    public async Task Encendido_ordena_el_mes_anterior_desde_el_dia_limite_y_una_sola_vez()
    {
        await ProgramadoAsync();
        await _c.CausarDiciembreAsync();
        _c.IrA(new DateOnly(2027, 1, 9));
        (await CorrerAsync()).Value.Ordenado.Should().BeFalse("antes del día límite (10) no");

        _c.IrA(new DateOnly(2027, 1, 10));
        var r = await CorrerAsync();

        r.Value.Ordenado.Should().BeTrue(r.Value.ErrorCode);
        var corrida = await _c.Db.LendingProcessRuns.AsNoTracking().SingleAsync(x => x.Kind == LendingProcessKind.MonthlyClose);
        (corrida.PublicId, corrida.PeriodYear, corrida.PeriodMonth, corrida.Status).Should()
            .Be((r.Value.ProcessRunPublicId!.Value, (short?)2026, (byte?)12, LendingProcessRunStatus.Requested));
        (await CorrerAsync()).Value.Ordenado.Should().BeFalse("con la corrida pedida el mes ya no está pendiente");
        _levantadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_un_requisito_falla_alerta_CierreProgramadoFallido_con_el_motivo()
    {
        await ProgramadoAsync();
        await _c.C.CausarHastaAsync(new DateOnly(2026, 12, 20));
        _c.IrA(new DateOnly(2027, 1, 10));

        var r = await CorrerAsync();

        r.Value.Should().Be(new CierreProgramadoRealizado(false, null, ErroresDeRiesgo.CodigoCausacionAtrasada));
        var alerta = _levantadas.Should().ContainSingle().Subject;
        (alerta.TypeCode, alerta.DedupKey).Should().Be((TiposDeAlerta.CierreProgramadoFallido, $"{TiposDeAlerta.CierreProgramadoFallido}:2026-12"));
    }

    [Fact]
    public void La_tarea_corre_una_vez_al_dia()
    {
        var tarea = new TareaDeCierreProgramado();
        tarea.Nombre.Should().Be("cartera.cierre-programado");
        var hoy = new DateTimeOffset(2027, 1, 10, 8, 0, 0, TimeSpan.FromHours(-5));
        tarea.DebeCorrer(hoy, hoy.AddHours(-1)).Should().BeFalse();
        tarea.DebeCorrer(hoy, hoy.AddDays(-1)).Should().BeTrue();
    }
}
