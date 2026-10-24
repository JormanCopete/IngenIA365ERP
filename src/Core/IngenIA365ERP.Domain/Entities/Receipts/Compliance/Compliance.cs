using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;

namespace IngenIA365ERP.Domain.Entities.Receipts.Compliance;

/// <summary>
/// <c>RCD_CashTransactions</c> (H, volumen) (feature 015, US14, T0239; data-model §7.1; FR-097, FR-204): el registro propio de transacciones en efectivo de Recaudo, una
/// fila por recibo con efectivo y, al anularlo, otra con signo contrario (el registro nunca se edita). Índice <c>(PersonId, Month)</c> para el acumulado del mes. (nuevo)
/// </summary>
public class CashTransaction : AuditableEntityLong, IHechoInmutable
{
    public Guid DocumentPublicId { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public CashTransactionDocumentKind DocumentKind { get; set; }

    public CashTransactionDirection Direction { get; set; }

    /// <summary>El titular (o el pagador si no hay titular distinto).</summary>
    public int PersonId { get; set; }

    public int PayerPersonId { get; set; }

    public int? DeliveredByPersonId { get; set; }

    public int? ReceivedByPersonId { get; set; }

    /// <summary>El efectivo con su signo: la anulación resta.</summary>
    public decimal CashAmount { get; set; }

    public decimal OtherMeansAmount { get; set; }

    public ReceiptChannel Channel { get; set; }

    public int BranchId { get; set; }

    public DateOnly OccurredOn { get; set; }

    /// <summary>aaaamm.</summary>
    public int Month { get; set; }

    public int? DeclarationId { get; set; }

    public string AlertsJson { get; set; } = "[]";
}

/// <summary>
/// <c>RCD_ComplianceAlertsLog</c> (data-model §7.2; FR-097): cada señal evaluada que levantó una alerta, con su ventana, la cantidad, el total y la versión del parámetro
/// que la fijó. (nuevo)
/// </summary>
public class ComplianceAlertLog : AuditableEntityLong
{
    public int? PersonId { get; set; }

    public string? DepositorKey { get; set; }

    public SenalDeEfectivo Signal { get; set; }

    public DateOnly WindowFrom { get; set; }

    public DateOnly WindowTo { get; set; }

    public int Count { get; set; }

    public decimal Total { get; set; }

    public string? ParameterVersion { get; set; }

    public Guid? AlertPublicId { get; set; }

    public Guid? DocumentPublicId { get; set; }

    public DateTime EvaluatedAt { get; set; }
}
