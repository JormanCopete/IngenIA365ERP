using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;

// Feature 015, T2 (fase 44, T0464; FR-133, FR-218; data-model §13.2): la conciliación de una red de tarjetas (adquirente): lo cobrado por lote de datáfono en Recaudo y en el
// punto de venta contra lo que la red liquida y abona, con la comisión, el IVA y las retenciones desagregados como hecho y la diferencia sobre la tolerancia con aprobación.

/// <summary>
/// <c>TES_AcquirerSettlements</c>: una liquidación de la red (su archivo, ámbito <c>AcquirerSettlement = 11</c>) abonada en una cuenta propia, con sus totales, lo cobrado
/// que le corresponde, la diferencia y su estado. Explicada (dentro de la tolerancia) o aprobada, nace <c>ConciliacionDeAdquirenteRegistrada</c> y queda como registro de
/// los libros para cruzar el abono del extracto. (nuevo)
/// </summary>
public class AcquirerSettlement : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    /// <summary>La red (<c>COR_CardAcquirers</c>); su persona es el tercero de la comisión y las retenciones.</summary>
    public int CardAcquirerId { get; set; }

    public int BankAccountId { get; set; }

    public int BankFileFormatId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileSha256 { get; set; } = string.Empty;

    /// <summary>El día del abono.</summary>
    public DateOnly SettlementDate { get; set; }

    /// <summary>La línea del extracto del abono, si se indicó.</summary>
    public Guid? StatementLinePublicId { get; set; }

    public decimal? StatementAmount { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal CommissionVatAmount { get; set; }

    public decimal WithholdingTaxAmount { get; set; }

    public decimal WithholdingVatAmount { get; set; }

    public decimal WithholdingIcaAmount { get; set; }

    /// <summary>Lo abonado según la red.</summary>
    public decimal NetAmount { get; set; }

    /// <summary>Lo cobrado en Recaudo y el punto de venta por los lotes de la liquidación.</summary>
    public decimal CollectedAmount { get; set; }

    /// <summary>(cobrado − bruto) + (abono del extracto − neto, si hay línea).</summary>
    public decimal Difference { get; set; }

    public decimal Tolerance { get; set; }

    public AcquirerSettlementState Status { get; set; } = AcquirerSettlementState.Explained;

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public int? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>El hecho emitido, cuando la liquidación quedó explicada o aprobada.</summary>
    public Guid? FactMessagePublicId { get; set; }

    public ICollection<AcquirerSettlementLine> Lines { get; set; } = new List<AcquirerSettlementLine>();
}

/// <summary>
/// <c>TES_AcquirerSettlementLines</c>: un lote de la liquidación de la red con lo que cobró la cooperativa por ese lote (Recaudo o punto de venta) y su diferencia; sin lote
/// cobrado, <see cref="Source"/> es nulo. (nuevo)
/// </summary>
public class AcquirerSettlementLine : AuditableEntity
{
    public int SettlementId { get; set; }

    public AcquirerSettlement? Settlement { get; set; }

    public int LineNumber { get; set; }

    public string TerminalCode { get; set; } = string.Empty;

    public string? BatchNumber { get; set; }

    public DateOnly BatchDate { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal CommissionVatAmount { get; set; }

    public decimal WithholdingAmount { get; set; }

    public decimal NetAmount { get; set; }

    public AcquirerBatchSource? Source { get; set; }

    public decimal? CollectedAmount { get; set; }

    public decimal Difference { get; set; }
}
