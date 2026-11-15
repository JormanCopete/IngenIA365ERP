using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

/// <summary>
/// Feature 021, US5 (T103; FR-048): el comprobante en carta de un pago del canal, el mismo documento de la caja (<see cref="GetReceiptDocumentQueryHandler"/>).
/// Sólo un recibo que salió de un aviso de esa persona: cualquier otro responde como inexistente. La descarga se audita. (nuevo)
/// </summary>
public sealed record GetChannelReceiptDocumentQuery(Guid PersonPublicId, Guid ReceiptPublicId) : IRequest<Result<ReciboModel>>;

public sealed class GetChannelReceiptDocumentQueryHandler(IApplicationDbContext db, AuditoriaDelCanal auditoria)
    : IRequestHandler<GetChannelReceiptDocumentQuery, Result<ReciboModel>>
{
    public const string NoEncontrado = "Channel.Receipt.NotFound";

    public async Task<Result<ReciboModel>> Handle(GetChannelReceiptDocumentQuery request, CancellationToken ct)
    {
        var recibo = await (from n in db.ChannelPaymentNotices.AsNoTracking()
                            join p in db.People.AsNoTracking() on n.PersonId equals p.Id
                            join r in db.IssuedReceipts.AsNoTracking() on n.ReceiptId equals (long?)r.Id
                            where p.PublicId == request.PersonPublicId && r.PublicId == request.ReceiptPublicId
                            select r).FirstOrDefaultAsync(ct);
        if (recibo is null) return Result.Failure<ReciboModel>(new Error(NoEncontrado, "No encontramos ese comprobante."));
        var modelo = await GetReceiptDocumentQueryHandler.ModeloAsync(db, recibo, FormatoDeRecibo.Letter, false, ct);
        await auditoria.LecturaAsync(AuditoriaDelCanal.EventoReciboDescargado, "Receipt", recibo.PublicId,
            new { channel = "Digital", number = recibo.Number }, ct);
        return Result.Success(modelo);
    }
}
