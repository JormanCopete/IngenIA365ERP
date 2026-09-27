using FluentValidation;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

public sealed record ComprobanteDelReciboDto(string TypeCode, long? Number);

public sealed record ActorDelReciboDto(ActorKind Kind, string Name);

/// <summary><c>InventoryPostingDto</c> de api.md §26.5: el vínculo documento ↔ comprobante, también de los resumidos y de una anulación.</summary>
public sealed record InventoryPostingDto(
    Guid MessagePublicId,
    string Type,
    int Version,
    Guid SourceDocumentPublicId,
    Guid? RelatedDocumentPublicId,
    DateOnly OperationDate,
    Guid? AccountingDocumentPublicId,
    ComprobanteDelReciboDto? Voucher,
    MotivoSinComprobante? WithoutVoucher,
    Guid? BatchPublicId,
    string OriginUserName,
    ActorDelReciboDto Actor,
    DateTime PostedAt);

/// <summary>
/// Los recibos de contabilización (feature 012, T518; api.md §26.5; <c>GET /api/accounting/inventory/postings</c> con
/// <c>Accounting.Vouchers.View</c>): de un documento (como origen o como relacionado), de un mensaje, de un lote o de un rango de
/// fechas de operación, en orden de proceso. Nunca expone el <c>Id</c> interno. (nuevo)
/// </summary>
public sealed record ListInventoryPostingsQuery(
    Guid? Document = null,
    Guid? Message = null,
    Guid? Batch = null,
    DateOnly? From = null,
    DateOnly? To = null,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<InventoryPostingDto>>>;

public sealed class ListInventoryPostingsQueryValidator : AbstractValidator<ListInventoryPostingsQuery>
{
    public ListInventoryPostingsQueryValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListInventoryPostingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListInventoryPostingsQuery, Result<PagedResult<InventoryPostingDto>>>
{
    public async Task<Result<PagedResult<InventoryPostingDto>>> Handle(ListInventoryPostingsQuery request, CancellationToken ct)
    {
        var q = db.InventoryPostings.AsNoTracking().Where(p => !p.IsDeleted);
        if (request.Document is { } documento) q = q.Where(p => p.SourcePublicId == documento || p.RelatedDocumentPublicId == documento);
        if (request.Message is { } mensaje) q = q.Where(p => p.MessagePublicId == mensaje);
        if (request.Batch is { } lote) q = q.Where(p => p.BatchPublicId == lote);
        if (request.From is { } desde) q = q.Where(p => p.OperationDate >= desde);
        if (request.To is { } hasta) q = q.Where(p => p.OperationDate <= hasta);

        var pagina = request.Pagina ?? new PageRequest();
        var total = await q.CountAsync(ct);
        var filas = await q.OrderBy(p => p.ProcessedAt).ThenBy(p => p.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(p => new
            {
                Recibo = p,
                Comprobante = p.AccountingDocument == null ? null : new { p.AccountingDocument.PublicId, Tipo = p.AccountingDocument.VoucherType!.Code, p.AccountingDocument.Number },
            })
            .ToListAsync(ct);
        var items = filas.Select(f => new InventoryPostingDto(
                f.Recibo.MessagePublicId, f.Recibo.MessageType, f.Recibo.MessageVersion, f.Recibo.SourcePublicId, f.Recibo.RelatedDocumentPublicId,
                f.Recibo.OperationDate, f.Comprobante?.PublicId,
                f.Comprobante is null ? null : new ComprobanteDelReciboDto(f.Comprobante.Tipo, f.Comprobante.Number),
                f.Recibo.NoVoucherReason switch
                {
                    InventoryPosting.SinComprobanteInformativo => MotivoSinComprobante.Informational,
                    InventoryPosting.SinComprobanteValorCero => MotivoSinComprobante.ZeroValue,
                    _ => null,
                },
                f.Recibo.BatchPublicId, f.Recibo.OriginUserName, new ActorDelReciboDto(f.Recibo.ActorKind, f.Recibo.ActorName), f.Recibo.ProcessedAt))
            .ToList();
        return Result.Success(new PagedResult<InventoryPostingDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}
