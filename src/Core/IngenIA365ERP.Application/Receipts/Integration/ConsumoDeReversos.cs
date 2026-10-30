using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Integration;

/// <summary>
/// Consume en Recaudo el reverso que hizo el módulo de un pago o una consignación que Recaudo había aplicado (feature 015, US5, T0176; mensajes.md §3.1; FR-059,
/// FR-063, FR-195): la porción del recibo vigente cuya clave es la del aviso pasa a reversada, su aplicación a <c>ReversedByModule</c> y nace
/// <c>RCD_ModuleReversals</c> con la causa <c>PendingConfirmation</c> (el aviso no la trae): una persona confirma si el dinero se recibió. Alerta
/// <c>Recaudo.AplicacionReversadaPorModulo</c>. Repetido, sin efecto (recibo único en <c>RCD_InboundMessages</c>); una clave que Recaudo no conoce,
/// <c>Receipts.Reversal.UnknownApplication</c>; de otro recaudador, <c>.NotOurs</c>; lo que Recaudo mismo reversó al anular, sin efecto. Nunca devuelve ni reaplica
/// solo. Sin ruta: lo manda <see cref="DestinoRecaudo"/>. (nuevo)
/// </summary>
public sealed record ConsumeLendingPaymentReversalCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeLendingPaymentReversalCommandValidator : AbstractValidator<ConsumeLendingPaymentReversalCommand>
{
    public ConsumeLendingPaymentReversalCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeLendingPaymentReversalCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ICerrojoDeRecaudo cerrojo,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<ConsumeLendingPaymentReversalCommand, Result<ResultadoDeConsumo>>
{
    /// <summary>Lo que el consumo necesita del aviso, de un pago o de una consignación.</summary>
    private sealed record Aviso(CollectorRefV1 Collector, Guid ReversalPublicId, string ReversalNumber, decimal Amount, string Reason);

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeLendingPaymentReversalCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Receipts, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await db.ReceiptsInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == mensaje.MessageId, ct) is { } previo)
            return Result.Success<ResultadoDeConsumo>(previo.Outcome == InboundMessageOutcome.Processed
                ? new ResultadoDeConsumo.AlreadyProcessed(previo.PublicId, SinComprobante: MotivoSinComprobante.Informational)
                : new ResultadoDeConsumo.Rejected(previo.Code ?? ErroresDeRecaudo.CodigoAplicacionDesconocida, previo.Detail ?? "El aviso fue rechazado al recibirlo."));
        Aviso? aviso = mensaje.Payload switch
        {
            PagoReversadoPorCarteraV1 p when mensaje.Version == 1 => new(p.Collector, p.ReversalOperationPublicId, p.ReversalNumber, p.ReversedAmount, p.Reason),
            ConsignacionReversadaPorCarteraV1 c when mensaje.Version == 1 => new(c.Collector, c.ReversalOperationPublicId, c.ReversalNumber, c.ReversedAmount, c.Reason),
            _ => null,
        };
        if (aviso is null)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Recaudo no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            if (!string.Equals(aviso.Collector.CollectorCode, ClavesDeRecaudo.Codigo, StringComparison.Ordinal))
                return await RechazarAsync(mensaje, ahora, aviso.Collector.CollectorClass == "Payroll" ? ErroresDeRecaudo.DeNomina() : ErroresDeRecaudo.DeOtroRecaudador(aviso.Collector.CollectorCode), ct);
            var porcionLeida = await db.ReceiptApplicationPortions.AsNoTracking().FirstOrDefaultAsync(p => p.IdempotencyKey == aviso.Collector.IdempotencyKey, ct);
            if (porcionLeida is null) return await RechazarAsync(mensaje, ahora, ErroresDeRecaudo.AplicacionDesconocida(aviso.Collector.IdempotencyKey), ct);
            var aplicacionLeida = await db.ReceiptApplications.AsNoTracking().FirstAsync(a => a.Id == porcionLeida.ReceiptApplicationId, ct);
            var recibo = await db.IssuedReceipts.AsNoTracking().FirstAsync(r => r.Id == aplicacionLeida.ReceiptId, ct);

            await cerrojo.TomarSetupAsync(ct: ct);
            await cerrojo.BloquearPagadoresAsync([recibo.PayerPersonId], ct);
            var porcion = await db.ReceiptApplicationPortions.FirstAsync(p => p.Id == porcionLeida.Id, ct);
            if (porcion.Status == PortionState.Reversed)
            {
                // La reversó Recaudo al anular el recibo (o ya llegó este aviso por otro mensaje): sin efecto.
                db.ReceiptsInboundMessages.Add(Recibo(mensaje, ahora, InboundMessageOutcome.Processed, null,
                    $"La porción {porcion.IdempotencyKey} ya estaba reversada: sin efecto."));
                await db.SaveChangesAsync(ct);
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(porcion.PublicId, null, null, MotivoSinComprobante.Informational));
            }
            var aplicacion = await db.ReceiptApplications.FirstAsync(a => a.Id == porcion.ReceiptApplicationId, ct);
            porcion.Status = PortionState.Reversed;
            if (aplicacion.Status == ApplicationState.Applied) aplicacion.Status = ApplicationState.ReversedByModule;
            var entrada = Recibo(mensaje, ahora, InboundMessageOutcome.Processed, null, $"Reverso {aviso.ReversalNumber} de la porción {porcion.IdempotencyKey}.");
            db.ReceiptsInboundMessages.Add(entrada);
            await db.SaveChangesAsync(ct);
            var reverso = new ModuleReversal
            {
                InboundMessageId = entrada.Id, ReceiptApplicationId = aplicacion.Id, PortionId = porcion.Id, Module = mensaje.OriginModule, MessageType = mensaje.Type,
                ModuleReversalPublicId = aviso.ReversalPublicId, ModuleReversalNumber = Recortar(aviso.ReversalNumber, 30),
                Amount = aviso.Amount > 0m ? Math.Min(aviso.Amount, porcion.Amount) : porcion.Amount, Reason = Recortar(aviso.Reason, 500),
            };
            db.ModuleReversals.Add(reverso);
            await db.SaveChangesAsync(ct);
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoAplicacionReversadaPorModulo, "Aplicación reversada por el módulo",
                string.Create(CultureInfo.InvariantCulture,
                    $"El módulo reversó {reverso.Amount:N0} del recibo {recibo.Number} ({aviso.ReversalNumber}): confirme si el dinero se recibió."),
                "ModuleReversal", reverso.PublicId, DedupKey: $"Recaudo.AplicacionReversadaPorModulo:{reverso.PublicId:N}", ModuloDeOrigen: "Receipts",
                ScopeBranchPublicId: await db.Branches.AsNoTracking().Where(b => b.Id == recibo.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)), ct);
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(reverso.PublicId, null, null, MotivoSinComprobante.Informational));
        }, ct);
    }

    private async Task<Result<ResultadoDeConsumo>> RechazarAsync(MensajeEntrante m, DateTime ahora, Error error, CancellationToken ct)
    {
        db.ReceiptsInboundMessages.Add(Recibo(m, ahora, InboundMessageOutcome.Rejected, error.Code, error.Message));
        await db.SaveChangesAsync(ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(error.Code, error.Message));
    }

    private static ReceiptsInboundMessage Recibo(MensajeEntrante m, DateTime ahora, InboundMessageOutcome resultado, string? codigo, string? detalle) => new()
    {
        MessagePublicId = m.MessageId, Type = m.Type, Version = (short)m.Version, OriginModule = m.OriginModule, ReceivedAt = ahora, Outcome = resultado, Code = codigo,
        Detail = detalle is { Length: > 1000 } ? detalle[..1000] : detalle,
    };

    private static string Recortar(string? texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
