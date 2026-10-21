using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Application.Lending.Processes.Tareas;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Lo que hizo una pasada del cierre programado. (nuevo)</summary>
public sealed record CierreProgramadoRealizado(bool Ordenado, Guid? ProcessRunPublicId, string? ErrorCode);

/// <summary>
/// El cierre programado (feature 013, US9, T0707; api.md §12.3; pregunta E6: por defecto manual con alerta): <b>sin ruta</b>, lo corre la tarea
/// <c>cartera.cierre-programado</c> una vez al día. Sale si Cartera no está activada o <c>Riesgo.CierreProgramado</c> está apagado. Desde el día
/// <c>Riesgo.DiaLimiteCierre</c> del mes, si el mes anterior no tiene cierre vigente ni una corrida de cierre, lo ordena como el proceso
/// (<see cref="OrderMonthlyCloseCommandHandler"/>, origen <c>Tarea:cartera.cierre-programado</c>); si un requisito falla, alerta
/// <c>Cartera.CierreProgramadoFallido</c> con el motivo (<c>DedupKey</c> por mes). (nuevo)
/// </summary>
public sealed record RunScheduledMonthlyCloseCommand : IRequest<Result<CierreProgramadoRealizado>>;

public sealed class RunScheduledMonthlyCloseCommandValidator : AbstractValidator<RunScheduledMonthlyCloseCommand>;

public sealed class RunScheduledMonthlyCloseCommandHandler(
    IApplicationDbContext db,
    RequisitosDelCierreMensual requisitos,
    IActorActual actorActual,
    IAlertas alertas,
    IDateTimeService reloj,
    ILectorDeParametros? parametros = null,
    ReplicacionDeValoresNacionales? replicacion = null) : IRequestHandler<RunScheduledMonthlyCloseCommand, Result<CierreProgramadoRealizado>>
{
    private static readonly CierreProgramadoRealizado Nada = new(false, null, null);

    public async Task<Result<CierreProgramadoRealizado>> Handle(RunScheduledMonthlyCloseCommand request, CancellationToken ct)
    {
        var activacion = await db.LendingSetups.AsNoTracking().Where(s => s.ActivationDate != null).Select(s => s.ActivationDate).FirstOrDefaultAsync(ct);
        if (activacion is not { } activada) return Result.Success(Nada);

        var hoy = reloj.HoyLocal;
        var lector = parametros ?? new LectorDeParametros(db);
        var programado = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.RiesgoCierreProgramado, hoy, ct: ct);
        if (programado.IsFailure || programado.Value.Valor is not true) return Result.Success(Nada);
        var limite = await DiaLimiteAsync(lector, hoy, ct);
        if (hoy.Day < limite) return Result.Success(Nada);

        var mes = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var fin = MonthlyClose.UltimoDiaDe(mes.Year, mes.Month);
        if (activada > fin) return Result.Success(Nada);
        if (await PendienteAsync(db, mes.Year, mes.Month, ct) is false) return Result.Success(Nada);

        var orden = await new OrderMonthlyCloseCommandHandler(db, requisitos, actorActual, reloj, replicacion)
            .Handle(new OrderMonthlyCloseCommand(mes.Year, mes.Month, "Cierre programado"), ct);
        if (orden.IsSuccess) return Result.Success(new CierreProgramadoRealizado(true, orden.Value.ProcessRunPublicId, null));

        var etiqueta = ErroresDeRiesgo.Mes(mes.Year, mes.Month);
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.CierreProgramadoFallido, $"No se pudo programar el cierre de {etiqueta}",
            $"El cierre programado de Cartera de {etiqueta} no se ordenó: {orden.Error.Message} Resuelva el requisito y ordénelo desde «Cierres».",
            DedupKey: $"{TiposDeAlerta.CierreProgramadoFallido}:{etiqueta}", ModuloDeOrigen: "Lending"), ct);
        return Result.Success(new CierreProgramadoRealizado(false, null, orden.Error.Code));
    }

    /// <summary>El día límite del mes (<c>Riesgo.DiaLimiteCierre</c>, 10 por defecto).</summary>
    public static async Task<int> DiaLimiteAsync(ILectorDeParametros lector, DateOnly hoy, CancellationToken ct)
    {
        var leido = await lector.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.RiesgoDiaLimiteCierre, hoy, ct: ct);
        return leido.IsSuccess && leido.Value.Valor is int d
            ? d
            : int.Parse(ParametrosDeCartera.Definiciones.First(x => x.Clave == ParametrosDeCartera.RiesgoDiaLimiteCierre).DefectoSeguro,
                System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>¿El mes no tiene cierre vigente ni una corrida de cierre en curso o sin terminar?</summary>
    public static async Task<bool> PendienteAsync(IApplicationDbContext db, int anio, int mes, CancellationToken ct)
    {
        var a = (short)anio;
        var m = (byte)mes;
        var cerrado = await db.MonthlyCloses.AsNoTracking().AnyAsync(c => !c.IsDeleted && c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == a
            && c.PeriodMonth == m && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted), ct);
        if (cerrado) return false;
        var conCorrida = await db.LendingProcessRuns.AsNoTracking().AnyAsync(r => !r.IsDeleted && r.Kind == LendingProcessKind.MonthlyClose
            && r.PeriodYear == a && r.PeriodMonth == m
            && (r.Status == LendingProcessRunStatus.Requested || r.Status == LendingProcessRunStatus.Running || r.Status == LendingProcessRunStatus.Failed
                || r.Status == LendingProcessRunStatus.CompletedWithErrors), ct);
        return !conCorrida;
    }
}

/// <summary>
/// La tarea programada <c>cartera.cierre-programado</c> (feature 013, US9, T0707; DT §2.13): una vez al día desde la hora de las tareas, en cada
/// cooperativa, por <see cref="IEjecutorEnCooperativa"/>. Escribe sólo por <see cref="RunScheduledMonthlyCloseCommand"/> y deja su marca diaria
/// (<see cref="RegisterDailyLendingTaskRunCommand"/>), que vigila <c>cartera.vigilancia</c>. Singleton. (nuevo)
/// </summary>
public sealed class TareaDeCierreProgramado : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.cierre-programado";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var enviar = servicios.GetRequiredService<ISender>();
        var r = await enviar.Send(new RunScheduledMonthlyCloseCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"El cierre programado de Cartera falló: {r.Error.Code} {r.Error.Message}");
        await enviar.Send(new RegisterDailyLendingTaskRunCommand(Nombre), ct);
    }
}
