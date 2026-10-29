using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_ClearingBatches</c> (feature 015, R2, fase 37, T0428; data-model §8.2; FR-084, FR-087): el lote de canje de los cheques recibidos que van consignados en la remesa del
/// día a una cuenta propia. Al armarlo los cheques pasan a «en canje»; se marcan pagados cuando una persona cruza el abono del extracto que los paga (el lote entero o una
/// parte) y se devuelven por la misma vía de FR-060. Lleva lo pagado y lo devuelto; cierra cuando ya no queda nada en canje. (nuevo)
/// </summary>
public class ClearingBatch : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public Guid OwnBankAccountPublicId { get; set; }

    public int BranchId { get; set; }

    /// <summary>La remesa de Tesorería en que salieron, si la hubo (por su <c>PublicId</c>, sin FK, R-05).</summary>
    public Guid? RemittancePublicId { get; set; }

    public DateOnly DepositedOn { get; set; }

    public int CheckCount { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal ReturnedAmount { get; set; }

    public ClearingBatchState Status { get; set; } = ClearingBatchState.InClearing;

    /// <summary>Las líneas del extracto que lo pagaron, separadas por coma (por su <c>PublicId</c>).</summary>
    public string? StatementLinePublicIds { get; set; }

    public int CreatedByUserId { get; set; }
}
