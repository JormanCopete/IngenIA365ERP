using FluentValidation;
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
/// Reprocesa mensajes rechazados (feature 012, T500; FR-079, FR-080; api.md §25.2, <c>POST /api/inventory/messages/reprocess</c>,
/// <c>Inventory.Messages.Reprocess</c>, con motivo e <c>Idempotency-Key</c>, responde 202). Sólo entregas <c>Rejected</c>
/// (si no, <c>Integration.Message.NotRejected</c> con la lista): crea un lote <c>Reprocess</c> y pasa a <c>InBatch</c> con él las
/// elegidas <b>y</b>, transitivamente, sus dependientes que esperaban (<c>Pending</c> o <c>InBatch</c> sin procesar), que se
/// reprocesan juntos en orden (T10). El actor es la persona que ordena (FR-083) y cada mensaje conserva su fecha de operación.
/// Un mensaje de una versión que el destino ya no acepta sigue rechazado (<c>Integration.VersionNotAccepted</c>): nunca se
/// reescribe.
/// </summary>
public sealed record ReprocessMessagesCommand(IReadOnlyList<Guid> MessagePublicIds, string Reason, string Destination = IntegrationDestinations.Accounting)
    : IRequest<Result<LoteOrdenadoDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReprocessMessagesCommandValidator : ValidadorConMotivo<ReprocessMessagesCommand>
{
    public ReprocessMessagesCommandValidator()
    {
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("Elegí al menos un mensaje rechazado.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(20);
    }
}

public sealed class ReprocessMessagesCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IEnumerable<IDestinoDeMensajes> destinos,
    ISenalDeMensajes? senal = null) : IRequestHandler<ReprocessMessagesCommand, Result<LoteOrdenadoDto>>
{
    public async Task<Result<LoteOrdenadoDto>> Handle(ReprocessMessagesCommand request, CancellationToken ct)
    {
        var ids = request.MessagePublicIds.Distinct().ToList();
        var destino = request.Destination;
        var entregas = await db.IntegrationMessageDeliveries
            .Include(d => d.Message)
            .Where(d => d.Destination == destino && ids.Contains(d.Message!.PublicId))
            .ToListAsync(ct);

        var noRechazados = ids.Where(id => !entregas.Any(e => e.Message!.PublicId == id && e.Status == DeliveryStatus.Rejected)).ToList();
        if (noRechazados.Count > 0)
            return Result.Failure<LoteOrdenadoDto>(ErroresDeIntegracion.MensajesNoRechazados(noRechazados));

        // Una versión que el destino ya no acepta sigue rechazada.
        var consumidor = destinos.FirstOrDefault(d => d.Destino == destino);
        var elegidas = entregas.Where(e => consumidor is null || consumidor.Acepta(e.Message!.Type, e.Message.Version)).ToList();

        var actor = await actorActual.ObtenerAsync(ct);
        var resultado = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var arrastre = await LotesDeIntegracion.DependientesAsync(db, elegidas.Select(e => e.MessageId), async candidatos =>
                (await db.IntegrationMessageDeliveries.AsNoTracking()
                    .Where(d => candidatos.Contains(d.MessageId) && d.Destination == destino
                                && (d.Status == DeliveryStatus.Pending || d.Status == DeliveryStatus.InBatch))
                    .Select(d => d.MessageId).ToListAsync(ct)).ToHashSet(), ct);
            var arrastradas = arrastre.Count == 0
                ? new List<IntegrationMessageDelivery>()
                : await db.IntegrationMessageDeliveries.Include(d => d.Message)
                    .Where(d => arrastre.Contains(d.MessageId) && d.Destination == destino).ToListAsync(ct);

            var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), destino, BatchTrigger.Reprocess,
                actor, request.Reason, reloj.UtcNow);
            var todas = elegidas.Concat(arrastradas).ToList();
            lote.MessageCount = todas.Count;
            lote.DocumentCount = todas.Select(e => e.Message!.OriginPublicId).Distinct().Count();
            if (todas.Count > 0)
            {
                lote.DateFrom = todas.Min(e => e.Message!.OperationDate);
                lote.DateTo = todas.Max(e => e.Message!.OperationDate);
            }

            db.IntegrationBatches.Add(lote);
            await db.SaveChangesAsync(ct);

            LotesDeIntegracion.Asignar(lote, todas);
            await db.SaveChangesAsync(ct);
            return Result.Success(new LoteOrdenadoDto(lote.PublicId, lote.Number, lote.Trigger, lote.Status, elegidas.Count, lote.DocumentCount, arrastradas.Count));
        }, ct);

        if (resultado.IsSuccess) senal?.Avisar(ids);
        return resultado;
    }
}
