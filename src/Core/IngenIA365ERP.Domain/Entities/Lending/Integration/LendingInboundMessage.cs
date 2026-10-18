using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Integration;

/// <summary>
/// <c>LND_InboundMessages</c> (feature 013, C.3; data-model §21.1; FR-009, FR-050–FR-055, FR-143; T17, PM-09): el recibo único de
/// cada mensaje que la bandeja entrega a Cartera (venta a crédito, ajuste, resultado del giro). <b>No es hecho</b>: un
/// <c>Rejected</c> se reprocesa sobre la misma fila y un <c>ValidationFailed</c> puede pasar a <c>ConfirmedByException</c>. Cada
/// evaluación queda en <see cref="LendingInboundMessageEvaluation"/>. Forma sin comportamiento. La columna
/// <c>PayrollDeductionFileId</c> nace con K2 (<c>CarteraLibranza</c>).
/// </summary>
public class LendingInboundMessage : AuditableEntity
{
    /// <summary>El mensaje de la bandeja (sin FK). Único sin filtro: si ya existe y no está <c>Rejected</c>, <c>AlreadyProcessed</c>.</summary>
    public Guid MessagePublicId { get; set; }

    public string MessageType { get; set; } = string.Empty;

    public short MessageVersion { get; set; }

    /// <summary><c>INV</c>, <c>TES</c> o <c>REC</c>, del sobre.</summary>
    public string OriginModule { get; set; } = string.Empty;

    public Guid OriginPublicId { get; set; }

    public string? OriginNumber { get; set; }

    public string OriginEventKey { get; set; } = string.Empty;

    /// <summary>En un ajuste, la venta ajustada.</summary>
    public Guid? RelatedPublicId { get; set; }

    /// <summary>La venta raíz de la cadena de Comercial.</summary>
    public Guid ChainRootPublicId { get; set; }

    /// <summary>La fecha del sobre; en una venta, la fecha de la venta (FR-050, FR-051).</summary>
    public DateOnly OperationDate { get; set; }

    /// <summary>Tercero resuelto desde el <c>PersonPublicId</c> del sobre; nulo = persona inexistente (<c>Rejected</c>).</summary>
    public int? PersonId { get; set; }

    /// <summary>Venta: el pago de crédito (una obligación por pago); ajuste: el pago original.</summary>
    public Guid? CreditPaymentPublicId { get; set; }

    public Guid? ReservationPublicId { get; set; }

    public string? ReservedLoanNumber { get; set; }

    public InboundMessageStatus Status { get; set; }

    /// <summary>Obligación creada o afectada (venta, ajuste).</summary>
    public int? LoanId { get; set; }

    /// <summary>La orden de giro de un <c>ResultadoDelGiro</c>.</summary>
    public int? DisbursementOrderId { get; set; }

    /// <summary>Venta o ajuste fechado hasta el corte (FR-143).</summary>
    public int? SaleReconciliationId { get; set; }

    /// <summary>K2 (<c>CarteraLibranza</c>): el archivo de descuentos de un <c>EnvioDeDescuentosConfirmado</c> o un <c>ResultadoDeDescuentos</c>.</summary>
    public int? PayrollDeductionFileId { get; set; }

    public int EvaluationCount { get; set; }

    public int? LastEvaluationId { get; set; }

    /// <summary>Última <c>ObligacionCreada</c> u <c>ObligacionRechazada</c> emitida; nulo en un <c>Rejected</c> reprocesable (C95).</summary>
    public Guid? ResponseMessagePublicId { get; set; }

    /// <summary>Sólo en un <c>Rejected</c>: cuándo dejó de ser reprocesable (C95). Nulo = sigue reprocesable.</summary>
    public DateTime? NotRetryableAt { get; set; }

    /// <summary>Quién lo marcó no reprocesable; nulo si lo cerró el consumidor al fallar un reproceso de forma definitiva.</summary>
    public int? NotRetryableByUserId { get; set; }

    public string? NotRetryableReason { get; set; }

    public DateTime FirstReceivedAt { get; set; }

    public DateTime LastProcessedAt { get; set; }

    /// <summary>Decisión por atribución que confirma por excepción (T63); nunca en los casos no exceptuables.</summary>
    public int? ExceptionDecisionId { get; set; }

    /// <summary>Obligatorio junto con <see cref="ExceptionDecisionId"/>.</summary>
    public string? ExceptionReason { get; set; }

    public DateTime? ConfirmedByExceptionAt { get; set; }

    public int? ConfirmedByExceptionByUserId { get; set; }
}
