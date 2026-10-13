using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Processes.Tareas;

/// <summary>Lo que levantó una pasada de la vigilancia. (nuevo)</summary>
public sealed record VigilanciaRealizada(bool CicloDeAyerSinTerminar, int CreditosSinCausar, IReadOnlyList<string> TareasSinCorrer)
{
    /// <summary>US9 (T0707): el mes anterior quedó sin cierre de Cartera pasado <c>Riesgo.DiaLimiteCierre</c>.</summary>
    public string? CierrePendiente { get; init; }
}

/// <summary>
/// La vigilancia de los procesos de Cartera (feature 013, US4, T0502; DT §2.13, data-model §22.5; SC-020): sin ruta, la corre la tarea
/// <c>cartera.vigilancia</c> cada hora. Sale si Cartera no está activada. Pasada la tolerancia (la hora de inicio de las tareas diarias):
/// <list type="bullet">
/// <item><c>Cartera.ProcesoDiarioNoCorrio</c> si el <c>DailyCycle</c> de ayer no está <c>Completed</c> ni <c>CompletedWithErrors</c>
/// (<c>DedupKey …:{fecha}</c>);</item>
/// <item><c>Cartera.CreditoSinCausar</c> por cada crédito con un error vigente en <c>LND_ProcessRunErrors</c> (<c>…:{crédito}</c>);</item>
/// <item><c>Cartera.TareaDiariaNoCorrio</c> por cada tarea diaria registrada de una entrega vigente cuya última marca
/// (<see cref="MarcaDeTareaDiariaDeCartera"/>) es anterior a ayer (<c>…:{tarea}:{fecha}</c>).</item>
/// </list>
/// Idempotente por el <c>DedupKey</c>: repetir suma la ocurrencia. US9 (T0707; pregunta E6) suma <c>Cartera.CierreMensualPendiente</c> si, pasado el
/// día <c>Riesgo.DiaLimiteCierre</c>, el mes anterior (con Cartera ya activada en él) no tiene cierre vigente (<c>…:{aaaa-mm}</c>). (nuevo)
/// </summary>
public sealed record WatchLendingProcessesCommand : IRequest<Result<VigilanciaRealizada>>;

public sealed class WatchLendingProcessesCommandValidator : AbstractValidator<WatchLendingProcessesCommand>;

public sealed class WatchLendingProcessesCommandHandler(
    IApplicationDbContext db,
    IAlertas alertas,
    IDateTimeService reloj,
    IEnumerable<ITareaProgramada> tareas,
    IngenIA365ERP.Application.Common.Parameters.ILectorDeParametros? parametros = null) : IRequestHandler<WatchLendingProcessesCommand, Result<VigilanciaRealizada>>
{
    /// <summary>Hasta esta hora local no se espera que lo de ayer esté terminado.</summary>
    public static readonly TimeOnly Tolerancia = TareasDeCartera.HoraDeInicio;

    private const string Modulo = "Lending";

    public async Task<Result<VigilanciaRealizada>> Handle(WatchLendingProcessesCommand request, CancellationToken ct)
    {
        var activacion = await db.LendingSetups.AsNoTracking().Where(s => s.ActivationDate != null).Select(s => s.ActivationDate).FirstOrDefaultAsync(ct);
        if (activacion is not { } activada) return Result.Success(new VigilanciaRealizada(false, 0, []));

        var hoy = reloj.HoyLocal;
        var ayer = hoy.AddDays(-1);
        var pasadaLaTolerancia = TimeOnly.FromTimeSpan(reloj.AhoraLocal.TimeOfDay) >= Tolerancia;

        // El ciclo de ayer.
        var cicloSinTerminar = false;
        if (pasadaLaTolerancia && activada <= ayer)
        {
            var estado = await db.LendingProcessRuns.AsNoTracking()
                .Where(r => r.Kind == LendingProcessKind.DailyCycle && r.ProcessDate == ayer && !r.IsDeleted)
                .Select(r => (LendingProcessRunStatus?)r.Status).FirstOrDefaultAsync(ct);
            if (estado is not (LendingProcessRunStatus.Completed or LendingProcessRunStatus.CompletedWithErrors))
            {
                cicloSinTerminar = true;
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.ProcesoDiarioNoCorrio, "El ciclo diario de Cartera no terminó",
                    $"El ciclo diario de Cartera del {ayer:yyyy-MM-dd} {(estado is null ? "no se creó" : $"está en «{estado}»")}. Revise las corridas "
                    + "en la bandeja de Cartera; los créditos se ponen al día solos cuando el ciclo corra.",
                    DedupKey: $"{TiposDeAlerta.ProcesoDiarioNoCorrio}:{ayer:yyyy-MM-dd}", ModuloDeOrigen: Modulo), ct);
            }
        }

        // Los créditos con error vigente.
        var conError = await (from e in db.LendingProcessRunErrors.AsNoTracking()
                              join l in db.Loans.AsNoTracking() on e.LoanId equals l.Id
                              where e.ResolvedAt == null
                              select new { l.Id, l.PublicId, l.Number, e.ErrorMessage, e.OccurredAt })
            .ToListAsync(ct);
        foreach (var c in conError.GroupBy(c => c.Id).Select(g => g.OrderByDescending(x => x.OccurredAt).First()))
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.CreditoSinCausar, $"Crédito {c.Number} sin causar",
                $"El crédito {c.Number} quedó con error en el ciclo diario y su causación no avanzó: {c.ErrorMessage}",
                EntityType: "Loan", EntityPublicId: c.PublicId, DedupKey: $"{TiposDeAlerta.CreditoSinCausar}:{c.Id}", ModuloDeOrigen: Modulo), ct);

        // Las tareas diarias sin su marca.
        var sinCorrer = new List<string>();
        if (pasadaLaTolerancia && activada <= ayer)
        {
            var registradas = tareas.Select(t => t.Nombre).ToHashSet(StringComparer.Ordinal);
            var vigilables = MarcaDeTareaDiariaDeCartera.Diarias.Where(t => t.Entrega <= CatalogoDeCartera.EntregaVigente && registradas.Contains(t.Nombre)).ToList();
            var origenes = vigilables.Select(t => MarcaDeTareaDiariaDeCartera.Origen(t.Nombre)).ToList();
            var marcas = await db.LendingProcessRuns.AsNoTracking()
                .Where(r => r.Kind == LendingProcessKind.ScheduledTask && origenes.Contains(r.ExecutionOrigin) && !r.IsDeleted)
                .GroupBy(r => r.ExecutionOrigin).Select(g => new { g.Key, Ultima = g.Max(r => r.ProcessDate) })
                .ToDictionaryAsync(x => x.Key, x => x.Ultima, ct);
            foreach (var t in vigilables)
            {
                var ultima = marcas.GetValueOrDefault(MarcaDeTareaDiariaDeCartera.Origen(t.Nombre));
                if (ultima is { } u && u >= ayer) continue;
                sinCorrer.Add(t.Nombre);
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TareaDiariaNoCorrio, $"La tarea {t.Nombre} no corrió",
                    $"La tarea diaria {t.Nombre} de Cartera no corrió el {ayer:yyyy-MM-dd}"
                    + (ultima is { } antes ? $" (la última vez fue el {antes:yyyy-MM-dd})." : " (no hay registro de que haya corrido).")
                    + " Revise el programador de tareas.",
                    DedupKey: $"{TiposDeAlerta.TareaDiariaNoCorrio}:{t.Nombre}:{ayer:yyyy-MM-dd}", ModuloDeOrigen: Modulo), ct);
            }
        }
        // US9 (T0707): el cierre del mes anterior, pasado el día límite.
        string? cierrePendiente = null;
        var limite = await Risk.RunScheduledMonthlyCloseCommandHandler.DiaLimiteAsync(parametros ?? new IngenIA365ERP.Application.Common.Parameters.LectorDeParametros(db), hoy, ct);
        var anterior = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);
        var finDelAnterior = Domain.Entities.Lending.Risk.MonthlyClose.UltimoDiaDe(anterior.Year, anterior.Month);
        if (hoy.Day > limite && activada <= finDelAnterior
            && !await Risk.RequisitosDelCierreMensual.MesConCierreVigenteAsync(db, anterior.Year, anterior.Month, ct))
        {
            cierrePendiente = Risk.ErroresDeRiesgo.Mes(anterior.Year, anterior.Month);
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.CierreMensualPendiente, $"Falta el cierre de Cartera de {cierrePendiente}",
                $"Pasó el día {limite} y Cartera no ha cerrado {cierrePendiente}. Ordene el cierre desde «Cierres»: Contabilidad no cierra el mes mientras "
                + "Cartera no lo haya cerrado.",
                DedupKey: $"{TiposDeAlerta.CierreMensualPendiente}:{cierrePendiente}", ModuloDeOrigen: Modulo), ct);
        }

        return Result.Success(new VigilanciaRealizada(cicloSinTerminar, conError.Select(c => c.Id).Distinct().Count(), sinCorrer)
        {
            CierrePendiente = cierrePendiente,
        });
    }
}

/// <summary>
/// La tarea programada <c>cartera.vigilancia</c> (feature 013, US4, T0502; DT §2.13): cada hora, en cada cooperativa, por
/// <see cref="IEjecutorEnCooperativa"/>. Escribe sólo por <see cref="WatchLendingProcessesCommand"/>. Singleton. (nuevo)
/// </summary>
public sealed class VigilanciaDeCartera : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.vigilancia";

    public string Nombre => NombreDeLaTarea;

    /// <summary>Una vez por hora.</summary>
    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= TimeSpan.FromHours(1);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new WatchLendingProcessesCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"La vigilancia de Cartera falló: {r.Error.Code} {r.Error.Message}");
    }
}
