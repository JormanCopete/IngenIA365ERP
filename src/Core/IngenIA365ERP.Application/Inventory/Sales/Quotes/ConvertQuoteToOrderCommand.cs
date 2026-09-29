using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales.Quotes;

/// <summary>
/// <c>POST /api/inventory/sales/quotes/{id}/to-order</c> (feature 012, I6, T878; FR-052; contracts/api.md §18.4): convierte una cotización
/// confirmada y vigente en el <b>borrador</b> de un pedido. <see cref="DocumentTypePublicId"/> elige el tipo del pedido (sin él, el primero
/// activo de clase <c>SalesOrder</c>). (nuevo)
/// </summary>
public sealed record ConvertQuoteToOrderCommand(Guid QuotePublicId, Guid? DocumentTypePublicId = null) : IRequest<Result<InventoryDocumentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ConvertQuoteToOrderCommandValidator : AbstractValidator<ConvertQuoteToOrderCommand>
{
    public ConvertQuoteToOrderCommandValidator() => RuleFor(x => x.QuotePublicId).NotEmpty();
}

/// <summary>
/// En orden: la cotización existe en el alcance y el grupo de ventas (404); es de clase <c>SalesQuote</c>
/// (<c>Inventory.Document.TypeNotForRoute</c>) y está confirmada (<c>Inventory.Document.NotConfirmed</c>); no venció a hoy
/// (<c>Inventory.Quote.Expired</c>). Crea el borrador del pedido con la cabecera (cliente, vendedor, bodega, sucursal, canal, centro) y las
/// líneas de la cotización con sus precios y descuentos tal como se cotizaron, y el vínculo <c>FromOrder</c> del documento y de cada línea
/// (no existe <c>FromQuote</c>, decisiones-transversales §2.5). El pedido no reserva hasta confirmarse. Un borrador nuevo no consume número.
/// Una cotización se puede convertir más de una vez (varios pedidos parciales): lo que cada pedido toma lo dicen sus vínculos. (nuevo)
/// </summary>
public sealed class ConvertQuoteToOrderCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    VistaDeDocumentos vista)
    : IRequestHandler<ConvertQuoteToOrderCommand, Result<InventoryDocumentDto>>
{
    public async Task<Result<InventoryDocumentDto>> Handle(ConvertQuoteToOrderCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        var cotizacion = await vista.BuscarAsync(request.QuotePublicId, DocumentClassGroup.Sales, seguir: false, ct);
        if (cotizacion is null) return Falla(InventoryErrors.DocumentNotFound());
        if (cotizacion.Class != DocumentClass.SalesQuote) return Falla(InventoryErrors.TypeNotForRoute(cotizacion.Class, DocumentClassGroup.Sales));
        if (cotizacion.Status != DocumentStatus.Confirmed) return Falla(InventoryErrors.NotConfirmed(cotizacion.Status));
        var hoy = reloj.HoyLocal;
        if (cotizacion.ValidUntil is { } vence && vence < hoy) return Falla(ErroresDelCicloComercial.QuoteExpired(vence));

        InventoryDocumentType? tipo;
        if (request.DocumentTypePublicId is { } t)
        {
            tipo = await db.InventoryDocumentTypes.FirstOrDefaultAsync(x => x.PublicId == t, ct);
            if (tipo is null) return Falla(InventoryErrors.DocumentTypeNotFound());
            if (tipo.Class != DocumentClass.SalesOrder) return Falla(InventoryErrors.TypeNotForRoute(tipo.Class, DocumentClassGroup.Sales));
            if (!tipo.IsActive) return Falla(InventoryErrors.DocumentTypeInactive(tipo.Code));
        }
        else
        {
            tipo = await db.InventoryDocumentTypes.Where(x => x.Class == DocumentClass.SalesOrder && x.IsActive).OrderBy(x => x.Code).FirstOrDefaultAsync(ct);
            if (tipo is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        }

        var pedido = new InventoryDocument
        {
            Class = DocumentClass.SalesOrder,
            DocumentTypeId = tipo.Id,
            DocumentType = tipo,
            OperationDate = hoy,
            CreatedByUserId = usuario,
            WarehouseId = cotizacion.WarehouseId,
            BranchId = cotizacion.BranchId,
            CostCenterId = cotizacion.CostCenterId,
            CounterpartyPersonId = cotizacion.CounterpartyPersonId,
            SalespersonId = cotizacion.SalespersonId,
            SalesChannelId = tipo.SalesChannelId ?? cotizacion.SalesChannelId,
            Currency = cotizacion.Currency,
            ExchangeRate = cotizacion.ExchangeRate,
            ExternalReference = cotizacion.ExternalReference,
            Notes = cotizacion.Notes,
            Subtotal = cotizacion.Subtotal,
            DiscountTotal = cotizacion.DiscountTotal,
            TaxTotal = cotizacion.TaxTotal,
            WithholdingTotal = cotizacion.WithholdingTotal,
            Total = cotizacion.Total,
            AmountDue = cotizacion.AmountDue,
        };
        var vinculo = new DocumentLink { SourceDocumentId = cotizacion.Id, TargetDocument = pedido, Kind = DocumentLinkKind.FromOrder };
        var pares = new List<(InventoryDocumentLine Origen, InventoryDocumentLine Destino)>();
        foreach (var origen in cotizacion.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber))
        {
            var linea = new InventoryDocumentLine
            {
                Document = pedido,
                LineNumber = origen.LineNumber,
                ProductId = origen.ProductId,
                UnitId = origen.UnitId,
                Quantity = origen.Quantity,
                Factor = origen.Factor,
                QuantityBase = origen.QuantityBase,
                RoundingQuantity = origen.RoundingQuantity,
                UnitPrice = origen.UnitPrice,
                ListPrice = origen.ListPrice,
                PriceListId = origen.PriceListId,
                ListPriceIncludesTaxes = origen.ListPriceIncludesTaxes,
                GrossAmount = origen.GrossAmount,
                DiscountAmount = origen.DiscountAmount,
                NetAmount = origen.NetAmount,
                LocationId = origen.LocationId,
                Description = origen.Description,
            };
            pedido.Lines.Add(linea);
            pares.Add((origen, linea));
            vinculo.LineLinks.Add(new DocumentLineLink { DocumentLink = vinculo, SourceLineId = origen.Id, TargetLine = linea, QuantityBase = origen.QuantityBase });
        }
        db.InventoryDocuments.Add(pedido);
        db.DocumentLinks.Add(vinculo);
        await db.SaveChangesAsync(ct);

        // Los descuentos tal como se cotizaron (manuales aprobados y promociones): la fila necesita el Id del documento y de la línea.
        var lineasDeLaCotizacion = pares.Select(p => p.Origen.Id).ToList();
        var descuentos = await db.DocumentLineDiscounts.AsNoTracking().Where(d => lineasDeLaCotizacion.Contains(d.DocumentLineId) && !d.IsDeleted).ToListAsync(ct);
        if (descuentos.Count > 0)
        {
            foreach (var d in descuentos)
            {
                var destino = pares.First(p => p.Origen.Id == d.DocumentLineId).Destino;
                db.DocumentLineDiscounts.Add(new DocumentLineDiscount
                {
                    DocumentLineId = destino.Id, DocumentId = pedido.Id, Sequence = d.Sequence, Source = d.Source, FromDocumentDiscount = d.FromDocumentDiscount,
                    IsPriceOverride = d.IsPriceOverride, Rate = d.Rate, Amount = d.Amount, CapRateApplied = d.CapRateApplied, RequiresApproval = d.RequiresApproval,
                    ApprovalRequestId = d.ApprovalRequestId, ApprovedByUserId = d.ApprovedByUserId, ApprovalMethod = d.ApprovalMethod, Reason = d.Reason,
                    PromotionId = d.PromotionId,
                });
            }
            await db.SaveChangesAsync(ct);
        }

        return Result.Success(await vista.DetalleAsync(pedido, [], ct));
    }

    private static Result<InventoryDocumentDto> Falla(Error error) => Result.Failure<InventoryDocumentDto>(error);
}
