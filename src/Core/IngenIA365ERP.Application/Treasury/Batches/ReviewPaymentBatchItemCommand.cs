using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Batches;

/// <summary>
/// <c>POST /api/treasury/payment-batches/{id}/items/{itemId}/review</c> (<c>Treasury.Batches.Approve</c>; feature 015, US26, T0166; FR-210; SC-026): la revisión expresa
/// de una orden marcada del lote —cuenta destino cambiada hace pocos días, compartida, de un usuario interno o importada sin certificación— por una persona
/// <b>distinta de quien preparó</b> el lote (<c>Treasury.Batch.SelfReview</c>), con su nota y auditada. Sin ella, un lote con una orden de cuenta cambiada no se
/// aprueba (<c>Treasury.Batch.ReviewRequired</c>, en <see cref="GuardasDeAprobacionDelLote"/>). Se revisa en borrador o en aprobación; la revisión no cambia la
/// huella del lote (no cambia lo que se paga). (nuevo)
/// </summary>
public sealed record ReviewPaymentBatchItemCommand(Guid BatchPublicId, Guid ItemPublicId, string? Note) : IRequest<Result<PaymentBatchDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ReviewPaymentBatchItemCommandValidator : AbstractValidator<ReviewPaymentBatchItemCommand>
{
    public ReviewPaymentBatchItemCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEmpty();
        RuleFor(x => x.ItemPublicId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().WithMessage("Escriba qué revisó (la certificación, la llamada al beneficiario…).").MaximumLength(500);
    }
}

public sealed class ReviewPaymentBatchItemCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    PersonasDeLosUsuarios personas,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ReviewPaymentBatchItemCommand, Result<PaymentBatchDto>>
{
    public const string CodigoSinMarca = "Treasury.Batch.ItemNotFlagged";

    public async Task<Result<PaymentBatchDto>> Handle(ReviewPaymentBatchItemCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var visto = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (visto is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [visto.BankAccountId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [visto.Id], ct: ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == visto.Id, ct);
        if (lote.Status is not (PaymentBatchState.Draft or PaymentBatchState.PendingApproval))
            return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteNoEnAprobacion(lote.Status.ToString()));
        var item = await db.PaymentBatchItems.FirstOrDefaultAsync(i => i.PublicId == request.ItemPublicId && i.PaymentBatchId == lote.Id && i.IsLive, ct);
        if (item is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("La orden del lote"));
        if (item.Flags == PaymentBatchItemFlags.None)
            return Result.Failure<PaymentBatchDto>(new Error(CodigoSinMarca, "La orden no tiene marcas: no necesita revisión expresa."));
        if (await personas.MismaPersonaAsync(lote.PreparedByUserId, usuario, ct)) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.RevisionPorQuienPreparo());

        item.ExplicitlyReviewedByUserId = usuario;
        item.ReviewedAt = reloj.UtcNow;
        item.ReviewNote = request.Note!.Trim();
        var orden = await db.PaymentOrders.AsNoTracking().Where(o => o.Id == item.PaymentOrderId).Select(o => o.Number).FirstAsync(ct);
        await auditoria.AgregarAsync("Treasury.PaymentBatch.ItemReviewed", "PaymentBatchItem", item.PublicId, null,
            new { batch = lote.Number, order = orden, flags = item.Flags.ToString(), note = item.ReviewNote, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await LotesDePago.DtoAsync(db, lote, null, ct));
    }
}
