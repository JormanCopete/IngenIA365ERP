using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021, US7 (T117; FR-052, FR-053; contracts/erp-api-de-servicio.md, «reversal»): el ERP reversa el pago del canal. Anula el recibo «en línea»
// por los contratos del módulo dueño —el mismo núcleo de la anulación de Recaudo, con su asiento espejo— y marca el aviso. Por la pasarela (antes del
// corte) el dinero sale del medio; por Tesorería (después) nace un saldo a favor del asociado que se devuelve con una orden de pago, junto con el saldo a
// favor que el pago hubiera dejado. Lo pide un funcionario (ReverseChannelPaymentCommand), nunca el canal solo: el mensaje contable lo origina él.

/// <summary>Cómo se reversa un pago del canal (FR-052).</summary>
public enum ModoDeReversoDelCanal
{
    /// <summary>Antes del corte: la pasarela devolvió el pago.</summary>
    Gateway = 1,

    /// <summary>Después del corte: devolución por Tesorería.</summary>
    Treasury = 2,
}

/// <summary>Una orden de pago de la devolución.</summary>
public sealed record ChannelRefundOrderDto(Guid PaymentOrderPublicId, string Number, decimal Amount);

/// <summary>El reverso de un pago del canal visto desde el ERP. <c>ChannelUpdated = false</c>: el canal no se enteró todavía; repetir el reverso lo avisa.</summary>
public sealed record ChannelPaymentReversalDto(
    string Reference,
    string Mode,
    DateTime ReversedAt,
    string? Reason,
    Guid? ReceiptPublicId,
    string? ReceiptNumber,
    Guid? VoidingPublicId,
    string? VoidingNumber,
    IReadOnlyList<ChannelRefundOrderDto> RefundOrders,
    bool ChannelUpdated = true);

/// <summary>Los errores del reverso de un pago del canal.</summary>
public static class ErroresDelReversoDelCanal
{
    public const string NoExiste = "Channel.Notice.NotFound";
    public const string SinRecibo = "Channel.Reversal.NoReceipt";
    public const string ReciboNoVigente = "Channel.Reversal.ReceiptNotValid";
    public const string RechazadoPorLaPasarela = "Channel.Reversal.GatewayRejected";
    public const string ReciboSinAnular = "Channel.Reversal.GatewayReversedButNotVoided";
    public const string FueraDeCorte = "Canal.Reverso.FueraDeCorte";

    public static Error AvisoInexistente() => new(NoExiste, "No hay un pago del canal con esa referencia.");

    public static Error NoHayRecibo() => new(SinRecibo,
        "Este pago no dejó recibo en Recaudo (quedó en revisión sin aplicar): resuelva su partida o su saldo en Recaudo y devuélvalo desde allí.");

    public static Error ReciboAnuladoORevisado(string numero) => new(ReciboNoVigente,
        $"El recibo {numero} de este pago ya no está vigente (anulado o con una anulación pendiente en Recaudo): el reverso no se puede hacer desde el canal.");

    public static Error LaPasarelaNoLoReverso(string? motivo) => new(RechazadoPorLaPasarela,
        $"La pasarela no reversó el pago{(string.IsNullOrWhiteSpace(motivo) ? "." : $": {motivo}")} No se cambió nada en el ERP.");

    public static Error ReversadoSinAnular(Error causa) => new(ReciboSinAnular,
        $"La pasarela ya reversó el pago, pero Recaudo no pudo anular el recibo ({causa.Message}). Corrija la causa y repita el reverso: la pasarela no lo reversa otra vez.");
}

/// <summary>Lo común de los reversos del canal: la clave de la operación y la respuesta.</summary>
public static class ReversosDelCanal
{
    /// <summary>La clave de operación del reverso de una referencia: la misma siempre (repetir no anula dos veces).</summary>
    public static Guid ClaveDeOperacion(string referencia) => AplicacionNoInteractiva.ClaveDelRecibo("CDG.Reverso|" + referencia);

    /// <summary>La clave de la orden de pago de un saldo devuelto por el reverso (Tesorería es idempotente por ella).</summary>
    public static Guid ClaveDeLaOrden(string referencia, Guid saldo) => AplicacionNoInteractiva.ClaveDelRecibo($"CDG.Devolucion|{referencia}|{saldo:N}");

    public static async Task<ChannelPaymentReversalDto> DtoAsync(IApplicationDbContext db, ChannelPaymentNotice n, CancellationToken ct)
    {
        var recibo = n.ReceiptId is { } id
            ? await db.IssuedReceipts.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.Id, r.PublicId, r.Number }).FirstOrDefaultAsync(ct)
            : null;
        var anulacion = recibo is null
            ? null
            : await db.ReceiptVoidings.AsNoTracking().Where(v => v.ReceiptId == recibo.Id && v.Status == ReceiptVoidingState.Applied)
                .OrderByDescending(v => v.Id).FirstOrDefaultAsync(ct);
        var ordenes = new List<ChannelRefundOrderDto>();
        if (recibo is not null)
        {
            var saldos = await db.PayerCreditBalances.AsNoTracking()
                .Where(s => s.OriginDocumentPublicId == recibo.PublicId || (anulacion != null && s.Id == anulacion.PayerCreditBalanceId))
                .Select(s => s.Id).ToListAsync(ct);
            ordenes = await db.PayerCreditBalanceMovements.AsNoTracking()
                .Where(m => saldos.Contains(m.PayerCreditBalanceId) && m.Kind == PayerCreditBalanceMovementKind.RefundedByOrder && m.DocumentPublicId != null)
                .OrderBy(m => m.Id)
                .Select(m => new ChannelRefundOrderDto(m.DocumentPublicId!.Value, m.DocumentNumber ?? string.Empty, m.Amount)).ToListAsync(ct);
        }
        var modo = anulacion?.PayerCreditBalanceId is not null ? ModoDeReversoDelCanal.Treasury : ModoDeReversoDelCanal.Gateway;
        return new ChannelPaymentReversalDto(n.Reference, modo.ToString(), n.ReversedAt ?? default, n.ReversalReason, recibo?.PublicId, recibo?.Number,
            anulacion?.PublicId, anulacion?.Number, ordenes);
    }

    /// <summary>¿Lo reversó la pasarela? Sin recibo o con la anulación sin saldo a favor: sí (Tesorería siempre deja un saldo que devuelve).</summary>
    public static async Task<bool> PorLaPasarelaAsync(IApplicationDbContext db, ChannelPaymentNotice n, CancellationToken ct) =>
        n.ReversedAt is not null
        && (n.ReceiptId is not { } id
            || !await db.ReceiptVoidings.AsNoTracking().AnyAsync(v => v.ReceiptId == id && v.Status == ReceiptVoidingState.Applied && v.PayerCreditBalanceId != null, ct));
}

/// <summary>
/// El ERP reversa un pago del canal: anula el recibo y marca el aviso (<c>Channel.Payments.Reverse</c>, a través de
/// <see cref="Payments.ReverseChannelPaymentCommand"/>). Idempotente por la referencia: repetirlo devuelve lo mismo.
/// </summary>
public sealed record ReverseChannelPaymentNoticeCommand(string Reference, ModoDeReversoDelCanal Mode, string Reason, Guid? PersonBankAccountPublicId)
    : IRequest<Result<ChannelPaymentReversalDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReverseChannelPaymentNoticeCommandValidator : AbstractValidator<ReverseChannelPaymentNoticeCommand>
{
    public ReverseChannelPaymentNoticeCommandValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del reverso.").MaximumLength(300);
    }
}

public sealed class ReverseChannelPaymentNoticeCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IngenIA365ERP.Application.Treasury.Common.ICerrojoDeTesoreria cerrojoDeTesoreria,
    AplicacionDeAnulacion anulaciones,
    IOrdenesDePagoDeTesoreria ordenes,
    EmisionDeRecaudo emision,
    AuditoriaDelCanal auditoria,
    IDateTimeService reloj) : IRequestHandler<ReverseChannelPaymentNoticeCommand, Result<ChannelPaymentReversalDto>>
{
    public const string EventoReversado = "Channel.Payment.Reversed";

    public async Task<Result<ChannelPaymentReversalDto>> Handle(ReverseChannelPaymentNoticeCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ChannelPaymentReversalDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await db.ChannelPaymentNotices.AsNoTracking().FirstOrDefaultAsync(n => n.Reference == request.Reference, ct);
        if (leido is null) return Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.AvisoInexistente());
        if (leido.ReversedAt is not null) return Result.Success(await ReversosDelCanal.DtoAsync(db, leido, ct));
        if (leido.ReceiptId is null && request.Mode == ModoDeReversoDelCanal.Treasury)
            return Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.NoHayRecibo());

        // Cerrojos en su orden: configuración, pagador. Después se relee.
        Domain.Entities.Receipts.Operations.Receipt? recibo = null;
        if (leido.ReceiptId is { } reciboId)
        {
            var pagador = await db.IssuedReceipts.AsNoTracking().Where(r => r.Id == reciboId).Select(r => r.PayerPersonId).FirstAsync(ct);
            await cerrojo.TomarSetupAsync(ct: ct);
            await cerrojo.BloquearPagadoresAsync([pagador], ct);
            // Por Tesorería, la orden de pago toma la configuración de Tesorería: va antes de numerar la anulación (data-model §0.5, Recaudo antes
            // que Tesorería y la numeración al final); la orden la vuelve a pedir y la encuentra tomada.
            if (request.Mode == ModoDeReversoDelCanal.Treasury) await cerrojoDeTesoreria.TomarSetupAsync(ct: ct);
            recibo = await db.IssuedReceipts.FirstAsync(r => r.Id == reciboId, ct);
        }
        var aviso = await db.ChannelPaymentNotices.FirstAsync(n => n.Id == leido.Id, ct);
        if (aviso.ReversedAt is not null) return Result.Success(await ReversosDelCanal.DtoAsync(db, aviso, ct));

        var motivo = request.Reason.Trim();
        var ahora = reloj.UtcNow;
        var hoy = reloj.HoyLocal;
        ReceiptVoiding? documento = null;
        var devoluciones = new List<string>();
        if (recibo is not null)
        {
            if (recibo.Status != ReceiptState.Valid) return Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.ReciboAnuladoORevisado(recibo.Number));
            var sesion = recibo.CashSessionId is { } s ? await db.ReceiptsCashSessions.FirstOrDefaultAsync(x => x.Id == s, ct) : null;
            documento = new ReceiptVoiding
            {
                ReceiptId = recibo.Id, Reason = Recortar($"Reverso del pago en línea {aviso.Reference}: {motivo}", 500), RequestedByUserId = usuario, RequestedAt = ahora,
                InSameSession = false, VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(),
            };
            db.ReceiptVoidings.Add(documento);
            var dinero = request.Mode == ModoDeReversoDelCanal.Gateway ? DineroDeLaAnulacion.DevueltoPorLaPasarela : DineroDeLaAnulacion.SaldoAFavor;
            var aplicada = await anulaciones.AplicarAsync(documento, recibo, sesion, usuario,
                request.Mode == ModoDeReversoDelCanal.Gateway ? "Reverso por la pasarela (canal digital)" : "Devolución por Tesorería (canal digital)", dinero, ct);
            if (aplicada.IsFailure) return Result.Failure<ChannelPaymentReversalDto>(aplicada.Error);

            if (request.Mode == ModoDeReversoDelCanal.Treasury)
            {
                // El saldo que nace de la anulación y el que el pago hubiera dejado a favor: los dos vuelven al asociado.
                var saldos = await db.PayerCreditBalances
                    .Where(x => x.Status == PayerCreditBalanceState.Open && x.Balance > 0m
                        && (x.Id == documento.PayerCreditBalanceId || x.OriginDocumentPublicId == recibo.PublicId))
                    .OrderBy(x => x.Id).ToListAsync(ct);
                foreach (var saldo in saldos)
                {
                    var orden = await RefundPayerCreditBalanceByOrderCommandHandler.DevolverPorOrdenAsync(db, ordenes, emision, saldo, saldo.Balance,
                        request.PersonBankAccountPublicId, "Transfer", Recortar($"Devolución del pago en línea {aviso.Reference}: {motivo}", 200),
                        ReversosDelCanal.ClaveDeLaOrden(aviso.Reference, saldo.PublicId), usuario, hoy, ahora, ct);
                    devoluciones.Add(orden.Number);
                }
            }
        }

        aviso.ReversedAt = ahora;
        aviso.ReversalReason = Recortar(motivo, 300);
        await auditoria.AgregarAsync(EventoReversado, nameof(ChannelPaymentNotice), aviso.PublicId, null, new
        {
            reference = aviso.Reference, mode = request.Mode.ToString(), reason = aviso.ReversalReason, receipt = recibo?.Number, voiding = documento?.Number,
            refundOrders = devoluciones, by = actor.Name,
        }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ReversosDelCanal.DtoAsync(db, aviso, ct));
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}
