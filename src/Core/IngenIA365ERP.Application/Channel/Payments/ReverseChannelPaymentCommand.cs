using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Payments;

// Feature 021, US7 (T118; FR-052, FR-053; contracts/erp-administracion.md, «POST /api/channel/payments/{reference}/reverse»): un funcionario con
// Channel.Payments.Reverse reversa un pago del canal con motivo. Antes del corte diario del destino (hora de Colombia, el mismo día de la aprobación) el
// reverso va por la pasarela y el ERP anula el recibo con su asiento espejo; después, como devolución por Tesorería. El canal vuelve a mirar el corte con
// su reloj: si ya pasó, responde Canal.Reverso.FueraDeCorte y el reverso sigue por Tesorería. Si el pago dejó un saldo a favor, va siempre por Tesorería
// (el saldo ya es dinero del asociado en Recaudo y se devuelve con él). El canal no llama de vuelta al ERP: lo completa este comando, con la identidad del
// funcionario; si la llamada se corta después de que la pasarela reversó, repetir el reverso lo termina (el canal responde «ya reversado»).

/// <summary>El reverso de un pago del canal pedido por un funcionario.</summary>
public sealed record ReverseChannelPaymentCommand(string Reference, string Reason, Guid? PersonBankAccountPublicId = null)
    : IRequest<Result<ChannelPaymentReversalDto>>;

public sealed class ReverseChannelPaymentCommandValidator : AbstractValidator<ReverseChannelPaymentCommand>
{
    public ReverseChannelPaymentCommandValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo del reverso.").MaximumLength(300);
    }
}

public sealed class ReverseChannelPaymentCommandHandler(
    IApplicationDbContext db,
    ISender sender,
    ICanalSatelite canal,
    ICurrentTenantService cooperativa,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ReverseChannelPaymentCommand, Result<ChannelPaymentReversalDto>>
{
    public async Task<Result<ChannelPaymentReversalDto>> Handle(ReverseChannelPaymentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ChannelPaymentReversalDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var referencia = request.Reference.Trim().ToUpperInvariant();
        var aviso = await db.ChannelPaymentNotices.AsNoTracking().FirstOrDefaultAsync(n => n.Reference == referencia, ct);
        if (aviso is null) return Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.AvisoInexistente());
        var destino = await db.ChannelPaymentDestinations.AsNoTracking().FirstAsync(d => d.Id == aviso.DestinationId, ct);
        var quien = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == usuario).Select(u => u.PublicId).FirstAsync(ct);
        var corte = destino.ReversalCutoffLocalTime.ToString("HH\\:mm", CultureInfo.InvariantCulture);
        var motivo = request.Reason.Trim();
        Guid.TryParse(cooperativa.TenantId, out var coop);

        // Ya reversado en el ERP: la misma respuesta; si fue por Tesorería y el canal no se enteró, se le vuelve a avisar.
        if (aviso.ReversedAt is not null)
        {
            var hecho = await ReversosDelCanal.DtoAsync(db, aviso, ct);
            if (hecho.Mode == nameof(ModoDeReversoDelCanal.Treasury))
                hecho = hecho with { ChannelUpdated = await MarcarEnElCanalAsync(coop, referencia, quien, aviso.ReversalReason ?? motivo, corte, ct) };
            return Result.Success(hecho);
        }

        var recibo = aviso.ReceiptId is { } id
            ? await db.IssuedReceipts.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.PublicId, r.Number, r.Status }).FirstAsync(ct)
            : null;
        if (recibo is not null && recibo.Status != ReceiptState.Valid)
            return Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.ReciboAnuladoORevisado(recibo.Number));

        // ¿La pasarela ya lo reversó en un intento anterior que se cortó? Entonces se termina por la pasarela, pase lo que pase con el reloj.
        if (canal.Configurado && coop != Guid.Empty)
        {
            var intentos = await canal.BuscarIntentosAsync(coop, null, null, referencia, ct);
            if (intentos.IsSuccess && intentos.Value.FirstOrDefault(i => i.Reference == referencia) is { ReversedAt: not null, ReversalKind: PedidoDeReversoAlCanal.PorLaPasarela })
                return await PorLaPasarelaEnElErpAsync(referencia, motivo, ct);
        }

        var conSaldo = recibo is not null && await db.PayerCreditBalances.AsNoTracking().AnyAsync(s => s.OriginDocumentPublicId == recibo.PublicId, ct);
        if (!conSaldo && DentroDelCorte(aviso.ApprovedAt, destino.ReversalCutoffLocalTime))
        {
            if (!canal.Configurado || coop == Guid.Empty)
                return Result.Failure<ChannelPaymentReversalDto>("Channel.Satellite.NotConfigured",
                    "El canal digital no está configurado en este ambiente: no se puede reversar por la pasarela.");
            var enLaPasarela = await canal.ReversarAsync(coop, referencia, new PedidoDeReversoAlCanal(quien, motivo, corte, PedidoDeReversoAlCanal.PorLaPasarela), ct);
            if (enLaPasarela.IsSuccess)
            {
                if (!enLaPasarela.Value.Reversado)
                    return Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.LaPasarelaNoLoReverso(enLaPasarela.Value.Motivo));
                return await PorLaPasarelaEnElErpAsync(referencia, motivo, ct);
            }
            if (enLaPasarela.Error.Code != ErroresDelReversoDelCanal.FueraDeCorte) return Result.Failure<ChannelPaymentReversalDto>(enLaPasarela.Error);
            // El corte pasó entre la lectura y la llamada: sigue por Tesorería.
        }

        if (recibo is null) return Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.NoHayRecibo());
        var porTesoreria = await sender.Send(new ReverseChannelPaymentNoticeCommand(referencia, ModoDeReversoDelCanal.Treasury, motivo, request.PersonBankAccountPublicId)
        {
            OperationKey = ReversosDelCanal.ClaveDeOperacion(referencia),
        }, ct);
        if (porTesoreria.IsFailure) return porTesoreria;
        return Result.Success(porTesoreria.Value with { ChannelUpdated = await MarcarEnElCanalAsync(coop, referencia, quien, motivo, corte, ct) });
    }

    /// <summary>Antes del corte: el mismo día de la aprobación (hora de Colombia) y antes de la hora del destino.</summary>
    private bool DentroDelCorte(DateTime aprobadoUtc, TimeOnly corte)
    {
        var ahora = reloj.AhoraLocal;
        var aprobado = new DateTimeOffset(DateTime.SpecifyKind(aprobadoUtc, DateTimeKind.Utc)).ToOffset(ahora.Offset);
        return DateOnly.FromDateTime(aprobado.DateTime) == DateOnly.FromDateTime(ahora.DateTime) && TimeOnly.FromDateTime(ahora.DateTime) < corte;
    }

    private async Task<Result<ChannelPaymentReversalDto>> PorLaPasarelaEnElErpAsync(string referencia, string motivo, CancellationToken ct)
    {
        var enElErp = await sender.Send(new ReverseChannelPaymentNoticeCommand(referencia, ModoDeReversoDelCanal.Gateway, motivo, null)
        {
            OperationKey = ReversosDelCanal.ClaveDeOperacion(referencia),
        }, ct);
        return enElErp.IsSuccess ? enElErp : Result.Failure<ChannelPaymentReversalDto>(ErroresDelReversoDelCanal.ReversadoSinAnular(enElErp.Error));
    }

    /// <summary>Marca el intento como devuelto por Tesorería en el canal (FR-053). Si el canal no responde, no deshace nada: repetir el reverso lo reintenta.</summary>
    private async Task<bool> MarcarEnElCanalAsync(Guid coop, string referencia, Guid quien, string motivo, string corte, CancellationToken ct)
    {
        if (!canal.Configurado || coop == Guid.Empty) return false;
        var r = await canal.ReversarAsync(coop, referencia, new PedidoDeReversoAlCanal(quien, motivo, corte, PedidoDeReversoAlCanal.PorTesoreria), ct);
        return r.IsSuccess && r.Value.Reversado;
    }
}
