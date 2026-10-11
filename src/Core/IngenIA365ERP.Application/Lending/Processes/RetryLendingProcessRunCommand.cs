using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>
/// <c>POST /api/lending/process-runs/{id}/retry</c> (feature 013, US4, T0499; api.md §16.4, <c>Lending.Processes.Run</c>): con motivo, vuelve a
/// encolar una corrida <c>Failed</c> o <c>CompletedWithErrors</c> (202). Retoma desde la marca de agua de cada crédito: el cursor vuelve al principio,
/// R se toma de nuevo y la cuenta de fallidos empieza de cero; los errores anteriores quedan, y los resuelve el intento que tenga éxito. Otro estado
/// → <c>Lending.ProcessRun.NotRetryable</c>. (nuevo)
/// </summary>
public sealed record RetryLendingProcessRunCommand(Guid ProcessRunPublicId, string Reason) : IRequest<Result<ProcessRunRequestedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class RetryLendingProcessRunCommandValidator : AbstractValidator<RetryLendingProcessRunCommand>
{
    public RetryLendingProcessRunCommandValidator()
    {
        RuleFor(x => x.ProcessRunPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se reintenta la corrida.").MaximumLength(500);
    }
}

public sealed class RetryLendingProcessRunCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<RetryLendingProcessRunCommand, Result<ProcessRunRequestedDto>>
{
    public async Task<Result<ProcessRunRequestedDto>> Handle(RetryLendingProcessRunCommand request, CancellationToken ct)
    {
        var corrida = await db.LendingProcessRuns.FirstOrDefaultAsync(r => r.PublicId == request.ProcessRunPublicId && !r.IsDeleted, ct);
        if (corrida is null) return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.Inexistente());
        if (corrida.Status is not (LendingProcessRunStatus.Failed or LendingProcessRunStatus.CompletedWithErrors))
            return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.NoReintentable(corrida.Status));
        if (await db.LendingProcessRuns.AsNoTracking().AnyAsync(r => r.Status == LendingProcessRunStatus.Running && r.Kind == corrida.Kind && r.Id != corrida.Id, ct))
            return Result.Failure<ProcessRunRequestedDto>(ErroresDeCorridas.EnCurso(corrida.Kind, corrida.PublicId));

        var actor = await actorActual.ObtenerAsync(ct);
        corrida.Status = LendingProcessRunStatus.Requested;
        corrida.RetryCount += 1;
        corrida.Reason = request.Reason.Trim();
        corrida.CursorLoanId = null;
        corrida.InformationCutoffAt = null;
        corrida.FailedLoans = 0;
        corrida.FinishedAt = null;
        corrida.CurrentStep = null;
        corrida.RequestedByKind = actor.Kind == ActorKind.Person ? ActorKind.Person : ActorKind.Process;
        corrida.RequestedByUserId = actor.UserId;
        corrida.RequestedByCentralUserId = actor.CentralUserId;
        corrida.RequestedByName = RequestLendingProcessRunCommandHandler.Recortar(actor.Name, 150);
        corrida.RequestedByIp = RequestLendingProcessRunCommandHandler.Recortar(actor.Ip, 45);
        corrida.RequestedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success(new ProcessRunRequestedDto(corrida.PublicId, corrida.Status.ToString()));
    }
}
