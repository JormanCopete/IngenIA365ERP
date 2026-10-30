using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Remittances;

/// <summary>
/// <c>TES_CashRemittances</c> (feature 015, US19, T0225; data-model §11.3; R-15; FR-105): la remesa de efectivo entre la bóveda de una oficina (de Recaudo, por su
/// <c>PublicId</c>) y una cuenta propia, con acta y sello de bolsa. La registra Recaudo por <c>IRemesasDeTesoreria</c> en la misma transacción que el movimiento de bóveda
/// (bóveda → banco) o Tesorería (banco → bóveda); queda en tránsito hasta confirmarla con soporte o por el cruce del extracto. (nuevo)
/// </summary>
public class CashRemittance : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public RemittanceDirection Direction { get; set; }

    public int BranchId { get; set; }

    public Guid VaultPublicId { get; set; }

    public int BankAccountId { get; set; }

    public decimal Amount { get; set; }

    public string? BagSeal { get; set; }

    public Guid? MinutesAttachmentPublicId { get; set; }

    /// <summary>El documento de Recaudo que la originó (el movimiento de bóveda), o nulo si la registró Tesorería.</summary>
    public Guid? SourceDocumentPublicId { get; set; }

    public string? SourceDocumentNumber { get; set; }

    public Guid IdempotencyKey { get; set; }

    public DateOnly DispatchDate { get; set; }

    public DateTime DispatchedAt { get; set; }

    public RemittanceState Status { get; set; } = RemittanceState.InTransit;

    public Guid? ConfirmedByStatementLinePublicId { get; set; }

    public Guid? ConfirmationSupportAttachmentPublicId { get; set; }

    public int? ConfirmedByUserId { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateOnly? ConfirmedOn { get; set; }

    /// <summary>El uso aprobado del fondo de liquidez cuando la remesa sale de una cuenta del fondo (banco → bóveda).</summary>
    public int? LiquidityFundUseId { get; set; }

    /// <summary>Remesa o consignación en tránsito cargada por la puesta en marcha (<c>tesoreria.remesas-y-consignaciones-en-transito</c>, US13): sin hecho. (nuevo)</summary>
    public bool IsOpeningLoad { get; set; }
}
