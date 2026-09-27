using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Pasa un lote <c>Requested</c> a <c>Running</c> (feature 012, T503; contracts/contabilidad.md §5.4). <b>Comando de proceso, sin
/// ruta</b>: lo envía sólo <c>DespachadorDeMensajes</c> por <c>ISender</c> dentro de <c>IEjecutorEnCooperativa</c>, con el actor del
/// proceso o de la persona que ordenó el lote. Una sola vez: si otra réplica ya lo arrancó, no hace nada (<c>false</c>); un choque
/// de <c>RowVersion</c> se reintenta entero y la relectura lo ve ya en curso.
/// </summary>
public sealed record StartIntegrationBatchCommand(Guid BatchPublicId) : IRequest<Result<bool>>, IReintentableAnteConcurrencia;

public sealed class StartIntegrationBatchCommandValidator : AbstractValidator<StartIntegrationBatchCommand>
{
    public StartIntegrationBatchCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
    }
}

public sealed class StartIntegrationBatchCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<StartIntegrationBatchCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(StartIntegrationBatchCommand request, CancellationToken ct)
    {
        var lote = await db.IntegrationBatches.FirstOrDefaultAsync(b => b.PublicId == request.BatchPublicId, ct);
        if (lote is null) return Result.Failure<bool>(ErroresDeIntegracion.LoteNoEncontrado(request.BatchPublicId));
        if (lote.Status != BatchStatus.Requested) return Result.Success(false);

        lote.Iniciar(reloj.UtcNow);
        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
