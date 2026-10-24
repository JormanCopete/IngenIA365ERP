using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;

/// <summary>
/// <c>TES_Checkbooks</c> (feature 015, US19, T0225; data-model §11.1; FR-134, FR-219): la chequera de una cuenta propia con su rango, el siguiente número (mutable bajo el
/// cerrojo de la chequera), su custodio y la constancia de custodia. Agotada, pide la siguiente (<c>Treasury.Checkbook.Exhausted</c>). (nuevo)
/// </summary>
public class Checkbook : AuditableEntity
{
    public int BankAccountId { get; set; }

    public long FromNumber { get; set; }

    public long ToNumber { get; set; }

    public long NextNumber { get; set; }

    public CheckbookState Status { get; set; } = CheckbookState.Active;

    public int CustodianUserId { get; set; }

    public Guid? CustodyAttachmentPublicId { get; set; }

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public string? VoidReason { get; set; }
}

/// <summary>
/// <c>TES_IssuedChecks</c> (data-model §11.2; FR-135, FR-136): un cheque girado de una chequera —el de una orden de un lote de cheques— o un cheque en blanco dañado. Cruzado y
/// para el primer beneficiario por defecto (ligado a la exención del GMF). El estado avanza por sus eventos; un hueco de numeración sólo lo explica un
/// <c>SpoiledBlank</c> o un <c>Voided</c>. El nombre no choca con el heredado <c>Check</c> (R-04). (nuevo)
/// </summary>
public class IssuedCheck : AuditableEntity
{
    public int CheckbookId { get; set; }

    public long CheckNumber { get; set; }

    public int? PaymentOrderId { get; set; }

    public int? PaymentBatchItemId { get; set; }

    public int? BeneficiaryPersonId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly IssuedOn { get; set; }

    public bool Crossed { get; set; } = true;

    public bool FirstBeneficiaryOnly { get; set; } = true;

    public IssuedCheckState Status { get; set; } = IssuedCheckState.Issued;

    public Guid? DeliveryReceiptAttachmentPublicId { get; set; }

    public DateOnly? DeliveredOn { get; set; }

    public DateOnly? CashedOn { get; set; }

    public string? VoidReason { get; set; }

    public bool IsOpeningLoad { get; set; }
}

/// <summary><c>TES_IssuedCheckEvents</c> (H): cada transición del cheque girado con quién, cuándo y su soporte. (nuevo)</summary>
public class IssuedCheckEvent : AuditableEntity, IHechoInmutable
{
    public int IssuedCheckId { get; set; }

    public IssuedCheckState FromStatus { get; set; }

    public IssuedCheckState ToStatus { get; set; }

    public int ByUserId { get; set; }

    public DateTime At { get; set; }

    public Guid? SupportAttachmentPublicId { get; set; }

    public string? Note { get; set; }
}
