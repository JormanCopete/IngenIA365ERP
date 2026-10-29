using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Consume en Comercial <c>PagoDeFacturaDeProveedor</c> v1 (feature 015, T2, fase 42, T0454; E-03; FR-124, FR-166): guarda el pago en <c>INV_SupplierInvoicePayments</c>
/// contra su factura; repetir el mensaje no duplica el pago. Comercial no lee tablas de Tesorería. Sin ruta. (nuevo)
/// </summary>
public sealed record ConsumeSupplierInvoicePaymentCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeSupplierInvoicePaymentCommandValidator : AbstractValidator<ConsumeSupplierInvoicePaymentCommand>
{
    public ConsumeSupplierInvoicePaymentCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeSupplierInvoicePaymentCommandHandler(IApplicationDbContext db, IMensajesEntrantes entrantes, IDateTimeService reloj)
    : IRequestHandler<ConsumeSupplierInvoicePaymentCommand, Result<ResultadoDeConsumo>>
{
    public const string CodigoFacturaInexistente = "Inventory.SupplierInvoice.PaymentForUnknownInvoice";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeSupplierInvoicePaymentCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Inventory, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (mensaje.Payload is not PagoDeFacturaDeProveedorV1 pago || mensaje.Version != 1)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Comercial no acepta {mensaje.Type} v{mensaje.Version}."));
        if (await db.SupplierInvoicePayments.AsNoTracking().AnyAsync(p => p.MessagePublicId == mensaje.MessageId, ct))
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(null, SinComprobante: MotivoSinComprobante.Informational));
        var factura = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == pago.InvoicePublicId).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (factura is null)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(CodigoFacturaInexistente,
                $"La factura {pago.InvoiceNumber} que pagó Tesorería no está en Comercial."));
        db.SupplierInvoicePayments.Add(new SupplierInvoicePayment
        {
            DocumentId = factura.Value, MessagePublicId = mensaje.MessageId, PaymentOrderPublicId = pago.PaymentOrderPublicId, OrderNumber = pago.OrderNumber,
            Amount = pago.Amount, PaidOn = pago.PaidOn, TreasuryReference = pago.TreasuryReference, ReceivedAt = reloj.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
    }
}
