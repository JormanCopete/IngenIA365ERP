using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Charges;

/// <summary>
/// Consume <c>FacturaDeCargoRegistrada</c> v1 (feature 014, US4, T117; mensajes.md §1.3 #40, §5.6; FR-128): Comercial informa el número y el estado de la
/// factura —o de la nota crédito— de un cargo de captación gravado. La Cartera <b>sólo lo guarda</b> en el cargo para mostrarlo: una factura rechazada no
/// cambia el cargo. Informativo: sin recibo de Cartera ni comprobante; repetirlo deja lo mismo (el último estado gana). Sin ruta: lo manda
/// <see cref="Integration.DestinoCartera"/> por la familia de Comercial. (nuevo)
/// </summary>
public sealed record ConsumeChargeInvoiceRegisteredCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeChargeInvoiceRegisteredCommandValidator : AbstractValidator<ConsumeChargeInvoiceRegisteredCommand>
{
    public ConsumeChargeInvoiceRegisteredCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeChargeInvoiceRegisteredCommandHandler(IMensajesEntrantes entrantes, FacturasDeCargos facturas)
    : IRequestHandler<ConsumeChargeInvoiceRegisteredCommand, Result<ResultadoDeConsumo>>
{
    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeChargeInvoiceRegisteredCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Lending, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        return Result.Success(await facturas.ConsumirAsync(leidos.Value[0], ct));
    }
}

/// <summary>Guarda en el cargo el estado de su factura en Comercial (ver <see cref="ConsumeChargeInvoiceRegisteredCommand"/>). (nuevo)</summary>
public sealed class FacturasDeCargos(IApplicationDbContext db)
{
    /// <summary>El cargo que nombra la factura no existe en la Cartera.</summary>
    public const string CodigoCargoInexistente = "Lending.Savings.ChargeNotFound";

    private const int LargoDelNumero = 40;

    private static readonly HashSet<string> Estados = ["Issued", "Accepted", "Rejected", "Voided"];

    public async Task<ResultadoDeConsumo> ConsumirAsync(MensajeEntrante m, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.Version != 1 || m.Payload is not FacturaDeCargoRegistradaV1 factura)
            return new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"La Cartera no conoce {m.Type} v{m.Version}.");
        if (!Estados.Contains(factura.Status))
            return new ResultadoDeConsumo.Rejected("Validation.Invalid", $"«{factura.Status}» no es un estado de factura (Issued, Accepted, Rejected o Voided).");
        var cargo = await db.AccountCharges.FirstOrDefaultAsync(c => c.PublicId == factura.ChargePublicId && !c.IsDeleted, ct);
        if (cargo is null) return new ResultadoDeConsumo.Rejected(CodigoCargoInexistente, $"La Cartera no tiene el cargo {factura.ChargePublicId:N}.");
        // Una nota crédito (sobre el CargoDeCaptacionReversado) anula la factura: el cargo muestra «Voided» con el número de la nota.
        var numero = factura.Document.Number;
        cargo.CommercialInvoiceNumber = string.IsNullOrWhiteSpace(numero) ? cargo.CommercialInvoiceNumber
            : numero.Length <= LargoDelNumero ? numero : numero[..LargoDelNumero];
        cargo.CommercialInvoiceStatus = factura.ChargeMessageId == cargo.ReversalMessagePublicId && cargo.ReversalMessagePublicId is not null
            ? "Voided" : factura.Status;
        await db.SaveChangesAsync(ct);
        return new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational);
    }
}
