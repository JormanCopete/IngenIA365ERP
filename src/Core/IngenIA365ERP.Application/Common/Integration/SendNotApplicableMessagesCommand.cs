using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El envío posterior de lo que no pasó (feature 012, T501; FR-078; api.md §25.2, <c>POST /api/inventory/messages/send-not-applicable</c>,
/// <c>Inventory.Messages.SendNotApplicable</c>, con motivo e <c>Idempotency-Key</c>, responde 202). Toma las entregas a Contabilidad
/// <c>NotApplicable</c> con fecha de operación en el rango (y de los tipos pedidos) hasta el corte de la vista previa, suma
/// <b>toda su clausura</b> de relacionados y derivados de cualquier fecha (anulaciones, notas, ajustes de costo y los originales
/// de éstos) —nada posterior al corte, aunque haya llegado después de la vista previa—, crea un lote <c>SendNotApplicable</c> y las
/// pasa en bloque a <c>InBatch</c> con la persona como actor y el motivo (T9). Si algún tipo del rango sigue en <c>NoPasa</c> a hoy
/// (<see cref="ILectorDeParametros"/>), <c>Integration.SendNotApplicable.ModeStillNotPosted</c>. Un documento en período contable
/// cerrado no es un error de la petición: lo rechaza el consumidor y <c>RegisterDeliveryResultCommand</c> rechaza con él el resto
/// de su clausura en el lote.
/// </summary>
public sealed record SendNotApplicableMessagesCommand(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<Guid>? DocumentTypePublicIds,
    Guid CutoffMessagePublicId,
    string Reason) : IRequest<Result<LoteOrdenadoDto>>, IOperacionIdempotente, IConMotivo
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
    ILectorDeParametros parametros,
    ISenalDeMensajes? senal = null) : IRequestHandler<SendNotApplicableMessagesCommand, Result<LoteOrdenadoDto>>
{
    private const string Destino = IntegrationDestinations.Accounting;

    public async Task<Result<LoteOrdenadoDto>> Handle(SendNotApplicableMessagesCommand request, CancellationToken ct)
    {
        var corte = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.PublicId == request.CutoffMessagePublicId).Select(m => (long?)m.Id).FirstOrDefaultAsync(ct);
        if (corte is not long hastaId)
            return Result.Failure<LoteOrdenadoDto>(ErroresDeIntegracion.MensajeNoEncontrado(request.CutoffMessagePublicId));

        List<string>? codigos = null;
        if (request.DocumentTypePublicIds is { Count: > 0 } tiposPedidos)
        {
            var publicos = tiposPedidos.ToList();
            codigos = await db.InventoryDocumentTypes.AsNoTracking().Where(t => publicos.Contains(t.PublicId)).Select(t => t.Code).ToListAsync(ct);
        }

        var desde = request.From;
        var hasta = request.To;
        var baseDelRango = db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Destination == Destino && d.Status == DeliveryStatus.NotApplicable && d.MessageId <= hastaId
                        && d.Message!.OperationDate >= desde && d.Message.OperationDate <= hasta);
        if (codigos is not null)
            baseDelRango = baseDelRango.Where(d => d.Message!.OriginDocumentTypeCode != null && codigos.Contains(d.Message.OriginDocumentTypeCode));
        var semillas = await baseDelRango.Select(d => new { d.MessageId, d.Message!.OriginDocumentTypeCode }).ToListAsync(ct);

        // El modo del tipo, a hoy: si sigue en «no pasa», enviar lo viejo no tiene sentido.
        var tipos = semillas.Select(s => s.OriginDocumentTypeCode).OfType<string>().Concat(codigos ?? []).Distinct(StringComparer.Ordinal).ToList();
        var sinPaso = await TiposQueSiguenSinPasoAsync(tipos, ct);
        if (sinPaso.Count > 0)
            return Result.Failure<LoteOrdenadoDto>(ErroresDeIntegracion.ModoSigueSinPaso(sinPaso));

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

    private async Task<List<string>> TiposQueSiguenSinPasoAsync(IReadOnlyList<string> codigos, CancellationToken ct)
    {
        if (codigos.Count == 0) return [];
        var hoy = reloj.HoyLocal;
        var tipos = await db.InventoryDocumentTypes.AsNoTracking().Where(t => codigos.Contains(t.Code))
            .Select(t => new { t.Id, t.Code }).ToListAsync(ct);

        var sinPaso = new List<string>();
        foreach (var tipo in tipos.OrderBy(t => t.Code, StringComparer.Ordinal))
        {
            var modo = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, hoy,
                ParameterScopeKind.DocumentType, tipo.Id, ct);
            if (modo.IsSuccess && modo.Value.Texto == "NoPasa") sinPaso.Add(tipo.Code);
        }

        return sinPaso;
    }
}
