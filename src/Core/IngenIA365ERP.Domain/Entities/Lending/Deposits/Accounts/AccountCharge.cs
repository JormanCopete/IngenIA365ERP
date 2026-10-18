using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_AccountCharges</c> (feature 014, US4, T114; data-model §4.8; FR-128, R14): un cargo causado a una cuenta de captación. Documento con estado
/// —<c>Pending → Collected</c> (la siguiente consignación), <c>Pending → Waived</c>, <c>Collected → Waived</c> (condonar con reintegro),
/// <c>Collected → Reversed</c>, <c>Pending → Reversed</c>—; el dinero se mueve sólo por las operaciones del libro que lo nombran
/// (<see cref="AppliedOperationId"/>, <see cref="CollectedOperationId"/>). Único por cuenta, cargo y período (periódicos) y por operación y cargo (por
/// evento). El estado de la factura de Comercial (<see cref="CommercialInvoiceNumber"/>, <see cref="CommercialInvoiceStatus"/>) es informativo: una
/// factura rechazada no cambia el cargo. (nuevo)
/// </summary>
public class AccountCharge : AuditableEntity
{
    /// <summary>El cargo no se puede condonar en su estado (api.md §4).</summary>
    public const string CodigoNoCondonable = "Lending.Savings.ChargeNotWaivable";

    public const int LargoDelMotivo = 300;

    public int MemberAccountId { get; set; }

    /// <summary>La versión del cargo que se aplicó.</summary>
    public int DepositLineChargeId { get; set; }

    /// <summary>Copia del código estable del cargo.</summary>
    public string ChargeCode { get; set; } = string.Empty;

    public ChargeKind Kind { get; set; }

    /// <summary>Período de un cargo periódico.</summary>
    public DateOnly? PeriodFrom { get; set; }

    public DateOnly? PeriodTo { get; set; }

    /// <summary>La operación que disparó un cargo por evento.</summary>
    public long? TriggerOperationId { get; set; }

    /// <summary>La base de un cargo porcentual (o el valor de la operación).</summary>
    public decimal BaseAmount { get; set; }

    public decimal Amount { get; set; }

    public AccountChargeStatus Status { get; set; }

    /// <summary>La operación <c>Charge</c> que lo causó (debitó el capital o lo dejó por cobrar).</summary>
    public long AppliedOperationId { get; set; }

    /// <summary>La consignación que cobró el pendiente.</summary>
    public long? CollectedOperationId { get; set; }

    public DateTime? WaivedAt { get; set; }

    public int? WaivedByUserId { get; set; }

    public string? WaiverReason { get; set; }

    /// <summary>La decisión de la matriz de atribuciones (sujeto <c>ChargeWaiver</c>), si la hubo.</summary>
    public int? WaiverDecisionId { get; set; }

    /// <summary>La operación que lo reversó.</summary>
    public long? ReversedByOperationId { get; set; }

    public bool IsVatTaxed { get; set; }

    /// <summary>El <c>CargoDeCaptacionCausado</c> que pidió la factura a Comercial.</summary>
    public Guid? ChargeMessagePublicId { get; set; }

    /// <summary>El <c>CargoDeCaptacionReversado</c> que pidió la nota crédito.</summary>
    public Guid? ReversalMessagePublicId { get; set; }

    public string? CommercialInvoiceNumber { get; set; }

    /// <summary><c>Issued</c>, <c>Accepted</c>, <c>Rejected</c> o <c>Voided</c> (lo informa Comercial).</summary>
    public string? CommercialInvoiceStatus { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary><c>Pending → Collected</c>: la consignación que lo cobró.</summary>
    public void Cobrar(long operacionId)
    {
        if (Status != AccountChargeStatus.Pending)
            throw new InvalidOperationException($"El cargo {ChargeCode} está «{Status}»: sólo se cobra uno pendiente.");
        Status = AccountChargeStatus.Collected;
        CollectedOperationId = operacionId;
    }

    /// <summary>
    /// <c>Collected → Pending</c>: se reversó la consignación que cobró el pendiente (FR-002, el reverso arrastra a sus dependientes); vuelve a deberse y se
    /// cobra con la siguiente consignación.
    /// </summary>
    public void DevolverAPendiente(long consignacionReversada)
    {
        if (Status != AccountChargeStatus.Collected || CollectedOperationId != consignacionReversada)
            throw new InvalidOperationException($"El cargo {ChargeCode} no lo cobró esa consignación.");
        Status = AccountChargeStatus.Pending;
        CollectedOperationId = null;
    }

    /// <summary>¿Se puede condonar? Pendiente o cobrado (con reintegro); no uno ya condonado ni reversado.</summary>
    public bool Condonable => Status is AccountChargeStatus.Pending or AccountChargeStatus.Collected;

    /// <summary><c>Pending|Collected → Waived</c>, con quién, por qué y la decisión si la hubo.</summary>
    public void Condonar(DateTime instante, int? usuario, string motivo, int? decision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (!Condonable) throw new InvalidOperationException($"El cargo {ChargeCode} está «{Status}»: no se condona.");
        Status = AccountChargeStatus.Waived;
        WaivedAt = instante;
        WaivedByUserId = usuario;
        var texto = motivo.Trim();
        WaiverReason = texto.Length <= LargoDelMotivo ? texto : texto[..LargoDelMotivo];
        WaiverDecisionId = decision;
    }

    /// <summary><c>Pending|Collected → Reversed</c>: la operación de reverso.</summary>
    public void Reversar(long operacionId)
    {
        if (Status is not (AccountChargeStatus.Pending or AccountChargeStatus.Collected))
            throw new InvalidOperationException($"El cargo {ChargeCode} está «{Status}»: no se reversa.");
        Status = AccountChargeStatus.Reversed;
        ReversedByOperationId = operacionId;
    }
}

/// <summary>
/// <c>LND_AccountChargeExemptions</c> (feature 014, US4, T114; data-model §4.9; FR-128): la exoneración individual aprobada de un código de cargo en una
/// cuenta, con vigencia y motivo. La aprueba otra persona que quien la pide (segregación); una vigente por cuenta y código. Estado con fechas. (nuevo)
/// </summary>
public class AccountChargeExemption : AuditableEntity
{
    public const int LargoDelMotivo = 300;

    public int MemberAccountId { get; set; }

    /// <summary>El código estable del cargo.</summary>
    public string ChargeCode { get; set; } = string.Empty;

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int ApprovedByUserId { get; set; }

    /// <summary>La decisión de la matriz de atribuciones (sujeto <c>ChargeWaiver</c>), si la hubo.</summary>
    public int? DecisionId { get; set; }

    /// <summary>¿Rige el <paramref name="dia"/>?</summary>
    public bool VigenteEn(DateOnly dia) => !IsDeleted && ValidFrom <= dia && (ValidTo is null || dia <= ValidTo);
}
