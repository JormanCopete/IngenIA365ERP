using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// Una línea del arqueo por medio de pago (<c>INV_CashCountLines</c>; feature 012, I3, T574; FR-099; data-model §15). Único
/// <c>(CashCountId, PaymentMeansId)</c>. Diferencia = contado − esperado; en <c>CashCountMethod.None</c> (créditos) contado =
/// esperado. La tolerancia es la <b>copia</b> de la del medio al cerrar. Motivo obligatorio con diferencia ≠ 0; el sobrante es
/// siempre <c>Surplus</c> y el faltante el tratamiento sellado en la sesión (<c>EvaluadorDeArqueo</c>). Inmutable junto con su
/// arqueo (<see cref="Status"/>, copia del de <see cref="CashCount"/>).
/// </summary>
public class CashCountLine : AuditableEntity, IInmutableTrasConfirmar
{
    public int CashCountId { get; set; }

    public CashCount? CashCount { get; set; }

    public int PaymentMeansId { get; set; }

    /// <summary>Copia del medio.</summary>
    public CashCountMethod CountMethod { get; set; }

    public decimal ExpectedAmount { get; set; }

    public decimal CountedAmount { get; set; }

    public decimal DifferenceAmount { get; set; }

    /// <summary>Pagos de la sesión con ese medio.</summary>
    public int PaymentCount { get; set; }

    /// <summary>Copia de <c>COR_PaymentMeans.ToleranceAmount</c>.</summary>
    public decimal ToleranceAmount { get; set; }

    public bool WithinTolerance { get; set; }

    public string? Reason { get; set; }

    public CashDifferenceTreatment? Treatment { get; set; }

    /// <summary>Copia del estado de su arqueo. (nuevo)</summary>
    public DocumentStatus Status { get; private set; } = DocumentStatus.Draft;

    public ICollection<CashCountDenomination> Denominations { get; set; } = new List<CashCountDenomination>();

    public ICollection<CashCountTerminalBatch> TerminalBatches { get; set; } = new List<CashCountTerminalBatch>();

    public ICollection<CashCountReferenceCheck> ReferenceChecks { get; set; } = new List<CashCountReferenceCheck>();

    internal void Fijar() => Status = DocumentStatus.Confirmed;
}
