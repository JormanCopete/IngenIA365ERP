using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementOrders</c> (feature 013; data-model §7.4; FR-045, FR-047, FR-048, D27): orden de giro de un tramo, con
/// beneficiario y cuenta sellados (I-1). Nunca en especie; el neto es el bruto menos lo recogido y es positivo. El estado sólo lo
/// mueve el <c>ResultadoDelGiro</c> consumido (§7.5). No es hecho. Forma sin comportamiento.
/// </summary>
public class DisbursementOrder : AuditableEntity
{
    public int LoanId { get; set; }

    /// <summary>El tramo (<c>LND_LoanOperations</c>, bigint); una orden por tramo.</summary>
    public long DisbursementOperationId { get; set; }

    /// <summary>Numerador <c>DisbursementOrder</c>; único.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Nunca <c>InKind</c> (<c>CK_LND_DisbursementOrders_Form</c>).</summary>
    public DisbursementForm Form { get; set; }

    /// <summary>Deudor o tercero.</summary>
    public int BeneficiaryPersonId { get; set; }

    /// <summary>Copia tal como se emitió (I-1).</summary>
    public string BeneficiaryTaxIdType { get; set; } = string.Empty;

    public string BeneficiaryTaxId { get; set; } = string.Empty;

    public string BeneficiaryName { get; set; } = string.Empty;

    /// <summary>Obligatorio en <c>Transfer</c>; propuesto desde la persona y corregible al desembolsar (I10).</summary>
    public int? DestinationBankId { get; set; }

    public byte? DestinationAccountType { get; set; }

    public string? DestinationAccountNumber { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal PayoffAmount { get; set; }

    /// <summary><c>GrossAmount − PayoffAmount</c>, mayor que cero (<c>CK_LND_DisbursementOrders_Net</c>).</summary>
    public decimal NetAmount { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public DisbursementOrderStatus Status { get; set; } = DisbursementOrderStatus.Issued;

    public DateTime IssuedAt { get; set; }

    public int IssuedByUserId { get; set; }

    public DateTime? StatusChangedAt { get; set; }

    /// <summary>Emisión + <c>Giros.DiasSinResultado</c> hábiles; pasado, alerta <c>Cartera.GiroSinResultado</c>.</summary>
    public DateOnly ResultDueOn { get; set; }

    /// <summary>Anulación pedida, a la espera de Tesorería: el estado no cambia hasta su respuesta.</summary>
    public DateTime? VoidRequestedAt { get; set; }

    public int? VoidRequestedByUserId { get; set; }

    public string? VoidReason { get; set; }

    /// <summary>Nuevo giro enlazado a la orden anterior.</summary>
    public int? PreviousOrderId { get; set; }

    /// <summary>Se decide al desembolsar y no cambia: la paga Cartera por archivo plano (C10). Sólo en <c>Transfer</c>.</summary>
    public bool PaidByFile { get; set; }

    /// <summary>Archivo no anulado que la lleva; la referencia vive sólo aquí (C10).</summary>
    public int? CurrentDisbursementFileId { get; set; }
}
