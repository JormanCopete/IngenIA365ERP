using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementOrderResults</c> (feature 013, C44; data-model §7.5; FR-045, FR-048, D27): cada resultado que Tesorería informa
/// de una orden de giro (puede haber varios: girada y luego cheque no cobrado). Hecho de sólo inserción (<see cref="IHechoInmutable"/>,
/// C42); sólo se registran las transiciones de §8.4.
/// </summary>
public class DisbursementOrderResult : AuditableEntity, IHechoInmutable
{
    public int OrderId { get; init; }

    /// <summary>El <c>ResultadoDelGiro</c> consumido; único.</summary>
    public int InboundMessageId { get; init; }

    /// <summary><c>Disbursed</c>, <c>Rejected</c>, <c>CheckNotCashed</c> o <c>Voided</c>.</summary>
    public DisbursementOrderStatus Result { get; init; }

    public DateOnly ResultDate { get; init; }

    public string? TreasuryReference { get; init; }

    public string? Reason { get; init; }

    /// <summary>El reverso del desembolso que causó (<c>LND_LoanOperations</c>, bigint).</summary>
    public long? ReversalOperationId { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>El resultado de Tesorería que se registra (sólo las transiciones de interfaces.md §4.3; T0433).</summary>
    public static DisbursementOrderResult Registrar(int ordenId, int recibo, DisbursementOrderStatus resultado, DateOnly fecha, string? referencia,
        string? motivo, long? reverso, DateTime instante) => new()
        {
            OrderId = ordenId,
            InboundMessageId = recibo,
            Result = resultado,
            ResultDate = fecha,
            TreasuryReference = string.IsNullOrWhiteSpace(referencia) ? null : referencia.Trim(),
            Reason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            ReversalOperationId = reverso,
            RegisteredAt = instante,
        };
}
