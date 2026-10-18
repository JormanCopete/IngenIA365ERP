namespace IngenIA365ERP.Domain.Enums.Lending;

// Libranza (K2), novedades (K3), reclamación al seguro e insolvencia (feature 013, T0201; data-model §9.1 y parte 2 «Nombres
// fijados», C65). Los valores de entregas posteriores existen desde K1 (data-model §0.14).

/// <summary>De dónde sale un no aplicado de una cuota (T92).</summary>
public enum NonDeductionOrigin
{
    Contract = 1,
    Activation = 2,
    PaymasterResult = 3,
}

/// <summary>
/// Por qué no se descontó una cuota (C56). Mismos nombres y números que <c>MotivoDeNoDescuento</c> del contrato (data-model §9.4;
/// <c>LosEnumsDelContratoNumeranComoElDominio</c>).
/// </summary>
public enum NonDeductionReason
{
    DeductionCap = 1,
    InsufficientNet = 2,
    Termination = 3,
    Leave = 4,
    Disability = 5,
    DeductionPriority = 6,
    Other = 99,
}

/// <summary>De dónde nació un vínculo de forma de pago y pagaduría (C44).</summary>
public enum LoanPayrollLinkOrigin
{
    Origination = 1,
    Migration = 2,
    Transfer = 3,
    CommercialSale = 4,
    ChannelChange = 5,
}

/// <summary>Estado de un convenio de libranza (K2).</summary>
public enum PayrollAgreementStatus
{
    Active = 1,
    Suspended = 2,
    Closed = 3,
}

/// <summary>Resultado de un descuento informado por la pagaduría (K2).</summary>
public enum PayrollResultOutcome
{
    Deducted = 1,
    PartiallyDeducted = 2,
    NotDeducted = 3,
}

/// <summary>Seguimiento de un no aplicado de pagaduría (K2).</summary>
public enum NonDeductionFollowUpAction
{
    DebtorNotified = 1,
    IncludedInNextFile = 2,
    MovedToCashPayment = 3,
    SentToCollection = 4,
    Closed = 5,
}

/// <summary>Estado de un archivo de descuentos (K2, F-11).</summary>
public enum PayrollFileStatus
{
    Generated = 1,
    Superseded = 2,
    Sent = 3,
    Voided = 4,
}

/// <summary>Novedad de una línea del archivo de descuentos (K2, F-11).</summary>
public enum PayrollNoveltyKind
{
    New = 1,
    Change = 2,
    Cancellation = 3,
    Unchanged = 4,
}

/// <summary>Mecanismo de una novedad (K3).</summary>
public enum NoveltyMechanism
{
    Extension = 1,
    PaymentDateChange = 2,
    RateChange = 3,
    Novation = 4,
    Refinancing = 5,

    /// <summary>
    /// K3 (T0883, C51): el cambio de oficina del crédito, sin efecto en la causa (siempre modificación) ni en el plan; mueve los saldos a la sucursal
    /// contable nueva con <c>DimensionContableCambiada</c>. Sujeto a la confirmación del dueño (tasks.md, notas 10). (nuevo)
    /// </summary>
    BranchChange = 6,
}

/// <summary>Causa de una novedad (K3).</summary>
public enum NoveltyCause
{
    Restructuring = 1,
    Modification = 2,
}

/// <summary>Estado de una novedad (K3).</summary>
public enum LoanNoveltyStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Applied = 5,
    Cancelled = 6,
}

/// <summary>Papel de un crédito en una novedad (K3).</summary>
public enum NoveltyLoanRole
{
    Modified = 1,
    Collected = 2,
    Resulting = 3,
}

/// <summary>Quién consiente una novedad (K3).</summary>
public enum ConsentPartyRole
{
    CoDebtor = 1,
    Guarantor = 2,
    CollateralOwner = 3,
}

/// <summary>Estado de un consentimiento (K3).</summary>
public enum ConsentStatus
{
    Pending = 1,
    Accepted = 2,
    Refused = 3,
}

/// <summary>Qué se hace si alguien no consiente (K3).</summary>
public enum ConsentRefusalResolution
{
    DoNotApply = 1,
    ApplyWithGuaranteeExtinguished = 2,
}

/// <summary>Estado de una condonación (K3).</summary>
public enum WaiverStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Applied = 5,
    Cancelled = 6,
}

/// <summary>Estado de un lote de castigo (K3).</summary>
public enum WriteOffBatchStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Applied = 5,
    Cancelled = 6,
}

/// <summary>Estado de un ítem de un lote de castigo (K3).</summary>
public enum WriteOffItemStatus
{
    Included = 1,
    Rejected = 2,
    WrittenOff = 3,
}

/// <summary>Estado de una reclamación al seguro de vida deudores (T102). <c>InProcess = 1</c> es el filtro del único vivo.</summary>
public enum InsuranceClaimStatus
{
    InProcess = 1,
    Paid = 2,
    Denied = 3,
    Withdrawn = 4,
}

/// <summary>Estado de un proceso de insolvencia (T102). <c>Negotiating = 1</c> es el filtro del único vivo.</summary>
public enum InsolvencyProceedingStatus
{
    Negotiating = 1,
    Agreement = 2,
    Failed = 3,
}

/// <summary>Régimen de insolvencia (Ley 1116, Ley 1564).</summary>
public enum InsolvencyRegime
{
    BusinessReorganization = 1,
    NaturalPersonDebtNegotiation = 2,
}
