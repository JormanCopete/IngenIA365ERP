using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Ordena un lote manual de contabilización (feature 012, T499; FR-077; api.md §26.4, <c>POST /api/accounting/inventory/batches</c>,
/// <c>Accounting.InventoryBatches.Run</c>, con motivo e <c>Idempotency-Key</c>, responde 202). Procesa <b>exactamente</b> lo que
/// mostró la vista previa: resuelve <see cref="CutoffMessagePublicId"/> al <c>Id</c> interno (que sólo guarda
/// <c>COR_IntegrationBatches.CutoffMessageId</c> y nunca sale por HTTP), toma número de <c>COR_IntegrationBatchCounters</c> y
/// asigna el lote a las entregas <c>InBatch</c> sin lote del alcance hasta el corte. El solicitante es la persona
/// (<see cref="IActorActual"/>, con su IP y el motivo): el despachador la ejecuta con ella como actor (FR-083). Si ya hay un
/// lote manual en curso que se cruza con el alcance, <c>Accounting.InventoryBatch.AlreadyRunning</c>. Despierta al
/// despachador por <see cref="ISenalDeMensajes"/> al terminar.
/// </summary>
public sealed record OrderIntegrationBatchCommand(
    Guid CutoffMessagePublicId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string>? DocumentTypeCodes,
    string? ScheduleKey,
    Guid? BranchPublicId,
    string Reason) : IRequest<Result<LoteOrdenadoDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }

    public AlcanceDeLote Alcance => new(From, To, DocumentTypeCodes, ScheduleKey, BranchPublicId);
}

/// <summary>
/// El alcance de un lote manual y de su vista previa (api.md §26.4): rango de fechas de operación, tipos de documento, horario
/// y sucursal. (nuevo)
/// </summary>
public sealed record AlcanceDeLote(DateOnly From, DateOnly To, IReadOnlyList<string>? DocumentTypeCodes, string? ScheduleKey, Guid? BranchPublicId)
{
    /// <summary>
    /// Las entregas a Contabilidad que un lote manual tomaría: <c>InBatch</c> sin lote, con fecha de operación en el rango y los
    /// filtros que vengan, hasta <paramref name="corte"/> si viene. En orden de emisión.
    /// </summary>
    public IQueryable<IntegrationMessageDelivery> Entregas(IApplicationDbContext db, long? corte = null)
    {
        var desde = From;
        var hasta = To;
        var consulta = db.IntegrationMessageDeliveries
            .Where(d => d.Destination == IntegrationDestinations.Accounting && d.Status == DeliveryStatus.InBatch && d.BatchId == null
                        && d.Message!.OperationDate >= desde && d.Message.OperationDate <= hasta);
        if (corte is long c) consulta = consulta.Where(d => d.MessageId <= c);
        if (DocumentTypeCodes is { Count: > 0 } tipos)
        {
            var lista = tipos.ToList();
            consulta = consulta.Where(d => d.Message!.OriginDocumentTypeCode != null && lista.Contains(d.Message.OriginDocumentTypeCode));
        }

        if (!string.IsNullOrWhiteSpace(ScheduleKey))
        {
            var horario = ScheduleKey;
            consulta = consulta.Where(d => d.ScheduleKey == horario);
        }

        if (BranchPublicId is Guid sucursal) consulta = consulta.Where(d => d.Message!.BranchPublicId == sucursal);
        return consulta.OrderBy(d => d.MessageId);
    }
}

public sealed class OrderIntegrationBatchCommandValidator : ValidadorConMotivo<OrderIntegrationBatchCommand>
{
    public OrderIntegrationBatchCommandValidator()
    {
        RuleFor(x => x.CutoffMessagePublicId).NotEqual(Guid.Empty).WithMessage("Indicá el corte de la vista previa (cutoffMessagePublicId).");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleFor(x => x.ScheduleKey).MaximumLength(60);
        RuleForEach(x => x.DocumentTypeCodes).NotEmpty().MaximumLength(10);
    }
}

public sealed class OrderIntegrationBatchCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    ISenalDeMensajes? senal = null) : IRequestHandler<OrderIntegrationBatchCommand, Result<LoteOrdenadoDto>>
{
    public async Task<Result<LoteOrdenadoDto>> Handle(OrderIntegrationBatchCommand request, CancellationToken ct)
    {
        var corte = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.PublicId == request.CutoffMessagePublicId).Select(m => (long?)m.Id).FirstOrDefaultAsync(ct);
        if (corte is null)
            return Result.Failure<LoteOrdenadoDto>(ErroresDeIntegracion.MensajeNoEncontrado(request.CutoffMessagePublicId));

        var enCurso = await db.IntegrationBatches.AsNoTracking()
            .Where(b => b.Destination == IntegrationDestinations.Accounting && b.Trigger == BatchTrigger.Manual
                        && (b.Status == BatchStatus.Requested || b.Status == BatchStatus.Running)
                        && (b.DateFrom == null || b.DateFrom <= request.To) && (b.DateTo == null || b.DateTo >= request.From)
                        && (b.ScheduleKey == null || request.ScheduleKey == null || b.ScheduleKey == request.ScheduleKey))
            .OrderBy(b => b.Id)
            .Select(b => new { b.PublicId, b.Number })
            .FirstOrDefaultAsync(ct);
        if (enCurso is not null)
            return Result.Failure<LoteOrdenadoDto>(AccountingErrors.InventoryBatchAlreadyRunning(enCurso.PublicId, enCurso.Number));

        var actor = await actorActual.ObtenerAsync(ct);
        var resultado = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var entregas = await request.Alcance.Entregas(db, corte).Include(d => d.Message).ToListAsync(ct);
            var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), IntegrationDestinations.Accounting,
                BatchTrigger.Manual, actor, request.Reason, reloj.UtcNow);
            lote.CutoffMessageId = corte;
            lote.DateFrom = request.From;
            lote.DateTo = request.To;
            lote.ScheduleKey = string.IsNullOrWhiteSpace(request.ScheduleKey) ? null : request.ScheduleKey;
            lote.Granularity = ClavesDeLote.Leer(lote.ScheduleKey)?.GranularidadDelLote;
            lote.MessageCount = entregas.Count;
            lote.DocumentCount = entregas.Select(e => e.Message!.OriginPublicId).Distinct().Count();
            db.IntegrationBatches.Add(lote);
            await db.SaveChangesAsync(ct);

            LotesDeIntegracion.Asignar(lote, entregas);
            await db.SaveChangesAsync(ct);
            return Result.Success(new LoteOrdenadoDto(lote.PublicId, lote.Number, lote.Trigger, lote.Status, lote.MessageCount, lote.DocumentCount, 0));
        }, ct);

        if (resultado.IsSuccess) senal?.Avisar([request.CutoffMessagePublicId]);
        return resultado;
    }
}
