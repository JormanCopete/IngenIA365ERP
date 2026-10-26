using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Treasury.Batches;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Orders;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Gmf;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Integration;

// Feature 015, US10 (T0140; FR-107, FR-108, FR-115, FR-121, FR-151, FR-206; contracts/mensajes.md §5.1, §5.2): el consumo de las órdenes de giro de Cartera y
// Captaciones y de su anulación. Sin ruta (LosComandosDeConsumoNoTienenRuta): los manda DestinoTesoreria, como «Proceso de integración». Cada mensaje deja su
// recibo único en TES_InboundMessages en el mismo SaveChanges que su efecto; repetirlo no cambia nada. Responden siempre un ResultadoDeConsumo; la falla del Result
// queda para lo que no se pudo leer. (nuevo)

/// <summary>Lo común del recibo de mensajes de Tesorería. (nuevo)</summary>
internal static class RecibosDeTesoreria
{
    public static async Task<TreasuryInboundMessage?> PrevioAsync(IApplicationDbContext db, Guid mensaje, CancellationToken ct) =>
        await db.TreasuryInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == mensaje, ct);

    /// <summary>Lo que se responde ante un mensaje ya recibido: el mismo resultado, sin un segundo efecto.</summary>
    public static ResultadoDeConsumo Repetido(TreasuryInboundMessage previo) => previo.Outcome == InboundMessageOutcome.Processed
        ? new ResultadoDeConsumo.AlreadyProcessed(previo.PublicId, SinComprobante: MotivoSinComprobante.Informational)
        : new ResultadoDeConsumo.Rejected(previo.Code ?? "Treasury.Order.Rejected", previo.Detail ?? "El mensaje fue rechazado al recibirlo.");

    public static TreasuryInboundMessage Nuevo(MensajeEntrante m, DateTime ahora, InboundMessageOutcome resultado, int? orden, string? codigo = null, string? detalle = null) =>
        new()
        {
            MessagePublicId = m.MessageId, Type = m.Type, Version = (short)m.Version, OriginModule = m.OriginModule, ReceivedAt = ahora, Outcome = resultado,
            PaymentOrderId = orden, Code = codigo, Detail = detalle is { Length: > 1000 } ? detalle[..1000] : detalle,
        };

    public const string Actor = "Proceso de integración";
}

/// <summary>
/// Consume en Tesorería una <c>OrdenDeGiroEmitida</c> v1 o v2 (mensajes.md §5.1): crea la orden en la bandeja única (<c>Received</c>), la valida
/// (<see cref="ValidadorDeOrdenes"/>: <c>Ready</c> o <c>Pending</c> con motivos) y sella la exención del GMF por operación (FR-206). Una orden repetida no se
/// duplica; <c>NetAmount ≤ 0</c> o una forma que no existe la dejan <c>RejectedOnArrival</c> con su código; <c>PaidByFile</c> la deja lista para recibir su resultado,
/// sin entrar a lotes (FR-108); <c>ReplacesOrderPublicId</c> enlaza la cadena de regiros (FR-121). En Tesorería no entra una orden con factura o gasto propio: sólo
/// las de un módulo (FR-127). (nuevo)
/// </summary>
public sealed record ConsumePaymentOrderCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumePaymentOrderCommandValidator : AbstractValidator<ConsumePaymentOrderCommand>
{
    public ConsumePaymentOrderCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumePaymentOrderCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    ValidadorDeOrdenes validador,
    ParametrosDeTesoreria parametros,
    IDateTimeService reloj) : IRequestHandler<ConsumePaymentOrderCommand, Result<ResultadoDeConsumo>>
{
    public const string CodigoDeOrden = "OP";

    /// <summary>Lo que el consumo necesita de una orden de giro, de la v1 o la v2.</summary>
    private sealed record OrdenRecibida(Guid OrderPublicId, string OrderNumber, string OrderKind, string Form, PayeeV1 Payee, decimal GrossAmount,
        decimal NetAmount, DateOnly PayDate, string Concept, bool PaidByFile, Guid? ReplacesOrderPublicId, Guid? SourceDocumentPublicId);

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumePaymentOrderCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Treasury, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await RecibosDeTesoreria.PrevioAsync(db, mensaje.MessageId, ct) is { } previo) return Result.Success(RecibosDeTesoreria.Repetido(previo));
        OrdenRecibida? recibida = mensaje.Payload switch
        {
            OrdenDeGiroEmitidaV1 v1 when mensaje.Version == 1 => new(v1.OrderPublicId, v1.OrderNumber, EvaluadorDeGmfDeEgresos.ClaseDesembolso, v1.Form, v1.Payee,
                v1.GrossAmount, v1.NetAmount, v1.PayDate, v1.Concept, v1.PaidByFile, v1.ReplacesOrderPublicId, null),
            OrdenDeGiroEmitidaV2 v2 when mensaje.Version == 2 => new(v2.OrderPublicId, v2.OrderNumber, v2.OrderKind, v2.Form, v2.Payee, v2.GrossAmount, v2.NetAmount,
                v2.PayDate, v2.Concept, v2.PaidByFile, v2.ReplacesOrderPublicId, v2.SourceDocumentPublicId),
            _ => null,
        };
        if (recibida is null)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Tesorería no acepta {mensaje.Type} v{mensaje.Version}."));
        var origen = string.Equals(recibida.OrderKind, EvaluadorDeGmfDeEgresos.ClaseDesembolso, StringComparison.Ordinal)
            ? PaymentOrderOrigin.LendingDisbursement
            : PaymentOrderOrigin.DepositsPayout;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            await cerrojo.TomarSetupAsync(ct: ct);
            if (await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Origin == origen && o.OriginOrderPublicId == recibida.OrderPublicId, ct) is { } ya)
            {
                db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Processed, ya.Id, "Treasury.Order.Duplicate",
                    $"La orden {recibida.OrderNumber} ya estaba en la bandeja como {ya.Number}: sin efecto."));
                await db.SaveChangesAsync(ct);
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(ya.PublicId, null, null, MotivoSinComprobante.Informational));
            }

            var beneficiario = await db.People.AsNoTracking().Where(p => p.PublicId == recibida.Payee.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (beneficiario is null)
                return await RechazarSinOrdenAsync(mensaje, ahora, ErroresDeTesoreria.CodigoBeneficiarioInexistente,
                    $"El beneficiario de la orden {recibida.OrderNumber} no existe en el maestro de personas.", ct);

            var orden = new PaymentOrder
            {
                Origin = origen, OriginOrderPublicId = recibida.OrderPublicId, OriginOrderNumber = Recortar(recibida.OrderNumber, 30),
                OriginMessagePublicId = mensaje.MessageId, OriginMessageVersion = (short)mensaje.Version, OrderKind = Recortar(recibida.OrderKind, 40),
                BeneficiaryPersonId = beneficiario.Value, BeneficiaryName = Recortar(recibida.Payee.Name, 200),
                BeneficiaryTaxIdType = string.IsNullOrWhiteSpace(recibida.Payee.TaxIdType) ? null : Recortar(recibida.Payee.TaxIdType, 10),
                BeneficiaryTaxId = Recortar(recibida.Payee.TaxId, 20), DestinationBankCode = Nulo(recibida.Payee.BankCode, 10),
                DestinationAccountNumber = Nulo(recibida.Payee.BankAccountNumber, 30), DestinationAccountType = Nulo(recibida.Payee.BankAccountType, 20),
                GrossAmount = recibida.GrossAmount, NetAmount = recibida.NetAmount, PayDate = recibida.PayDate, Concept = Recortar(recibida.Concept, 120),
                SourceDocumentPublicId = recibida.SourceDocumentPublicId, BranchPublicId = mensaje.Envelope.BranchPublicId,
                PaidByLendingFile = recibida.PaidByFile, BeneficiaryIsDebtor = mensaje.Envelope.PersonPublicId == recibida.Payee.PersonPublicId,
                ReceivedAt = ahora,
            };
            if (recibida.ReplacesOrderPublicId is { } anterior)
                orden.ReplacesOrderId = await db.PaymentOrders.AsNoTracking().Where(o => o.OriginOrderPublicId == anterior).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);

            string? codigo = null, detalle = null;
            if (!Enum.TryParse<PaymentForm>(recibida.Form, ignoreCase: false, out var forma) || !Enum.IsDefined(forma))
            {
                orden.Form = PaymentForm.Transfer;
                (codigo, detalle) = (ErroresDeTesoreria.CodigoFormaInvalida, ErroresDeTesoreria.FormaInvalida(recibida.Form).Message);
            }
            else orden.Form = forma;
            if (codigo is null && recibida.NetAmount <= 0m)
                (codigo, detalle) = (ErroresDeTesoreria.CodigoValorInvalido, ErroresDeTesoreria.ValorInvalido(recibida.NetAmount).Message);

            if (codigo is not null)
            {
                orden.Status = PaymentOrderState.RejectedOnArrival;
                orden.PendingReasonsJson = $"[\"{codigo}\"]";
            }
            else if (orden.PaidByLendingFile)
                orden.Status = PaymentOrderState.Ready;
            else
                await validador.ValidarAsync(orden, ct);

            if (codigo is null)
            {
                var gmf = EvaluadorDeGmfDeEgresos.Evaluar(new EgresoAEvaluar(orden.Origin, orden.OrderKind, orden.Form, orden.NetAmount, orden.BeneficiaryIsDebtor,
                    false, orden.Origin == PaymentOrderOrigin.DepositsPayout), await parametros.GmfAsync(orden.PayDate, ct));
                orden.GmfExempt = gmf.Exento;
                orden.GmfExemptionReason = gmf.Evaluado ? gmf.Motivo : null;
                orden.GmfEstimate = gmf.Gravado ? gmf.Gmf : 0m;
            }
            await numerador.NumerarAsync(orden, CodigoDeOrden, CodigoDeOrden, ct);
            db.PaymentOrders.Add(orden);
            await db.SaveChangesAsync(ct);
            db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora,
                codigo is null ? InboundMessageOutcome.Processed : InboundMessageOutcome.Rejected, orden.Id, codigo, detalle));
            await db.SaveChangesAsync(ct);
            return Result.Success<ResultadoDeConsumo>(codigo is null
                ? new ResultadoDeConsumo.Processed(orden.PublicId, null, null, MotivoSinComprobante.Informational)
                : new ResultadoDeConsumo.Rejected(codigo, detalle!));
        }, ct);
    }

    private async Task<Result<ResultadoDeConsumo>> RechazarSinOrdenAsync(MensajeEntrante m, DateTime ahora, string codigo, string detalle, CancellationToken ct)
    {
        db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(m, ahora, InboundMessageOutcome.Rejected, null, codigo, detalle));
        await db.SaveChangesAsync(ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(codigo, detalle));
    }

    private static string Recortar(string? texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }

    private static string? Nulo(string? texto, int largo) => string.IsNullOrWhiteSpace(texto) ? null : Recortar(texto, largo);
}

/// <summary>
/// Consume en Tesorería una <c>AnulacionDeOrdenDeGiro</c> v1 (mensajes.md §5.2; FR-115): la orden en la bandeja (recibida, pendiente o lista) queda anulada; en un
/// lote sin archivo enviado, sale del lote (el ítem queda retirado y el lote se recalcula) y queda anulada; enviada, queda con la anulación pedida hasta conocer
/// su resultado; con resultado, sin efecto. Quien anuló en el origen queda sellado en la orden: el resultado «anulada» sale a su nombre (lo emite US11). (nuevo)
/// </summary>
public sealed record ConsumePaymentOrderVoidCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumePaymentOrderVoidCommandValidator : AbstractValidator<ConsumePaymentOrderVoidCommand>
{
    public ConsumePaymentOrderVoidCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumePaymentOrderVoidCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ICerrojoDeTesoreria cerrojo,
    IMotorDeAprobaciones motor,
    Results.RegistroDeResultadosDelGiro resultados,
    IDateTimeService reloj) : IRequestHandler<ConsumePaymentOrderVoidCommand, Result<ResultadoDeConsumo>>
{
    public const string CodigoOrdenDesconocida = "Treasury.Order.UnknownOrder";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumePaymentOrderVoidCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Treasury, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await RecibosDeTesoreria.PrevioAsync(db, mensaje.MessageId, ct) is { } previo) return Result.Success(RecibosDeTesoreria.Repetido(previo));
        if (mensaje.Payload is not AnulacionDeOrdenDeGiroV1 anulacion || mensaje.Version != 1)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Tesorería no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            var id = await db.PaymentOrders.AsNoTracking()
                .Where(o => o.OriginOrderPublicId == anulacion.OrderPublicId
                            && (o.Origin == PaymentOrderOrigin.LendingDisbursement || o.Origin == PaymentOrderOrigin.DepositsPayout))
                .Select(o => (int?)o.Id).FirstOrDefaultAsync(ct);
            if (id is null)
            {
                var detalle = $"La orden {anulacion.OrderNumber} no está en la bandeja de Tesorería: no se puede anular.";
                db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Rejected, null, CodigoOrdenDesconocida, detalle));
                await db.SaveChangesAsync(ct);
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(CodigoOrdenDesconocida, detalle));
            }

            var item = await db.PaymentBatchItems.AsNoTracking().FirstOrDefaultAsync(i => i.PaymentOrderId == id.Value && i.IsLive, ct);
            var loteId = item?.PaymentBatchId;
            int? cuentaId = loteId is { } l ? await db.PaymentBatches.AsNoTracking().Where(b => b.Id == l).Select(b => (int?)b.BankAccountId).FirstAsync(ct) : null;
            await cerrojo.TomarSetupAsync(ct: ct);
            if (cuentaId is { } c) await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [c], exclusivo: false, ct);
            if (loteId is { } lt) await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [lt], ct: ct);
            await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, [id.Value], ct: ct);
            var orden = await db.PaymentOrders.FirstAsync(o => o.Id == id.Value, ct);
            orden.VoidReason = Recortar(anulacion.Reason, 500);
            orden.VoidedByCentralUserId = mensaje.Envelope.OriginUser.CentralUserId;
            orden.VoidedByName = Recortar(mensaje.Envelope.OriginUser.Name, 150);

            var efecto = "sin efecto: la orden ya tiene su resultado";
            switch (orden.Status)
            {
                case PaymentOrderState.Received or PaymentOrderState.Pending or PaymentOrderState.Ready:
                    await resultados.AnuladaPorElOrigenAsync(orden, mensaje.Envelope.OriginUser, ct);
                    efecto = "anulada";
                    break;
                case PaymentOrderState.InBatch or PaymentOrderState.Approved:
                    var vivo = await db.PaymentBatchItems.FirstAsync(i => i.PaymentOrderId == orden.Id && i.IsLive, ct);
                    await LotesDePago.SacarAsync(db, motor, vivo, $"Anulación de la orden {orden.Number} por el origen", ahora, ct);
                    await resultados.AnuladaPorElOrigenAsync(orden, mensaje.Envelope.OriginUser, ct);
                    efecto = "sale del lote y queda anulada";
                    break;
                case PaymentOrderState.Sent:
                    orden.VoidRequested = true;
                    efecto = "enviada: sigue hasta conocer su resultado";
                    break;
            }
            await db.SaveChangesAsync(ct);
            db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Processed, orden.Id, null,
                $"Anulación de la orden {orden.Number}: {efecto}."));
            await db.SaveChangesAsync(ct);
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(orden.PublicId, null, null, MotivoSinComprobante.Informational));
        }, ct);
    }

    private static string Recortar(string? texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
