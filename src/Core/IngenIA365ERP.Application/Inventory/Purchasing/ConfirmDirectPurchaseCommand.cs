using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// La compra directa en un paso (feature 012, T344; FR-019; contracts/api.md §14.3, <c>POST /purchases/direct</c>): la recepción
/// y la factura del proveedor con las mismas líneas y precios, la misma cadena y el mismo modo sellado, en <b>una</b>
/// transacción. (nuevo)
/// </summary>
public sealed record ConfirmDirectPurchaseCommand(SaveInventoryDraftRequest Receipt, DirectPurchaseInvoiceRequest Invoice)
    : IRequest<Result<DirectPurchaseResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmDirectPurchaseCommandValidator : AbstractValidator<ConfirmDirectPurchaseCommand>
{
    public ConfirmDirectPurchaseCommandValidator()
    {
        RuleFor(x => x.Receipt).NotNull();
        RuleFor(x => x.Receipt.DocumentTypePublicId).NotEmpty().WithMessage("Indicá el tipo de la recepción.");
        RuleFor(x => x.Receipt.WarehousePublicId).NotEmpty().WithMessage("Indicá la bodega que recibe.");
        RuleFor(x => x.Receipt.Contraparte).NotEmpty().WithMessage("Indicá el proveedor.");
        RuleFor(x => x.Receipt.Lines).NotEmpty().WithMessage("La compra lleva al menos una línea.");
        RuleForEach(x => x.Receipt.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.ProductPublicId).NotEmpty();
            l.RuleFor(x => x.UnitPublicId).NotEmpty();
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.UnitPrice).NotNull().GreaterThanOrEqualTo(0).WithMessage("Cada línea de la compra lleva su precio.");
        });
        RuleFor(x => x.Invoice).NotNull();
        RuleFor(x => x.Invoice.DocumentTypePublicId).NotEmpty().WithMessage("Indicá el tipo de la factura del proveedor.");
        RuleFor(x => x.Invoice.Supplier).NotNull();
        RuleFor(x => x.Invoice.Supplier.Number).NotEmpty().MaximumLength(SupplierInvoiceDetail.LargoDelNumero).WithMessage("Indicá el número de la factura.");
        RuleFor(x => x.Invoice.Supplier.Prefix).MaximumLength(SupplierInvoiceDetail.LargoDelPrefijo);
    }
}

/// <summary>
/// En orden, dentro de una <see cref="TransaccionExplicita"/>: la factura del proveedor no está ya registrada (antes de
/// guardar nada); el borrador de la recepción (el ciclo común con las reglas de compras); su confirmación por el flujo canónico
/// —la política del tipo de la recepción y el monto máximo de <c>Purchases.Confirm</c> se miden con el total de la compra, que
/// es el de la factura—; el borrador de la factura con las mismas líneas enlazadas a la recepción; y, si la recepción quedó
/// confirmada, la confirmación de la factura (copia el modo de la recepción). Si la recepción quedó en aprobación, la factura
/// queda en borrador enlazada y <see cref="CompraDirectaEncadenada"/> la confirma con la última aprobación, en la transacción
/// del aprobador. Un error de la factura no deja nada guardado.
/// </summary>
public sealed class ConfirmDirectPurchaseCommandHandler(
    IApplicationDbContext db,
    SaveInventoryDraftCommandHandler guardar,
    ConfirmacionDeDocumento confirmacion,
    ContextoDeCompraDirecta contexto,
    VistaDeDocumentos vista)
    : IRequestHandler<ConfirmDirectPurchaseCommand, Result<DirectPurchaseResultDto>>
{
    public Task<Result<DirectPurchaseResultDto>> Handle(ConfirmDirectPurchaseCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => ComprarAsync(request, ct), ct);

    private async Task<Result<DirectPurchaseResultDto>> ComprarAsync(ConfirmDirectPurchaseCommand request, CancellationToken ct)
    {
        // (0) Los tipos y la factura del proveedor, antes de guardar nada.
        var tipoRecepcion = await db.InventoryDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == request.Receipt.DocumentTypePublicId, ct);
        if (tipoRecepcion is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        if (tipoRecepcion.Class != DocumentClass.PurchaseReceipt) return Falla(InventoryErrors.TypeNotForRoute(tipoRecepcion.Class, DocumentClassGroup.Purchases));
        var tipoFactura = await db.InventoryDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == request.Invoice.DocumentTypePublicId, ct);
        if (tipoFactura is null) return Falla(InventoryErrors.DocumentTypeNotFound());
        if (tipoFactura.Class != DocumentClass.SupplierInvoice) return Falla(InventoryErrors.TypeNotForRoute(tipoFactura.Class, DocumentClassGroup.Purchases));

        var proveedor = await db.People.AsNoTracking().Where(p => p.PublicId == request.Receipt.Contraparte).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (proveedor is null) return Falla(ErroresDelDocumento.PersonaInexistente());
        var previo = await ColisionDeFacturaDeProveedor.BuscarAsync(db, new SupplierInvoiceDetail
        {
            DocumentClass = DocumentClass.SupplierInvoice,
            SupplierPersonId = proveedor.Value,
            SupplierPrefix = SupplierInvoiceDetail.NormalizarNumero(request.Invoice.Supplier.Prefix),
            SupplierNumber = SupplierInvoiceDetail.NormalizarNumero(request.Invoice.Supplier.Number),
            Cufe = SupplierInvoiceDetail.NormalizarCufe(request.Invoice.Supplier.Cufe),
        }, ct);
        if (previo is not null) return Falla(previo);

        // (1) La recepción: su borrador y su confirmación (o su solicitud de aprobación).
        var recepcion = await guardar.Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, request.Receipt), ct);
        if (recepcion.IsFailure) return Falla(recepcion.Error);
        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(recepcion.Value.PublicId, DocumentClassGroup.Purchases), ct);
        if (confirmada.IsFailure) return Falla(confirmada.Error);

        // (2) La factura: las mismas líneas, enlazadas a las de la recepción, con el documento del proveedor.
        var lineas = recepcion.Value.Lines.OrderBy(l => l.LineNumber).Select(l =>
        {
            var pedida = request.Receipt.Lines[l.LineNumber - 1];
            return new SaveInventoryDraftLine(null, l.Product.PublicId, l.Unit.PublicId, l.Quantity, pedida.UnitPrice, pedida.DiscountPercent,
                pedida.DiscountAmount, ReceiptLinePublicId: l.LinePublicId);
        }).ToList();
        var borradorFactura = new SaveInventoryDraftRequest(tipoFactura.PublicId, request.Receipt.OperationDate, null, null,
            request.Receipt.CostCenterPublicId, request.Receipt.Contraparte, null, null, null, request.Receipt.Notes, null, null, null, lineas,
            OperationMunicipalityDaneCode: request.Receipt.OperationMunicipalityDaneCode,
            Supplier: request.Invoice.Supplier);

        contexto.PermiteRecepcionPendiente = true;
        Result<InventoryDocumentDto> factura;
        try
        {
            factura = await guardar.Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Purchases, borradorFactura), ct);
        }
        finally
        {
            contexto.PermiteRecepcionPendiente = false;
        }
        if (factura.IsFailure) return Falla(factura.Error);

        // (3) Con la recepción confirmada, la factura se confirma en la misma transacción.
        ConfirmationResultDto? facturaConfirmada = null;
        if (confirmada.Value.Status == DocumentStatus.Confirmed)
        {
            var r = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(factura.Value.PublicId, DocumentClassGroup.Purchases), ct);
            if (r.IsFailure) return Falla(r.Error);
            facturaConfirmada = r.Value;
        }

        var documentoFactura = await db.InventoryDocuments.AsNoTracking().FirstAsync(d => d.PublicId == factura.Value.PublicId, ct);
        var detalle = await db.SupplierInvoiceDetails.AsNoTracking().FirstAsync(d => d.DocumentId == documentoFactura.Id, ct);
        var eventos = await ConsultasDeCompras.EventosAsync(db, documentoFactura.Id, ct);
        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == documentoFactura.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var numeros = await db.InventoryDocumentLines.AsNoTracking().Where(l => l.DocumentId == documentoFactura.Id).ToDictionaryAsync(l => l.Id, l => l.LineNumber, ct);
        var costos = await vista.TieneAsync(PermisosDeGrupo.LeerCostos, ct);

        var mensajes = (confirmada.Value.Messages ?? []).Concat(facturaConfirmada?.Messages ?? []).ToList();
        return Result.Success(new DirectPurchaseResultDto(
            confirmada.Value.Status,
            new DocumentoCreadoDto(confirmada.Value.PublicId, confirmada.Value.DisplayNumber, confirmada.Value.Status),
            new FacturaCreadaDto(documentoFactura.PublicId, VistaDeDocumentos.NumeroVisible(documentoFactura.Prefix, documentoFactura.Number),
                documentoFactura.Status, detalle.NumeroVisible),
            confirmada.Value.Approval,
            new DocumentTotalsDto(documentoFactura.Subtotal, documentoFactura.DiscountTotal, documentoFactura.TaxTotal, documentoFactura.WithholdingTotal,
                documentoFactura.Total, documentoFactura.AmountDue, costos ? documentoFactura.CostTotal : null),
            impuestos.Count > 0
                ? impuestos.Select(t => new DocumentTaxLineDto(t.DocumentLineId is int li && numeros.TryGetValue(li, out var n) ? n : null,
                    t.Kind, t.TaxRateCode, t.Rate, t.AmountPerUnit, t.Base, t.Amount, t.Treatment, t.MunicipalityDaneCode, t.ExplanationJson)).ToList()
                : factura.Value.TaxLines,
            eventos,
            confirmada.Value.Messages is null && facturaConfirmada?.Messages is null ? null : mensajes,
            recepcion.Value.Warnings.Concat(factura.Value.Warnings).ToList()));
    }

    private static Result<DirectPurchaseResultDto> Falla(Error error) => Result.Failure<DirectPurchaseResultDto>(error);
}

/// <summary>
/// La última aprobación de la recepción de una compra directa confirma también su factura (T344): la que está en borrador
/// enlazada a ella (<c>InvoiceOfReceipt</c>). Sólo la compra directa deja una factura así (el borrador común no admite una
/// recepción sin confirmar). <see cref="ConfirmacionDeDocumento"/> se resuelve al usarla: depende del motor de aprobaciones,
/// que depende de las fuentes. (nuevo)
/// </summary>
public sealed class CompraDirectaEncadenada(IApplicationDbContext db, IServiceProvider servicios) : IConfirmacionEncadenada
{
    public async Task<Result> AlConfirmarPorAprobacionAsync(Guid documentoPublicId, CancellationToken ct)
    {
        var recepcion = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.PublicId == documentoPublicId && d.Class == DocumentClass.PurchaseReceipt)
            .Select(d => new { d.Id, d.Status }).FirstOrDefaultAsync(ct);
        if (recepcion is null || recepcion.Status != DocumentStatus.Confirmed) return Result.Success();

        var facturas = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == recepcion.Id && l.Kind == DocumentLinkKind.InvoiceOfReceipt)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => d.Status == DocumentStatus.Draft && d.Class == DocumentClass.SupplierInvoice)
            .Select(d => d.PublicId).Distinct().ToListAsync(ct);
        if (facturas.Count == 0) return Result.Success();

        var confirmacion = servicios.GetRequiredService<ConfirmacionDeDocumento>();
        foreach (var factura in facturas)
        {
            var r = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(factura, DocumentClassGroup.Purchases), ct);
            if (r.IsFailure) return Result.Failure(r.Error);
        }
        return Result.Success();
    }
}
