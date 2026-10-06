using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El envío posterior de lo que no pasó (feature 012, T501; FR-078; api.md §25.2, <c>POST /api/inventory/messages/send-not-applicable</c>,
/// <c>Inventory.Messages.SendNotApplicable</c>, con motivo e <c>Idempotency-Key</c>, responde 202). Toma las entregas a Contabilidad
/// <c>NotApplicable</c> con fecha de operación en el rango (y de los tipos pedidos) hasta el corte de la vista previa, suma
/// <b>toda su clausura</b> de relacionados y derivados de cualquier fecha (anulaciones, notas, ajustes de costo y los originales
/// de éstos) —nada posterior al corte, aunque haya llegado después de la vista previa—, crea un lote <c>SendNotApplicable</c> y las
/// pasa en bloque a <c>InBatch</c> con la persona como actor y el motivo (T9). Si algún tipo del rango sigue en <c>NoPasa</c> a hoy,
/// <c>Integration.SendNotApplicable.ModeStillNotPosted</c>. Qué es «un tipo» y su modo vigente los dice la vista del módulo
/// (<see cref="IVistaDeBandejaDelModulo"/>, feature 013, T0075: en Inventario, el tipo de documento y <c>Contabilidad.ModoDePaso</c>);
/// <see cref="Modulo"/> la elige y sólo toma mensajes de ese módulo. Un documento en período contable
/// cerrado no es un error de la petición: lo rechaza el consumidor y <c>RegisterDeliveryResultCommand</c> rechaza con él el resto
/// de su clausura en el lote.
/// </summary>
public sealed record SendNotApplicableMessagesCommand(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<Guid>? DocumentTypePublicIds,
    Guid CutoffMessagePublicId,
    string Reason,
    string Modulo = ModulosDeOrigen.Inventario) : IRequest<Result<LoteOrdenadoDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SendNotApplicableMessagesCommandValidator : ValidadorConMotivo<SendNotApplicableMessagesCommand>
{
    public SendNotApplicableMessagesCommandValidator()
    {
        RuleFor(x => x.CutoffMessagePublicId).NotEqual(Guid.Empty).WithMessage("Indicá el corte de la vista previa (cutoffMessagePublicId).");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
        RuleForEach(x => x.DocumentTypePublicIds).NotEqual(Guid.Empty);
    }
}

public sealed class SendNotApplicableMessagesCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    IEnumerable<IVistaDeBandejaDelModulo> vistas,
    ISenalDeMensajes? senal = null) : IRequestHandler<SendNotApplicableMessagesCommand, Result<LoteOrdenadoDto>>
{
    private const string Destino = IntegrationDestinations.Accounting;

    public async Task<Result<LoteOrdenadoDto>> Handle(SendNotApplicableMessagesCommand request, CancellationToken ct)
    {
        var corte = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.PublicId == request.CutoffMessagePublicId).Select(m => (long?)m.Id).FirstOrDefaultAsync(ct);
        if (corte is not long hastaId)
            return Result.Failure<LoteOrdenadoDto>(ErroresDeIntegracion.MensajeNoEncontrado(request.CutoffMessagePublicId));

        var vista = VistasDeBandeja.Del(vistas, request.Modulo);
        IReadOnlyList<string>? codigos = null;
        if (request.DocumentTypePublicIds is { Count: > 0 } tiposPedidos)
            codigos = await vista.CodigosDeTipoAsync(tiposPedidos.ToList(), ct);

        var desde = request.From;
        var hasta = request.To;
        var modulo = vista.Modulo;
        var baseDelRango = db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Destination == Destino && d.Status == DeliveryStatus.NotApplicable && d.MessageId <= hastaId
                        && d.Message!.OriginModule == modulo
                        && d.Message.OperationDate >= desde && d.Message.OperationDate <= hasta);
        if (codigos is not null)
            baseDelRango = vista.DeLosTipos(baseDelRango, codigos);
        var semillas = await baseDelRango.Select(d => new { d.MessageId, d.Message!.OriginDocumentTypeCode, d.Message.Type }).ToListAsync(ct);

        // El modo del tipo, a hoy: si sigue en «no pasa», enviar lo viejo no tiene sentido.
        var tipos = semillas.Select(s => vista.CodigoDeTipo(s.OriginDocumentTypeCode, s.Type)).OfType<string>()
            .Concat(codigos ?? []).Distinct(StringComparer.Ordinal).ToList();
        var sinPaso = await vista.TiposQueSiguenSinPasoAsync(tipos, reloj.HoyLocal, ct);
        if (sinPaso.Count > 0)
            return Result.Failure<LoteOrdenadoDto>(ErroresDeIntegracion.ModoSigueSinPaso(sinPaso.ToList()));

        var clausura = await LotesDeIntegracion.ClausuraAsync(db, semillas.Select(s => s.MessageId), async candidatos =>
            (await db.IntegrationMessageDeliveries.AsNoTracking()
                .Where(d => candidatos.Contains(d.MessageId) && d.Destination == Destino && d.Status == DeliveryStatus.NotApplicable && d.MessageId <= hastaId)
                .Select(d => d.MessageId).ToListAsync(ct)).ToHashSet(), ct);

        var actor = await actorActual.ObtenerAsync(ct);
        var resultado = await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ids = clausura.ToList();
            var entregas = await db.IntegrationMessageDeliveries.Include(d => d.Message)
                .Where(d => ids.Contains(d.MessageId) && d.Destination == Destino && d.Status == DeliveryStatus.NotApplicable)
                .OrderBy(d => d.MessageId)
                .ToListAsync(ct);

            var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), Destino, BatchTrigger.SendNotApplicable,
                actor, request.Reason, reloj.UtcNow);
            lote.CutoffMessageId = hastaId;
            lote.DateFrom = request.From;
            lote.DateTo = request.To;
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
