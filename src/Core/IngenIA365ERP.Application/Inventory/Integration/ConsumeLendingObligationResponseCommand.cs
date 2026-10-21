using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Guarda en Comercial una respuesta de Cartera a la venta a crédito —<c>ObligacionCreada</c> v1 u <c>ObligacionRechazada</c> v1— (feature 013, US6,
/// T0598; mensajes.md §11.1, §11.2; data-model parte 3 §8.3). Sin ruta (<c>LosComandosDeConsumoNoTienenRuta</c>): lo manda <see cref="DestinoInventario"/>,
/// y es <see cref="IReintentableAnteConcurrencia"/>. Responde siempre un <see cref="ResultadoDeConsumo"/>; la falla del <see cref="Result"/> queda para lo
/// que no se pudo leer. (nuevo)
/// </summary>
public sealed record ConsumeLendingObligationResponseCommand(IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeLendingObligationResponseCommandValidator : AbstractValidator<ConsumeLendingObligationResponseCommand>
{
    public ConsumeLendingObligationResponseCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

/// <summary>
/// Una fila de <c>INV_LendingObligationResponses</c> por mensaje, en un solo <c>SaveChanges</c>: el recibo es la fila misma (único
/// <c>MessagePublicId</c>), así que un mensaje ya guardado responde <c>AlreadyProcessed</c> con la misma referencia y la colisión del índice por
/// concurrencia, también. El pago se resuelve por <c>creditPaymentPublicId</c>; si no existe en Comercial, <c>Rejected</c>
/// (<c>Inventory.Credit.PaymentNotFound</c>). Guarda número, estado, código y motivo; <b>nunca</b> saldos ni cuotas: del plan no lee nada.
/// <list type="bullet">
/// <item><c>ObligacionCreada</c> → <c>Created</c> con la obligación, su número y el estado (<c>Validated</c>, <c>ValidationFailed</c> con el código y
/// el motivo de la validación, <c>Migrated</c> o <c>CancelledBeforeCutoff</c>);</item>
/// <item><c>ObligacionRechazada</c> → <c>Rejected</c> con el motivo (<c>ReasonCode</c>) y el texto; <c>NoConvertible</c> no tiene estado de
/// obligación; los demás motivos (aceptada sin migrar, fuera de Cartera, sin obligación) son <c>NotIncorporated</c>.</item>
/// </list>
/// </summary>
public sealed class ConsumeLendingObligationResponseCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    IDateTimeService reloj) : IRequestHandler<ConsumeLendingObligationResponseCommand, Result<ResultadoDeConsumo>>
{
    /// <summary>El pago de crédito que nombra la respuesta no existe en Comercial.</summary>
    public const string CodigoPagoInexistente = "Inventory.Credit.PaymentNotFound";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeLendingObligationResponseCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Inventory, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);

        Guid? referencia = null;
        var nuevas = new List<LendingObligationResponse>();
        foreach (var m in leidos.Value)
        {
            if (await YaGuardadaAsync(m.MessageId, ct) is { } ya)
            {
                referencia ??= ya;
                continue;
            }
            var fila = await FilaAsync(m, ct);
            if (fila.IsFailure)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(fila.Error.Code, fila.Error.Message));
            nuevas.Add(fila.Value);
        }

        if (nuevas.Count == 0)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(referencia, SinComprobante: MotivoSinComprobante.Informational));

        db.LendingObligationResponses.AddRange(nuevas);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dos réplicas con la misma respuesta: la otra ya guardó la fila (UK MessagePublicId). Si no fue eso, sube.
            db.DescartarCambios();
            if (await YaGuardadaAsync(nuevas[0].MessagePublicId, ct) is { } otra)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(otra, SinComprobante: MotivoSinComprobante.Informational));
            throw;
        }
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(nuevas[0].PublicId, null, null, MotivoSinComprobante.Informational));
    }

    private async Task<Guid?> YaGuardadaAsync(Guid mensaje, CancellationToken ct) =>
        await db.LendingObligationResponses.AsNoTracking().Where(r => r.MessagePublicId == mensaje).Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct);

    private async Task<Result<LendingObligationResponse>> FilaAsync(MensajeEntrante m, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        switch (m.Payload)
        {
            case ObligacionCreadaV1 creada when m.Version == 1:
            {
                var pago = await PagoAsync(creada.CreditPaymentPublicId, ct);
                if (pago is null) return PagoInexistente(creada.CreditPaymentPublicId);
                return Result.Success(new LendingObligationResponse
                {
                    DocumentPaymentId = pago.Value,
                    MessagePublicId = m.MessageId,
                    MessageType = ObligacionCreadaV1.Type,
                    MessageVersion = (short)m.Version,
                    OriginalMessagePublicId = creada.SaleMessage.MessageId,
                    Outcome = LendingObligationOutcome.Created,
                    ObligationPublicId = creada.Loan.LoanPublicId,
                    ObligationNumber = creada.Loan.LoanNumber,
                    ValidationStatus = Enum.TryParse<LendingObligationState>(creada.Status, ignoreCase: false, out var estado) ? estado : null,
                    ReasonCode = creada.Validation?.Code,
                    Reason = Recortar(creada.Validation?.Reason),
                    Retryable = false,
                    ReceivedAt = ahora,
                });
            }
            case ObligacionRechazadaV1 rechazada when m.Version == 1:
            {
                var pago = await PagoAsync(rechazada.CreditPaymentPublicId, ct);
                if (pago is null) return PagoInexistente(rechazada.CreditPaymentPublicId);
                return Result.Success(new LendingObligationResponse
                {
                    DocumentPaymentId = pago.Value,
                    MessagePublicId = m.MessageId,
                    MessageType = ObligacionRechazadaV1.Type,
                    MessageVersion = (short)m.Version,
                    OriginalMessagePublicId = rechazada.SaleMessage.MessageId,
                    Outcome = LendingObligationOutcome.Rejected,
                    ValidationStatus = EstadoDelRechazo(rechazada.Motive),
                    ReasonCode = string.IsNullOrWhiteSpace(rechazada.Motive) ? rechazada.Code : rechazada.Motive,
                    Reason = Recortar(rechazada.Reason),
                    Retryable = rechazada.Retryable,
                    ReceivedAt = ahora,
                });
            }
            default:
                return Result.Failure<LendingObligationResponse>(new Error("Integration.VersionNotAccepted",
                    $"Comercial no acepta {m.Type} v{m.Version}."));
        }
    }

    /// <summary><c>NoConvertible</c> no deja obligación ni estado; lo demás (aceptada sin migrar, fuera de Cartera, sin obligación) no se incorporó.</summary>
    public static LendingObligationState? EstadoDelRechazo(string? motivo) =>
        string.Equals(motivo, "NoConvertible", StringComparison.Ordinal) ? null : LendingObligationState.NotIncorporated;

    private async Task<int?> PagoAsync(Guid pago, CancellationToken ct) =>
        await db.DocumentPayments.AsNoTracking().Where(p => p.PublicId == pago).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);

    private static Result<LendingObligationResponse> PagoInexistente(Guid pago) =>
        Result.Failure<LendingObligationResponse>(new Error(CodigoPagoInexistente,
            $"Cartera respondió por el pago {pago} y Comercial no lo tiene: revisá la venta en la bandeja de mensajes."));

    private static string? Recortar(string? texto) => texto is null ? null : texto.Length <= 500 ? texto : texto[..500];
}
