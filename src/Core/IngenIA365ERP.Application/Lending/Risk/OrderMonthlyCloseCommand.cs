using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>La preparación del cierre de un mes (api.md §12.3). (nuevo)</summary>
public sealed record MonthlyCloseReadinessDto(int Year, int Month, bool Ready, int Version, IReadOnlyList<RequisitoDelCierre> Requirements, string? BlockedCode,
    string? BlockedMessage);

/// <summary>
/// <c>GET /api/lending/risk/monthly-closes/{year}/{month}/readiness</c> (feature 013, US9, T0704; api.md §12.3, <c>Lending.MonthlyClose.Execute</c>):
/// los requisitos del cierre del mes con su detalle (<see cref="RequisitosDelCierreMensual"/>). No escribe. Es de toda la cooperativa: no trae
/// datos de deudores. (nuevo)
/// </summary>
public sealed record GetMonthlyCloseReadinessQuery(int Year, int Month) : IRequest<Result<MonthlyCloseReadinessDto>>;

public sealed class GetMonthlyCloseReadinessQueryValidator : AbstractValidator<GetMonthlyCloseReadinessQuery>
{
    public GetMonthlyCloseReadinessQueryValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class GetMonthlyCloseReadinessQueryHandler(RequisitosDelCierreMensual requisitos)
    : IRequestHandler<GetMonthlyCloseReadinessQuery, Result<MonthlyCloseReadinessDto>>
{
    public async Task<Result<MonthlyCloseReadinessDto>> Handle(GetMonthlyCloseReadinessQuery request, CancellationToken ct)
    {
        var e = await requisitos.EvaluarAsync(request.Year, request.Month, null, ct);
        return Result.Success(new MonthlyCloseReadinessDto(request.Year, request.Month, e.Listo, e.Version, e.Requisitos, e.Bloqueo?.Code, e.Bloqueo?.Message));
    }
}

/// <summary>
/// <c>POST /api/lending/risk/monthly-closes/{year}/{month}/close</c> (feature 013, US9, T0704; api.md §12.3, <c>Lending.MonthlyClose.Execute</c>;
/// T47): <b>sólo encola</b> (202) la corrida <c>MonthlyClose</c> del mes con la persona que ordena como actor (<c>RequestedByKind = Person</c>,
/// origen <c>Orden:{PublicId del usuario}</c>; sin persona, el proceso y el origen de la tarea <c>cartera.cierre-programado</c>). Antes revisa los
/// requisitos de <see cref="RequisitosDelCierreMensual"/>, cada uno con su código, y sincroniza la réplica de valores nacionales con la central:
/// si la central no responde, se niega con motivo (<c>Lending.MonthlyClose.ReplicationUnavailable</c>, B7). Una corrida del mismo mes que no
/// terminó con su cierre (fallida o con errores) se reintenta, no se vuelve a ordenar (<c>Lending.Process.AlreadyRunning</c>). (nuevo)
/// </summary>
public sealed record OrderMonthlyCloseCommand(int Year, int Month, string? Reason = null) : IRequest<Result<ProcessRunRequestedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class OrderMonthlyCloseCommandValidator : AbstractValidator<OrderMonthlyCloseCommand>
{
    public OrderMonthlyCloseCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2999);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class OrderMonthlyCloseCommandHandler(
    IApplicationDbContext db,
    RequisitosDelCierreMensual requisitos,
    IActorActual actorActual,
    IDateTimeService reloj,
    ReplicacionDeValoresNacionales? replicacion = null) : IRequestHandler<OrderMonthlyCloseCommand, Result<ProcessRunRequestedDto>>
{
    /// <summary>El origen de una corrida que ordena la tarea del cierre programado.</summary>
    public const string OrigenProgramado = "Tarea:cartera.cierre-programado";

    public async Task<Result<ProcessRunRequestedDto>> Handle(OrderMonthlyCloseCommand request, CancellationToken ct)
    {
        var evaluacion = await requisitos.EvaluarAsync(request.Year, request.Month, null, ct);
        if (evaluacion.PrimerError is { } error) return Result.Failure<ProcessRunRequestedDto>(error);

        var anio = (short)request.Year;
        var mes = (byte)request.Month;
        var pendiente = await db.LendingProcessRuns.AsNoTracking()
            .Where(r => !r.IsDeleted && r.Kind == LendingProcessKind.MonthlyClose && r.PeriodYear == anio && r.PeriodMonth == mes
                && r.CloseVersion == evaluacion.Version)
            .Select(r => new { r.PublicId, r.Kind }).FirstOrDefaultAsync(ct);
        if (pendiente is not null) return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.EnCurso(pendiente.Kind, pendiente.PublicId));

        if (replicacion is not null)
        {
            var actorDeLaReplica = await actorActual.ObtenerAsync(ct);
            var replica = await replicacion.AplicarAsync(NationalValueReplicationOrigin.MonthlyClose, actorDeLaReplica.Name, ct);
            if (!replica.CentralDisponible) return Result.Failure<ProcessRunRequestedDto>(ErroresDeRiesgo.ReplicaNoDisponible(replica.Motivo));
        }

        var actor = await actorActual.ObtenerAsync(ct);
        var persona = actor.Kind == ActorKind.Person && actor.UserPublicId is not null;
        var corrida = new LendingProcessRun
        {
            Kind = LendingProcessKind.MonthlyClose,
            PeriodYear = anio,
            PeriodMonth = mes,
            CloseVersion = evaluacion.Version,
            Status = LendingProcessRunStatus.Requested,
            RequestedByKind = persona ? ActorKind.Person : ActorKind.Process,
            RequestedByUserId = actor.UserId,
            RequestedByCentralUserId = actor.CentralUserId,
            RequestedByName = RequestLendingProcessRunCommandHandler.Recortar(actor.Name, 150),
            RequestedByIp = RequestLendingProcessRunCommandHandler.Recortar(actor.Ip, 45),
            ExecutionOrigin = persona ? $"Orden:{actor.UserPublicId:D}" : OrigenProgramado,
            Reason = RequestLendingProcessRunCommandHandler.Recortar(request.Reason?.Trim(), 500),
            RequestedAt = reloj.UtcNow,
        };
        db.LendingProcessRuns.Add(corrida);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ProcessRunRequestedDto(corrida.PublicId, corrida.Status.ToString()));
    }
}
