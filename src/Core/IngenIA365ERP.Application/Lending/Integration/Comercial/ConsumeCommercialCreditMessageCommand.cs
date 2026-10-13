using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>
/// Consume un mensaje de la familia de Comercial —<c>VentaACreditoRegistrada</c> (v1, v2) o <c>AjusteDeVentaACredito</c> (v1)— (feature 013, US6, T0587;
/// DT §2.10; T17; mensajes.md §14.4, §15). Sin ruta (<c>LosComandosDeConsumoNoTienenRuta</c>): lo manda <see cref="DestinoCartera"/>, en el molde de
/// <c>PostInventoryMessagesCommand</c>, y es <see cref="IReintentableAnteConcurrencia"/>. Responde siempre un <see cref="ResultadoDeConsumo"/>; la falla
/// del <see cref="Result"/> queda para lo que no se pudo leer. (nuevo)
/// </summary>
public sealed record ConsumeCommercialCreditMessageCommand(IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeCommercialCreditMessageCommandValidator : AbstractValidator<ConsumeCommercialCreditMessageCommand>
{
    public ConsumeCommercialCreditMessageCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

/// <summary>
/// En un <see cref="TransaccionExplicita"/> y en <b>un</b> guardado lógico (data-model §21; mensajes.md §15.2): el recibo <c>LND_InboundMessages</c> (único
/// por <c>MessagePublicId</c>), una <c>LND_InboundMessageEvaluations</c> por evaluación, el efecto y los mensajes que emite. Los pasos de §15.3:
/// <list type="number">
/// <item>el recibo: si existe y no está <c>Rejected</c>, <c>AlreadyProcessed</c> con la misma referencia y sin emitir nada; uno marcado no
/// reprocesable responde su rechazo sin evaluar otra vez; uno <c>Rejected</c> reprocesable se vuelve a evaluar (suma una evaluación);</item>
/// <item>la versión: desconocida → evaluación y <c>Rejected</c> (<c>Integration.VersionNotAccepted</c>), reprocesable y sin
/// <c>ObligacionRechazada</c> (C16, C95);</item>
/// <item>hasta el corte de la carga activada → <see cref="VentasAnterioresAlCorte"/> (FR-143);</item>
/// <item>una venta en vivo → <see cref="ConsumoDeVentaACredito"/>; un ajuste → <see cref="AjusteDesdeComercial"/>.</item>
/// </list>
/// La colisión del índice único del recibo por concurrencia se traduce a <c>AlreadyProcessed</c> (la otra réplica ya lo consumió). (nuevo)
/// </summary>
public sealed class ConsumeCommercialCreditMessageCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    RecibosDeCartera recibos,
    ConsumoDeVentaACredito ventas,
    AjusteDesdeComercial ajustes,
    VentasAnterioresAlCorte anteriores) : IRequestHandler<ConsumeCommercialCreditMessageCommand, Result<ResultadoDeConsumo>>
{
    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeCommercialCreditMessageCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Lending, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];

        try
        {
            return await TransaccionExplicita.EjecutarAsync(db, () => ConsumirAsync(mensaje, ct), ct);
        }
        catch (DbUpdateException)
        {
            // Dos réplicas con el mismo mensaje: la otra ya escribió el recibo (UK MessagePublicId). Si no fue eso, sube.
            if (await YaConsumidoAsync(mensaje.MessageId, ct) is { } yaConsumido) return Result.Success(yaConsumido);
            throw;
        }
    }

    private async Task<Result<ResultadoDeConsumo>> ConsumirAsync(MensajeEntrante m, CancellationToken ct)
    {
        var recibo = await recibos.DeAsync(m.MessageId, ct);
        if (recibo is not null && recibo.Status != InboundMessageStatus.Rejected)
            return Result.Success(await YaProcesadoAsync(recibo, ct));
        if (recibo is { NotRetryableAt: not null })
        {
            var ultima = recibo.LastEvaluationId is { } id ? await db.LendingInboundMessageEvaluations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct) : null;
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(ultima?.FailureCode ?? ErroresDeVentasACredito.CodigoNoMarcable,
                $"El mensaje se marcó no reprocesable: {recibo.NotRetryableReason}"));
        }
        recibo ??= recibos.Nuevo(m);

        // La versión: toda versión de la familia llega al consumidor (C16); la que no conoce queda reprocesable, sin respuesta (C95).
        object? contenido = m.Type switch
        {
            VentaACreditoRegistradaV1.Type => VentaRecibida.De(m),
            AjusteDeVentaACreditoV1.Type when m.Version == 1 => m.Payload as AjusteDeVentaACreditoV1,
            _ => null,
        };
        if (contenido is null)
            return Result.Success(await ventas.NoConvertibleAsync(m, recibo, ErroresDeVentasACredito.VersionNoAceptada(m.Type, m.Version), null, ct));

        var activacion = await recibos.ActivacionAsync(ct);
        var fecha = m.Envelope.Origin.OperationDate;
        return contenido switch
        {
            VentaRecibida venta when VentasAnterioresAlCorte.HastaElCorte(activacion, fecha) =>
                Result.Success(await anteriores.VentaAsync(m, venta, recibo, activacion!, ct)),
            VentaRecibida venta => Result.Success(await ventas.CrearAsync(m, venta, recibo, null, ct)),
            AjusteDeVentaACreditoV1 ajuste => Result.Success(await ajustes.ConsumirAsync(m, ajuste, recibo, activacion, ct)),
            _ => Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(ErroresDeVentasACredito.CodigoVersionNoAceptada,
                $"Cartera no acepta {m.Type} v{m.Version}.")),
        };
    }

    /// <summary>El recibo ya procesado: la misma referencia que la primera vez (SC-010), sin un segundo efecto.</summary>
    private async Task<ResultadoDeConsumo> YaProcesadoAsync(LendingInboundMessage recibo, CancellationToken ct)
    {
        if (recibo.Status == InboundMessageStatus.NotIncorporated)
            return new ResultadoDeConsumo.AlreadyProcessed(null, SinComprobante: MotivoSinComprobante.NotIncorporated);
        if (recibo.LoanId is { } loanId)
        {
            var credito = await db.Loans.AsNoTracking().Where(l => l.Id == loanId).Select(l => new { l.PublicId, l.Number }).FirstOrDefaultAsync(ct);
            if (credito is not null) return new ResultadoDeConsumo.AlreadyProcessed(credito.PublicId, null, credito.Number);
        }
        return new ResultadoDeConsumo.AlreadyProcessed(null, SinComprobante: MotivoSinComprobante.Informational);
    }

    private async Task<ResultadoDeConsumo?> YaConsumidoAsync(Guid messageId, CancellationToken ct)
    {
        db.DescartarCambios();
        var recibo = await db.LendingInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == messageId, ct);
        return recibo is null || recibo.Status == InboundMessageStatus.Rejected ? null : await YaProcesadoAsync(recibo, ct);
    }
}
