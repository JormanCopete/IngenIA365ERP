using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Payments;

/// <summary>
/// <c>TES_PaymentVouchers</c> (feature 015, T1, fase 35, T0421; data-model §13.1; FR-117, FR-156): el comprobante de pago numerado de una orden, que nace con su resultado
/// «girada» —uno por orden— con lo que se pagó (bruto, retención, neto), la forma, la cuenta de origen, la fecha y la referencia del banco, y un código de verificación. Lo
/// que se pagó no cambia; lo único que se escribe después es su envío al beneficiario por correo, que sale sólo con la autorización del canal. (nuevo)
/// </summary>
public class PaymentVoucher : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int PaymentOrderId { get; set; }

    public int DisbursementResultId { get; set; }

    /// <summary>El resultado «girada» del que nace (la navegación sólo sirve para enlazarlo en la misma unidad de trabajo).</summary>
    public DisbursementResult? DisbursementResult { get; set; }

    public int BeneficiaryPersonId { get; set; }

    /// <summary>La cuenta propia de la que salió (la del lote), si se conoce.</summary>
    public int? BankAccountId { get; set; }

    public PaymentForm Form { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal WithholdingAmount { get; set; }

    public decimal NetAmount { get; set; }

    public DateOnly PaidOn { get; set; }

    public string TreasuryReference { get; set; } = string.Empty;

    public string? CheckNumber { get; set; }

    public string Concept { get; set; } = string.Empty;

    public string VerificationCode { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; }

    /// <summary>El último envío por correo al beneficiario: cuándo, a qué correo (enmascarado) y quién.</summary>
    public DateTime? SentAt { get; set; }

    public string? SentTo { get; set; }

    public int? SentByUserId { get; set; }

    public int SendCount { get; set; }
}
