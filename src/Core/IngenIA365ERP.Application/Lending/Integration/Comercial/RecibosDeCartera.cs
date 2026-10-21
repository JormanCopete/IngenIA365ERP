using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Comercial;

/// <summary>Lo que se anota en una evaluación de un mensaje recibido (data-model §21.2). (nuevo)</summary>
public sealed record EvaluacionAAnotar(
    InboundEvaluationOutcome Resultado,
    string? Linea,
    string ChecksJson,
    string? Codigo,
    string? Motivo,
    bool Reprocesable,
    string? ValidacionPrevia,
    Guid? Respuesta = null,
    Guid? PublicId = null);

/// <summary>
/// El recibo <c>LND_InboundMessages</c> y sus evaluaciones (feature 013, US6, T0587; data-model §21.1, §21.2; T17): lo comparten los consumidores de
/// Cartera para que el recibo sea único por mensaje y cada evaluación sea un hecho de sólo inserción. Guarda en la transacción de quien llama (los
/// Id de recibo y evaluación); nunca confirma. (nuevo)
/// </summary>
public sealed class RecibosDeCartera(IApplicationDbContext db, IDateTimeService reloj)
{
    /// <summary>El recibo de un mensaje, si existe (con seguimiento).</summary>
    public Task<LendingInboundMessage?> DeAsync(Guid messagePublicId, CancellationToken ct) =>
        db.LendingInboundMessages.FirstOrDefaultAsync(r => r.MessagePublicId == messagePublicId, ct);

    /// <summary>Un recibo nuevo (aún sin guardar) con lo del sobre; nace <c>Rejected</c> y lo cambia el desenlace.</summary>
    public LendingInboundMessage Nuevo(MensajeEntrante m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var ahora = reloj.UtcNow;
        var recibo = new LendingInboundMessage
        {
            MessagePublicId = m.MessageId,
            MessageType = m.Type,
            MessageVersion = (short)m.Version,
            OriginModule = m.OriginModule,
            OriginPublicId = m.Envelope.Origin.PublicId,
            OriginNumber = Recortar(m.Envelope.Origin.Number, 30),
            OriginEventKey = m.Envelope.OriginEventKey,
            RelatedPublicId = m.Envelope.Related?.PublicId,
            ChainRootPublicId = m.Envelope.ChainRootPublicId == Guid.Empty ? m.Envelope.Origin.PublicId : m.Envelope.ChainRootPublicId,
            OperationDate = m.Envelope.Origin.OperationDate,
            Status = InboundMessageStatus.Rejected,
            FirstReceivedAt = ahora,
            LastProcessedAt = ahora,
        };
        db.LendingInboundMessages.Add(recibo);
        return recibo;
    }

    /// <summary>Guarda el recibo si todavía no tiene Id (las operaciones que nacen del consumo lo referencian).</summary>
    public async Task AsegurarAsync(LendingInboundMessage recibo, CancellationToken ct)
    {
        if (recibo.Id == 0) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Anota una evaluación (sólo inserción) con el número siguiente, la deja como última del recibo y marca el instante del proceso. Guarda (dos
    /// veces: la evaluación necesita el Id del recibo y el recibo el de la evaluación).
    /// </summary>
    public async Task<LendingInboundMessageEvaluation> AnotarAsync(LendingInboundMessage recibo, MensajeEntrante m, EvaluacionAAnotar e, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recibo);
        ArgumentNullException.ThrowIfNull(e);
        await AsegurarAsync(recibo, ct);
        var ahora = reloj.UtcNow;
        recibo.EvaluationCount += 1;
        recibo.LastProcessedAt = ahora;
        var evaluacion = new LendingInboundMessageEvaluation
        {
            InboundMessageId = recibo.Id,
            EvaluationNumber = recibo.EvaluationCount,
            AsOfDate = m.Envelope.Origin.OperationDate,
            EvaluatedAt = ahora,
            Outcome = e.Resultado,
            CreditLineCode = e.Linea is null ? null : Recortar(e.Linea, 10),
            ChecksJson = e.ChecksJson,
            FailureCode = e.Codigo is null ? null : Recortar(e.Codigo, 80),
            FailureReason = e.Motivo is null ? null : Recortar(e.Motivo, 500),
            Retryable = e.Reprocesable,
            PrevalidationOutcome = e.ValidacionPrevia,
            ResponseMessagePublicId = e.Respuesta,
        };
        if (e.PublicId is { } id) evaluacion.PublicId = id;
        db.LendingInboundMessageEvaluations.Add(evaluacion);
        await db.SaveChangesAsync(ct);
        recibo.LastEvaluationId = evaluacion.Id;
        await db.SaveChangesAsync(ct);
        return evaluacion;
    }

    /// <summary>La activación vigente de Cartera (la del <c>LND_Setup</c>), con el corte de su carga; nula si no hay (sin migración: no hay corte).</summary>
    public async Task<LendingActivation?> ActivacionAsync(CancellationToken ct)
    {
        var id = await db.LendingSetups.AsNoTracking().Where(s => s.Id == LendingSetup.IdDeLaFilaUnica).Select(s => s.ActivationId).FirstOrDefaultAsync(ct);
        return id is { } a ? await db.LendingActivations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a, ct) : null;
    }

    public static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}
