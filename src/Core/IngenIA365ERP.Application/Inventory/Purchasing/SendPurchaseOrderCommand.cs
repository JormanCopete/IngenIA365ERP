using System.Net;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Envía la orden de compra al proveedor en PDF (<c>POST /api/inventory/purchases/orders/{id}/send</c>, <c>Inventory.Purchases.Confirm</c>
/// → 204; feature 012, I5, T791; FR-048; contracts/api.md §14.9). (nuevo)
/// <list type="bullet">
/// <item>sólo una orden <c>Confirmed</c> (<c>Inventory.PurchaseOrder.NotConfirmed</c>); fuera del alcance o de otra clase, el 404 del
/// documento;</item>
/// <item>el correo es <see cref="Email"/> o, si no viene, el del proveedor en el maestro de personas
/// (<c>Inventory.PurchaseOrder.SupplierEmailMissing</c> si no tiene);</item>
/// <item>el PDF lo dibuja <see cref="IOrdenDeCompraEnPdf"/> y sale por <see cref="IEmailSender"/> directo;</item>
/// <item>cada envío queda en la auditoría como <c>Inventory.PurchaseOrder.Sent</c> (nuevo) con el correo y el número, por
/// <see cref="InventoryAuditEmitter"/> con el PublicId de la cooperativa. Enviar otra vez es otro envío, con otra clave de operación.</item>
/// </list>
/// </summary>
public sealed record SendPurchaseOrderCommand(Guid OrderPublicId, string? Email = null) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendPurchaseOrderCommandValidator : AbstractValidator<SendPurchaseOrderCommand>
{
    public SendPurchaseOrderCommandValidator()
    {
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class SendPurchaseOrderCommandHandler(
    IApplicationDbContext db,
    VistaDeDocumentos vista,
    ModeloDeOrdenDeCompra modelo,
    IEnumerable<IOrdenDeCompraEnPdf> generadores,
    IEmailSender correo,
    InventoryAuditEmitter auditoria)
    : IRequestHandler<SendPurchaseOrderCommand, Result>
{
    public async Task<Result> Handle(SendPurchaseOrderCommand request, CancellationToken ct)
    {
        var orden = await vista.BuscarAsync(request.OrderPublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (orden is null || orden.Class != DocumentClass.PurchaseOrder) return Result.Failure(InventoryErrors.DocumentNotFound());
        if (orden.Status != DocumentStatus.Confirmed) return Result.Failure(ErroresDeCompras.OrderNotConfirmed(orden.Status));

        var destino = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(destino))
        {
            var proveedor = await db.People.AsNoTracking().Where(p => p.Id == orden.CounterpartyPersonId)
                .Select(p => new { p.PublicId, p.Email }).FirstOrDefaultAsync(ct);
            destino = proveedor?.Email?.Trim();
            if (string.IsNullOrWhiteSpace(destino)) return Result.Failure(ErroresDeCompras.SupplierEmailMissing(proveedor?.PublicId ?? Guid.Empty));
        }

        var pdf = await OrdenesDeCompraEnPdf.GenerarAsync(orden, modelo, generadores, ct);
        if (pdf.IsFailure) return Result.Failure(pdf.Error);
        var o = pdf.Value.Orden;
        var numero = o.DisplayNumber ?? string.Empty;
        var entrega = o.ExpectedDate is { } fecha ? $" con entrega esperada el {fecha:dd/MM/yyyy}" : string.Empty;
        await correo.SendAsync(new EmailMessage(destino,
            $"Orden de compra {numero} — {o.Cooperativa.Name}",
            $"<p>Señores {WebUtility.HtmlEncode(o.Proveedor.Name)}:</p><p>Adjuntamos la orden de compra {WebUtility.HtmlEncode(numero)} de "
            + $"{WebUtility.HtmlEncode(o.Cooperativa.Name)}{WebUtility.HtmlEncode(entrega)}.</p>",
            Attachments: [new EmailAttachment(pdf.Value.FileName, "application/pdf", pdf.Value.Pdf)]), ct);

        await auditoria.EmitAsync(AuditEventTypes.InventoryPurchaseOrderSent, "InventoryDocument", orden.PublicId, null,
            new { email = destino, displayNumber = numero, fileName = pdf.Value.FileName }, ct);
        return Result.Success();
    }
}
