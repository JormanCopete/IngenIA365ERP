using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Processes;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>
/// Una tanda del ciclo diario de la corrida <see cref="ProcessRunPublicId"/> (feature 013, US4, T0497; DT §2.13). <b>Sin ruta</b>: la corre el
/// procesador por <c>IEjecutorEnCooperativa</c> (con <see cref="EjecutorDelCicloDiario"/>, que llama a <see cref="CicloDiarioDeCartera"/> sin pasar
/// por el pipeline para que una tanda fallida pueda reintentarse crédito por crédito en transacciones propias) y la mandan a mano las pruebas
/// e2e. Devuelve si la corrida quedó pendiente o terminó. (nuevo)
/// </summary>
public sealed record RunDailyAccrualCommand(Guid ProcessRunPublicId) : IRequest<Result<AvanceDeCorrida>>;

public sealed class RunDailyAccrualCommandValidator : AbstractValidator<RunDailyAccrualCommand>
{
    public RunDailyAccrualCommandValidator() => RuleFor(x => x.ProcessRunPublicId).NotEmpty();
}

public sealed class RunDailyAccrualCommandHandler(IApplicationDbContext db, CicloDiarioDeCartera ciclo, IDateTimeService reloj)
    : IRequestHandler<RunDailyAccrualCommand, Result<AvanceDeCorrida>>
{
    public async Task<Result<AvanceDeCorrida>> Handle(RunDailyAccrualCommand request, CancellationToken ct)
    {
        var corrida = db.LendingProcessRuns.Local.FirstOrDefault(r => r.PublicId == request.ProcessRunPublicId)
                      ?? await db.LendingProcessRuns.FirstOrDefaultAsync(r => r.PublicId == request.ProcessRunPublicId && !r.IsDeleted, ct);
        if (corrida is null || corrida.Kind != LendingProcessKind.DailyCycle) return Result.Failure<AvanceDeCorrida>(ErroresDeCorridas.Inexistente());
        if (corrida.Status is LendingProcessRunStatus.Requested)
        {
            corrida.Status = LendingProcessRunStatus.Running;
            corrida.StartedAt ??= reloj.UtcNow;
        }
        var avance = await ciclo.TandaAsync(corrida, ct);
        corrida.BatchCount += 1;
        corrida.LastHeartbeatAt = reloj.UtcNow;
        if (avance == AvanceDeCorrida.Terminada)
        {
            corrida.Status = corrida.FailedLoans > 0 ? LendingProcessRunStatus.CompletedWithErrors : LendingProcessRunStatus.Completed;
            corrida.FinishedAt = reloj.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(avance);
    }
}

/// <summary>El ejecutor de las corridas <c>DailyCycle</c> que conduce <see cref="ConductorDeCorridasDeCartera"/> (T0225, C59). (nuevo)</summary>
public sealed class EjecutorDelCicloDiario(CicloDiarioDeCartera ciclo) : IEjecutorDeCorridaDeCartera
{
    public LendingProcessKind Clase => LendingProcessKind.DailyCycle;

    public Task<AvanceDeCorrida> EjecutarTandaAsync(Domain.Entities.Lending.Integration.LendingProcessRun corrida, TimeSpan presupuesto, CancellationToken ct) =>
        ciclo.TandaAsync(corrida, ct);
}
