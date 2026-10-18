using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Consume un <c>ResultadoDelGiro</c> v1 de Tesorería (feature 013, US3, T0449; interfaces.md §4.3; mensajes.md §12.4, §15.2; data-model §7.5).
/// Sin ruta (<c>LosComandosDeConsumoNoTienenRuta</c>): lo manda <see cref="DestinoCartera"/>. Responde siempre un <see cref="ResultadoDeConsumo"/>:
/// la falla del <see cref="Result"/> se reserva para lo que no se pudo leer. (nuevo)
/// </summary>
public sealed record ConsumeTreasuryResultCommand(IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeTreasuryResultCommandValidator : AbstractValidator<ConsumeTreasuryResultCommand>
{
    public ConsumeTreasuryResultCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

/// <summary>
/// La tabla de interfaces.md §4.3, en un <see cref="TransaccionExplicita"/> con el recibo <c>LND_InboundMessages</c> (único por
/// <c>MessagePublicId</c>), su evaluación <c>LND_InboundMessageEvaluations</c>, la <c>LND_DisbursementOrderResults</c> y, si corresponde, el reverso
/// del desembolso desde su origen (<see cref="ReversoDeDesembolso"/>):
/// <list type="bullet">
/// <item><c>Issued → Disbursed</c>: sin efecto en el libro; <c>Issued → Rejected</c> y, con la anulación pedida, <c>Issued → Voided</c>: el reverso;
/// <c>Disbursed</c> (cheque) <c>→ CheckNotCashed</c>: el mismo reverso; con la anulación pedida, <c>Disbursed</c>: la anulación no tuvo efecto
/// (FR-045);</item>
/// <item>lo que no cabe —valor distinto del neto, transición fuera de la tabla, un resultado desconocido— es <c>Rejected</c> con un solo código,
/// <c>Lending.DisbursementOrder.InvalidResult</c>, y el motivo como dato (<c>AmountMismatch</c> con <c>expected</c>/<c>received</c>,
/// <c>InvalidTransition</c> con <c>from</c>/<c>to</c>; C113); queda el recibo <c>Rejected</c> con su evaluación, reprocesable;</item>
/// <item>la orden inexistente es <c>Rejected</c> con <c>Generic.NotFound</c>;</item>
/// <item>un recibo que ya existe y no está <c>Rejected</c> responde <c>AlreadyProcessed</c> sin efecto (SC-010).</item>
/// </list>
/// El actor del reverso es el proceso, con el usuario de Tesorería del mensaje como usuario de origen (T8, T11). (nuevo)
/// </summary>
public sealed class ConsumeTreasuryResultCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ICerrojoDeCartera cerrojo,
    ReversoDeDesembolso reverso,
    IDateTimeService reloj,
    Deposits.Operations.OrdenesDeGiroDeCuentas? ordenesDeCuentas = null) : IRequestHandler<ConsumeTreasuryResultCommand, Result<ResultadoDeConsumo>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeTreasuryResultCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Lending, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (mensaje.Payload is not ResultadoDelGiroV1 resultado || !VersionesAceptadasDeCartera.Acepta(mensaje.Type, mensaje.Version))
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted",
                $"Cartera no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var recibo = await db.LendingInboundMessages.FirstOrDefaultAsync(r => r.MessagePublicId == mensaje.MessageId, ct);
            if (recibo is not null && recibo.Status != InboundMessageStatus.Rejected)
                return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(null, SinComprobante: MotivoSinComprobante.Informational));

            var ahora = reloj.UtcNow;
            recibo ??= NuevoRecibo(mensaje, ahora);
            var orden = await db.DisbursementOrders.FirstOrDefaultAsync(o => o.PublicId == resultado.OrderPublicId && !o.IsDeleted, ct);
            // Feature 014 (US10, T101; mensajes.md §5.3): la orden puede ser de cuentas (LND_AccountPayoutOrders, v2).
            if (orden is null && ordenesDeCuentas is not null
                && await db.AccountPayoutOrders.FirstOrDefaultAsync(o => o.PublicId == resultado.OrderPublicId && !o.IsDeleted, ct) is { } deCuenta)
                return await DeCuentaAsync(recibo, mensaje, resultado, deCuenta, ct);
            if (orden is null)
                return await RechazarAsync(recibo, mensaje, ErroresDeDesembolso.CodigoNoEncontrado, "La orden de giro no existe en Cartera.",
                    new { orderPublicId = resultado.OrderPublicId }, ct);
            recibo.DisbursementOrderId = orden.Id;
            recibo.LoanId = orden.LoanId;

            if (!Enum.TryParse<DisbursementOrderStatus>(resultado.Outcome, ignoreCase: false, out var hacia) || hacia == DisbursementOrderStatus.Issued)
                return await RechazarAsync(recibo, mensaje, ErroresDeDesembolso.CodigoResultadoInvalido,
                    $"«{resultado.Outcome}» no es un resultado del giro: se admiten Disbursed, Rejected, CheckNotCashed y Voided.",
                    new { reason = "InvalidTransition", from = orden.Status.ToString(), to = resultado.Outcome }, ct);
            if (resultado.Amount != orden.NetAmount)
                return await RechazarAsync(recibo, mensaje, ErroresDeDesembolso.CodigoResultadoInvalido,
                    $"Tesorería informó {ErroresDeDesembolso.Pesos(resultado.Amount)} y la orden {orden.Number} es por {ErroresDeDesembolso.Pesos(orden.NetAmount)}.",
                    new { reason = "AmountMismatch", expected = orden.NetAmount, received = resultado.Amount }, ct);
            if (!orden.AdmiteResultado(hacia))
                return await RechazarAsync(recibo, mensaje, ErroresDeDesembolso.CodigoResultadoInvalido,
                    $"La orden {orden.Number} está en «{orden.Status}» y no admite el resultado «{hacia}»: queda para revisión en la bandeja.",
                    new { reason = "InvalidTransition", from = orden.Status.ToString(), to = hacia.ToString() }, ct);

            var deudor = await db.Loans.AsNoTracking().Where(l => l.Id == orden.LoanId).Select(l => l.DebtorPersonId).FirstAsync(ct);
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([deudor], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([orden.LoanId], ModoDeEspera.Esperar, ct);

            if (recibo.Id == 0) db.LendingInboundMessages.Add(recibo);
            recibo.Status = InboundMessageStatus.Processed;
            recibo.LastProcessedAt = ahora;
            recibo.EvaluationCount += 1;
            await db.SaveChangesAsync(ct);

            long? reversoId = null;
            if (DisbursementOrder.Reversa(hacia))
            {
                var origen = mensaje.Envelope.OriginUser;
                var actor = new ActorDelReverso(ActorKind.Process, null, origen.CentralUserId, origen);
                var hecho = await reverso.ReversarAsync(orden, hacia.ToString(), mensaje.MessageId, recibo.Id, resultado.Reason, actor, ct);
                if (hecho.IsFailure) return Result.Failure<ResultadoDeConsumo>(hecho.Error);
                reversoId = hecho.Value.Reverso.Id;
            }
            orden.RegistrarResultado(hacia, ahora);
            db.DisbursementOrderResults.Add(DisbursementOrderResult.Registrar(orden.Id, recibo.Id, hacia, resultado.OccurredOn, resultado.TreasuryReference,
                resultado.Reason, reversoId, ahora));
            var evaluacion = Evaluacion(recibo, mensaje, InboundEvaluationOutcome.Accepted, null, null, false, ahora);
            db.LendingInboundMessageEvaluations.Add(evaluacion);
            await db.SaveChangesAsync(ct);
            recibo.LastEvaluationId = evaluacion.Id;
            await db.SaveChangesAsync(ct);
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
        }, ct);
    }

    /// <summary>
    /// El resultado de una orden de cuentas (feature 014, US10, T101; mensajes.md §5.3): la misma validación de valor y transición que las de desembolso,
    /// el cerrojo de la persona y la cuenta, el recibo y su evaluación, y el efecto de <see cref="Deposits.Operations.OrdenesDeGiroDeCuentas"/> (girada o
    /// reintegrada). No escribe en <c>LND_DisbursementOrderResults</c>, que exige una orden de desembolso.
    /// </summary>
    private async Task<Result<ResultadoDeConsumo>> DeCuentaAsync(LendingInboundMessage recibo, MensajeEntrante mensaje, ResultadoDelGiroV1 resultado,
        Domain.Entities.Lending.Deposits.Accounts.AccountPayoutOrder orden, CancellationToken ct)
    {
        if (!Enum.TryParse<DisbursementOrderStatus>(resultado.Outcome, ignoreCase: false, out var hacia) || hacia == DisbursementOrderStatus.Issued)
            return await RechazarAsync(recibo, mensaje, ErroresDeDesembolso.CodigoResultadoInvalido,
                $"«{resultado.Outcome}» no es un resultado del giro: se admiten Disbursed, Rejected, CheckNotCashed y Voided.",
                new { reason = "InvalidTransition", from = orden.Status.ToString(), to = resultado.Outcome }, ct);
        if (resultado.Amount != orden.Amount)
            return await RechazarAsync(recibo, mensaje, ErroresDeDesembolso.CodigoResultadoInvalido,
                $"Tesorería informó {ErroresDeDesembolso.Pesos(resultado.Amount)} y la orden {orden.Number} es por {ErroresDeDesembolso.Pesos(orden.Amount)}.",
                new { reason = "AmountMismatch", expected = orden.Amount, received = resultado.Amount }, ct);
        if (!orden.AdmiteResultado(hacia))
            return await RechazarAsync(recibo, mensaje, ErroresDeDesembolso.CodigoResultadoInvalido,
                $"La orden {orden.Number} está en «{orden.Status}» y no admite el resultado «{hacia}»: queda para revisión en la bandeja.",
                new { reason = "InvalidTransition", from = orden.Status.ToString(), to = hacia.ToString() }, ct);

        var titular = await db.MemberAccounts.AsNoTracking().Where(a => a.Id == orden.MemberAccountId).Select(a => a.HolderPersonId).FirstAsync(ct);
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync([titular], ModoDeEspera.Esperar, ct);
        var cuentas = await db.AccountPayoutOrders.AsNoTracking()
            .Where(o => o.Id == orden.Id || (orden.MembershipWithdrawalId != null && o.MembershipWithdrawalId == orden.MembershipWithdrawalId))
            .Select(o => o.MemberAccountId).Distinct().ToListAsync(ct);
        if (orden.MembershipWithdrawalId is { } retiroId)
            cuentas.AddRange(await db.MembershipWithdrawals.AsNoTracking().Where(w => w.Id == retiroId).Select(w => w.ContributionAccountId).ToListAsync(ct));
        await cerrojo.BloquearCuentasAsync(cuentas.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);

        var ahora = reloj.UtcNow;
        if (recibo.Id == 0) db.LendingInboundMessages.Add(recibo);
        recibo.Status = InboundMessageStatus.Processed;
        recibo.LastProcessedAt = ahora;
        recibo.EvaluationCount += 1;
        await db.SaveChangesAsync(ct);

        var efecto = await ordenesDeCuentas!.AplicarResultadoAsync(orden, hacia, resultado.TreasuryReference, resultado.OccurredOn, resultado.Reason, ct);
        if (efecto.IsFailure) return Result.Failure<ResultadoDeConsumo>(efecto.Error);
        var evaluacion = Evaluacion(recibo, mensaje, InboundEvaluationOutcome.Accepted, null, null, false, ahora);
        db.LendingInboundMessageEvaluations.Add(evaluacion);
        await db.SaveChangesAsync(ct);
        recibo.LastEvaluationId = evaluacion.Id;
        await db.SaveChangesAsync(ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
    }

    /// <summary>
    /// Deja el recibo <c>Rejected</c> (reprocesable) con su evaluación negativa y responde <c>Rejected</c> con el código y el motivo como dato. Se
    /// guarda: la transacción confirma porque el resultado del comando es un éxito.
    /// </summary>
    private async Task<Result<ResultadoDeConsumo>> RechazarAsync(LendingInboundMessage recibo, MensajeEntrante mensaje, string codigo, string motivo, object datos,
        CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        if (recibo.Id == 0) db.LendingInboundMessages.Add(recibo);
        recibo.Status = InboundMessageStatus.Rejected;
        recibo.LastProcessedAt = ahora;
        recibo.EvaluationCount += 1;
        await db.SaveChangesAsync(ct);
        var json = JsonSerializer.Serialize(datos, Json);
        var evaluacion = Evaluacion(recibo, mensaje, InboundEvaluationOutcome.NotConvertible, codigo, motivo, true, ahora, json);
        db.LendingInboundMessageEvaluations.Add(evaluacion);
        await db.SaveChangesAsync(ct);
        recibo.LastEvaluationId = evaluacion.Id;
        await db.SaveChangesAsync(ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(codigo, motivo, json));
    }

    private static LendingInboundMessage NuevoRecibo(MensajeEntrante m, DateTime ahora) => new()
    {
        MessagePublicId = m.MessageId,
        MessageType = m.Type,
        MessageVersion = (short)m.Version,
        OriginModule = m.OriginModule,
        OriginPublicId = m.Envelope.Origin.PublicId,
        OriginNumber = m.Envelope.Origin.Number,
        OriginEventKey = m.Envelope.OriginEventKey,
        RelatedPublicId = m.Envelope.Related?.PublicId,
        ChainRootPublicId = m.Envelope.ChainRootPublicId == Guid.Empty ? m.Envelope.Origin.PublicId : m.Envelope.ChainRootPublicId,
        OperationDate = m.Envelope.Origin.OperationDate,
        Status = InboundMessageStatus.Rejected,
        FirstReceivedAt = ahora,
        LastProcessedAt = ahora,
    };

    private static LendingInboundMessageEvaluation Evaluacion(LendingInboundMessage recibo, MensajeEntrante m, InboundEvaluationOutcome resultado, string? codigo,
        string? motivo, bool reprocesable, DateTime ahora, string? datos = null) => new()
    {
        InboundMessageId = recibo.Id,
        EvaluationNumber = recibo.EvaluationCount,
        AsOfDate = m.Envelope.Origin.OperationDate,
        EvaluatedAt = ahora,
        Outcome = resultado,
        ChecksJson = datos is null ? "[]" : $"[{{\"check\":\"TreasuryResult\",\"evidence\":{datos}}}]",
        FailureCode = codigo,
        FailureReason = motivo,
        Retryable = reprocesable,
        PrevalidationOutcome = null,
    };
}

/// <summary>
/// Las versiones que acepta el destino <c>Lending</c> (mensajes.md §15.1; C16): <b>toda</b> versión de <c>VentaACreditoRegistrada</c> y
/// <c>AjusteDeVentaACredito</c> —así una versión desconocida no se rechaza en el despachador sin respuesta: el consumidor deja su evaluación y la
/// responde como no convertible— (US6, T0586), <c>ResultadoDelGiro</c> v1 (US3) y, desde K2, <c>EnvioDeDescuentosConfirmado</c> v1 y
/// <c>ResultadoDeDescuentos</c> v1 (T0838). Lo demás, <c>Rejected</c> con <c>Integration.VersionNotAccepted</c>
/// en el despachador. (nuevo)
/// </summary>
public static class VersionesAceptadasDeCartera
{
    private static readonly HashSet<(string Type, int Version)> Aceptadas =
    [
        (ResultadoDelGiroV1.Type, 1),
        // K2 (T0838): lo que Recaudo devuelve del archivo de descuentos (la fila CAR del registro los adopta desde CarteraLibranza).
        (EnvioDeDescuentosConfirmadoV1.Type, 1),
        (ResultadoDeDescuentosV1.Type, 1),
    ];

    /// <summary>Las familias de Comercial: se aceptan en toda versión (C16).</summary>
    private static readonly HashSet<string> FamiliasDeComercial =
    [
        IngenIA365ERP.Application.Common.Integration.Contracts.Inventory.VentaACreditoRegistradaV1.Type,
        IngenIA365ERP.Application.Common.Integration.Contracts.Inventory.AjusteDeVentaACreditoV1.Type,
    ];

    public static bool Acepta(string type, int version) => Aceptadas.Contains((type, version)) || (version >= 1 && FamiliasDeComercial.Contains(type));

    /// <summary>¿Es un mensaje de la familia de Comercial (lo consume <c>ConsumeCommercialCreditMessageCommand</c>)?</summary>
    public static bool EsDeComercial(string type) => FamiliasDeComercial.Contains(type);
}
