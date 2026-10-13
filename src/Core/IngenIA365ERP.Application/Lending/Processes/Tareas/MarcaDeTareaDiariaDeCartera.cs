using FluentValidation;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Processes.Tareas;

/// <summary>Una tarea diaria de Cartera y la entrega desde la que corre (SC-020, C123). (nuevo)</summary>
public sealed record TareaDiariaDeCartera(string Nombre, EntregaDeCartera Entrega);

/// <summary>
/// La marca de la última ejecución de una tarea diaria de Cartera (feature 013, US4, T0502; SC-020, C123): <b>el único</b> que escribe la fila
/// <c>LND_ProcessRuns</c> <c>Kind = ScheduledTask</c> (<c>ProcessDate</c> = la fecha local, <c>ExecutionOrigin = Tarea:{nombre}</c>,
/// <c>Completed</c>) al terminar bien cada tarea diaria; la última corrida del <c>ProgramadorDeTareas</c> vive sólo en la memoria del proceso y no
/// sirve de prueba. Idempotente por tarea y fecha. No guarda: la escribe <see cref="RegisterDailyLendingTaskRunCommand"/>. (nuevo)
/// </summary>
public sealed class MarcaDeTareaDiariaDeCartera(IApplicationDbContext db, IDateTimeService reloj)
{
    /// <summary>Las tareas diarias de Cartera (DT §2.13): las de K1 y, desde K2, las de la libranza (que la llaman al crearse).</summary>
    public static IReadOnlyList<TareaDiariaDeCartera> Diarias { get; } =
    [
        new(Origination.Tareas.TareaDeReservasVencidas.NombreDeLaTarea, EntregaDeCartera.K1),
        new(TareaDeGirosSinResultado.NombreDeLaTarea, EntregaDeCartera.K1),
        new("cartera.cierre-programado", EntregaDeCartera.K1),
        new(TareaDeGarantiasPorVencer.NombreDeLaTarea, EntregaDeCartera.K1),
        new("cartera.archivos-de-descuento", EntregaDeCartera.K2),
        new("cartera.giros-de-pagaduria", EntregaDeCartera.K2),
    ];

    /// <summary>El origen con que se marca una tarea.</summary>
    public static string Origen(string tarea) => Actor.OrigenDeTarea(tarea);

    /// <summary>Agrega la marca de hoy de <paramref name="tarea"/> si no la tiene; devuelve si la agregó. No guarda.</summary>
    public async Task<bool> MarcarAsync(string tarea, CancellationToken ct)
    {
        if (Diarias.All(t => t.Nombre != tarea))
            throw new ArgumentException($"«{tarea}» no es una tarea diaria de Cartera.", nameof(tarea));
        var hoy = reloj.HoyLocal;
        var origen = Origen(tarea);
        if (await db.LendingProcessRuns.AsNoTracking().AnyAsync(r => r.Kind == LendingProcessKind.ScheduledTask && r.ProcessDate == hoy
                && r.ExecutionOrigin == origen && !r.IsDeleted, ct))
            return false;
        var ahora = reloj.UtcNow;
        db.LendingProcessRuns.Add(new LendingProcessRun
        {
            Kind = LendingProcessKind.ScheduledTask,
            ProcessDate = hoy,
            Status = LendingProcessRunStatus.Completed,
            RequestedByKind = ActorKind.Process,
            ExecutionOrigin = origen,
            RequestedAt = ahora,
            StartedAt = ahora,
            FinishedAt = ahora,
        });
        return true;
    }
}

/// <summary>La marca de una tarea diaria de Cartera, sin ruta: la manda la tarea al terminar bien (T0502). (nuevo)</summary>
public sealed record RegisterDailyLendingTaskRunCommand(string Tarea) : IRequest<Result<bool>>;

public sealed class RegisterDailyLendingTaskRunCommandValidator : AbstractValidator<RegisterDailyLendingTaskRunCommand>
{
    public RegisterDailyLendingTaskRunCommandValidator() => RuleFor(x => x.Tarea).NotEmpty();
}

public sealed class RegisterDailyLendingTaskRunCommandHandler(IApplicationDbContext db, MarcaDeTareaDiariaDeCartera marca)
    : IRequestHandler<RegisterDailyLendingTaskRunCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RegisterDailyLendingTaskRunCommand request, CancellationToken ct)
    {
        var marcada = await marca.MarcarAsync(request.Tarea, ct);
        if (marcada) await db.SaveChangesAsync(ct);
        return Result.Success(marcada);
    }
}
