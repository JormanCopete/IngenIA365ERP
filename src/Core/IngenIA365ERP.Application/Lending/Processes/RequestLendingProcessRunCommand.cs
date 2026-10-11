using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>Los errores de las corridas de Cartera (api.md §16.4; data-model parte 2 §10.1, C111). Sin datos del deudor. (nuevo)</summary>
public static class ErroresDeCorridas
{
    public const string CodigoEnCurso = "Lending.Process.AlreadyRunning";
    public const string CodigoClaseNoOrdenable = "Lending.ProcessRun.KindNotOrderable";
    public const string CodigoNoReintentable = "Lending.ProcessRun.NotRetryable";
    public const string CodigoInexistente = "Lending.ProcessRun.NotFound";
    public const string CodigoYaTerminada = "Lending.ProcessRun.AlreadyCompleted";
    public const string CodigoFechaFutura = "Lending.ProcessRun.FutureDate";

    public static Error EnCurso(LendingProcessKind clase, Guid corrida) => new ErrorConDatos(CodigoEnCurso,
        $"Ya hay una corrida de {Nombre(clase)} en curso. Espere a que termine.", new { processRunPublicId = corrida, kind = clase.ToString() });

    public static Error ClaseNoOrdenable(LendingProcessKind clase) => new ErrorConDatos(CodigoClaseNoOrdenable,
        clase is LendingProcessKind.MonthlyClose or LendingProcessKind.CloseReopen
            ? "El cierre mensual y su reapertura se ordenan desde el cierre de Cartera, no desde las corridas."
            : $"Las corridas de {Nombre(clase)} no se ordenan a mano: las encola el sistema.",
        new { kind = clase.ToString() });

    public static Error NoReintentable(LendingProcessRunStatus estado) => new ErrorConDatos(CodigoNoReintentable,
        "Sólo se reintenta una corrida que falló o que terminó con créditos sin causar.", new { status = estado.ToString() });

    public static Error Inexistente() => new(CodigoInexistente, "La corrida no existe.");

    /// <summary>
    /// US4 (bloque 8C): un ciclo de una fecha que todavía no llega causaría intereses de días no vividos; se ordena desde ese día (el
    /// procesador, por su lado, nunca crea uno después de hoy).
    /// </summary>
    public static Error FechaFutura(DateOnly fecha, DateOnly hoy) => new ErrorConDatos(CodigoFechaFutura,
        $"No se causa un día que no ha llegado: el ciclo del {fecha:yyyy-MM-dd} se puede ordenar desde ese día (hoy es {hoy:yyyy-MM-dd}).",
        new { date = fecha, today = hoy });

    public static Error YaTerminada(DateOnly fecha, Guid corrida, LendingProcessRunStatus estado) => new ErrorConDatos(CodigoYaTerminada,
        $"La corrida del ciclo del {fecha:yyyy-MM-dd} ya terminó. Si quedó con créditos sin causar, reinténtela.",
        new { processRunPublicId = corrida, status = estado.ToString() });

    public static string Nombre(LendingProcessKind clase) => clase switch
    {
        LendingProcessKind.DailyCycle => "ciclo diario",
        LendingProcessKind.MonthlyClose => "cierre mensual",
        LendingProcessKind.CloseReopen => "reapertura de cierre",
        LendingProcessKind.NationalValueCorrection => "corrección por valor nacional",
        _ => "proceso",
    };
}

/// <summary>Lo que responde la orden de una corrida (202). (nuevo)</summary>
public sealed record ProcessRunRequestedDto(Guid ProcessRunPublicId, string Status);

/// <summary>
/// <c>POST /api/lending/process-runs</c> (feature 013, US4, T0499; api.md §16.4, <c>Lending.Processes.Run</c>): <b>sólo encola</b> (202) una corrida
/// <c>DailyCycle</c> de <see cref="Date"/> (hoy si no viene) con la persona como quien la ordenó (T8): el procesador la corre y sus mensajes van a
/// nombre de ella. Otra corrida de la misma clase y fecha <c>Requested</c> o <c>Running</c> → <c>Lending.Process.AlreadyRunning</c>; una ya terminada
/// → <c>Lending.ProcessRun.AlreadyCompleted</c> (el único del ciclo por día no admite otra: se reintenta); el cierre, su reapertura y la corrección
/// por valor nacional no se ordenan aquí (<c>Lending.ProcessRun.KindNotOrderable</c>). (nuevo)
/// </summary>
public sealed record RequestLendingProcessRunCommand(LendingProcessKind Kind, DateOnly? Date = null) : IRequest<Result<ProcessRunRequestedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RequestLendingProcessRunCommandValidator : AbstractValidator<RequestLendingProcessRunCommand>
{
    public RequestLendingProcessRunCommandValidator() => RuleFor(x => x.Kind).IsInEnum();
}

public sealed class RequestLendingProcessRunCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<RequestLendingProcessRunCommand, Result<ProcessRunRequestedDto>>
{
    public async Task<Result<ProcessRunRequestedDto>> Handle(RequestLendingProcessRunCommand request, CancellationToken ct)
    {
        if (request.Kind != LendingProcessKind.DailyCycle) return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.ClaseNoOrdenable(request.Kind));
        var fecha = request.Date ?? reloj.HoyLocal;
        if (fecha > reloj.HoyLocal) return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.FechaFutura(fecha, reloj.HoyLocal));
        var existente = await db.LendingProcessRuns.AsNoTracking()
            .Where(r => r.Kind == request.Kind && r.ProcessDate == fecha && !r.IsDeleted)
            .Select(r => new { r.PublicId, r.Status }).FirstOrDefaultAsync(ct);
        if (existente is not null)
            return existente.Status is LendingProcessRunStatus.Requested or LendingProcessRunStatus.Running
                ? Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.EnCurso(request.Kind, existente.PublicId))
                : Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.YaTerminada(fecha, existente.PublicId, existente.Status));

        var actor = await actorActual.ObtenerAsync(ct);
        var corrida = new LendingProcessRun
        {
            Kind = request.Kind,
            ProcessDate = fecha,
            Status = LendingProcessRunStatus.Requested,
            RequestedByKind = actor.Kind == ActorKind.Person ? ActorKind.Person : ActorKind.Process,
            RequestedByUserId = actor.UserId,
            RequestedByCentralUserId = actor.CentralUserId,
            RequestedByName = Recortar(actor.Name, 150),
            RequestedByIp = Recortar(actor.Ip, 45),
            ExecutionOrigin = actor.UserPublicId is { } usuario ? $"Orden:{usuario:D}" : Accrual.CicloDiarioDeCartera.OrigenDelDia(fecha),
            RequestedAt = reloj.UtcNow,
        };
        db.LendingProcessRuns.Add(corrida);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ProcessRunRequestedDto(corrida.PublicId, corrida.Status.ToString()));
    }

    internal static string? Recortar(string? texto, int largo) => texto is null ? null : texto.Length > largo ? texto[..largo] : texto;
}
