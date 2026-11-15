using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Anula la compra completa (2026-11-10, pedido del dueño): la compra directa nace en un paso —recepción y factura del proveedor—
/// y se anula en un paso, desde cualquiera de los dos documentos (<c>POST /purchases/direct/{id}/void</c> con
/// <c>{ reason, operationDate? }</c>). Sirve para toda pareja uno a uno: una recepción con una sola factura vigente que no cubre
/// otra recepción. Anula primero la factura y después la recepción, cada una por <see cref="VoidInventoryDocumentCommandHandler"/>
/// con sus mismas reglas, en <b>una</b> transacción: si una no se puede anular, no queda ninguna anulada. (nuevo)
/// </summary>
public sealed record VoidDirectPurchaseCommand(Guid DocumentPublicId, string Reason, DateOnly? OperationDate = null)
    : IRequest<Result<VoidDirectPurchaseResultDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>Las dos anulaciones de la compra: la de la factura y la de la recepción.</summary>
public sealed record VoidDirectPurchaseResultDto(Guid ReceiptPublicId, Guid InvoicePublicId, VoidResultDto InvoiceVoiding, VoidResultDto ReceiptVoiding);

public sealed class VoidDirectPurchaseCommandValidator : ValidadorConMotivo<VoidDirectPurchaseCommand>
{
    public VoidDirectPurchaseCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
    }
}

public sealed class VoidDirectPurchaseCommandHandler(IApplicationDbContext db, VoidInventoryDocumentCommandHandler anular)
    : IRequestHandler<VoidDirectPurchaseCommand, Result<VoidDirectPurchaseResultDto>>
{
    private static readonly DocumentStatus[] Vigentes = [DocumentStatus.PendingApproval, DocumentStatus.Confirmed];

    public Task<Result<VoidDirectPurchaseResultDto>> Handle(VoidDirectPurchaseCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => AnularAsync(request, ct), ct);

    private async Task<Result<VoidDirectPurchaseResultDto>> AnularAsync(VoidDirectPurchaseCommand request, CancellationToken ct)
    {
        var pareja = await ParejaAsync(db, request.DocumentPublicId, ct);
        if (pareja.IsFailure) return Result.Failure<VoidDirectPurchaseResultDto>(pareja.Error);
        var (recepcion, factura) = pareja.Value;

        var deLaFactura = await anular.Handle(new VoidInventoryDocumentCommand(factura, DocumentClassGroup.Purchases, request.Reason, request.OperationDate), ct);
        if (deLaFactura.IsFailure) return Result.Failure<VoidDirectPurchaseResultDto>(deLaFactura.Error);
        var deLaRecepcion = await anular.Handle(new VoidInventoryDocumentCommand(recepcion, DocumentClassGroup.Purchases, request.Reason, request.OperationDate), ct);
        if (deLaRecepcion.IsFailure) return Result.Failure<VoidDirectPurchaseResultDto>(deLaRecepcion.Error);

        return Result.Success(new VoidDirectPurchaseResultDto(recepcion, factura, deLaFactura.Value, deLaRecepcion.Value));
    }

    /// <summary>
    /// La recepción y la factura de una compra uno a uno, partiendo de cualquiera de las dos. Sin pareja (otra clase, una recepción
    /// sin factura vigente, una factura de varias recepciones o una recepción con varias facturas) responde
    /// <c>Inventory.Purchase.NotSinglePurchase</c>: esas se anulan documento por documento.
    /// </summary>
    internal static async Task<Result<(Guid Recepcion, Guid Factura)>> ParejaAsync(IApplicationDbContext db, Guid documento, CancellationToken ct)
    {
        var doc = await db.InventoryDocuments.AsNoTracking()
            .Where(d => d.PublicId == documento && (d.Class == DocumentClass.PurchaseReceipt || d.Class == DocumentClass.SupplierInvoice))
            .Select(d => new { d.Id, d.PublicId, d.Class })
            .FirstOrDefaultAsync(ct);
        if (doc is null) return Result.Failure<(Guid, Guid)>(InventoryErrors.DocumentNotFound());

        var recepcionId = doc.Class == DocumentClass.PurchaseReceipt ? doc.Id : (int?)null;
        if (recepcionId is null)
        {
            var recepciones = await RecepcionesDeAsync(db, doc.Id, ct);
            if (recepciones.Count != 1) return Result.Failure<(Guid, Guid)>(ErroresDeAnulacionDeCompra.NotSinglePurchase());
            recepcionId = recepciones[0];
        }

        var facturas = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == recepcionId && l.Kind == DocumentLinkKind.InvoiceOfReceipt && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => Vigentes.Contains(d.Status))
            .Select(d => d.Id).Distinct().ToListAsync(ct);
        if (facturas.Count != 1) return Result.Failure<(Guid, Guid)>(ErroresDeAnulacionDeCompra.NotSinglePurchase());
        if ((await RecepcionesDeAsync(db, facturas[0], ct)).Count != 1) return Result.Failure<(Guid, Guid)>(ErroresDeAnulacionDeCompra.NotSinglePurchase());

        var ids = new[] { recepcionId.Value, facturas[0] };
        var publicos = await db.InventoryDocuments.AsNoTracking().Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.PublicId, ct);
        return Result.Success((publicos[recepcionId.Value], publicos[facturas[0]]));
    }

    private static Task<List<int>> RecepcionesDeAsync(IApplicationDbContext db, int facturaId, CancellationToken ct) =>
        db.DocumentLinks.AsNoTracking()
            .Where(l => l.TargetDocumentId == facturaId && l.Kind == DocumentLinkKind.InvoiceOfReceipt && !l.IsDeleted)
            .Select(l => l.SourceDocumentId).Distinct().ToListAsync(ct);
}

/// <summary>El error de la anulación de la compra completa. Responde 422.</summary>
public static class ErroresDeAnulacionDeCompra
{
    public static Error NotSinglePurchase() => new("Inventory.Purchase.NotSinglePurchase",
        "Esta compra no es una recepción con una sola factura: anule cada documento por separado (primero la factura, después la recepción).");
}
