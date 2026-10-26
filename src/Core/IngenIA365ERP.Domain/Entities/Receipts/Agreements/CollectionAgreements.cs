using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Agreements;

// Feature 015, R2 (fase 38, T0433; FR-088 a FR-093, FR-197, FR-218; data-model §8.2): los convenios de recaudo con bancos, corresponsales y pasarelas, la referencia de
// pago estable por producto, los archivos diarios que se cargan con su huella y la conciliación neta de cada archivo. La cuenta recaudadora es de Tesorería: se guarda por
// su PublicId, sin FK hacia TES_ (R-05). Sólo por archivo (C2 = A): no hay botón de pago en línea. (nuevo)

/// <summary>
/// <c>RCD_CollectionAgreements</c>: un convenio de recaudo —banco, corresponsal o pasarela—, con su cuenta recaudadora (la cuenta propia donde el aliado abona lo
/// recaudado neto) y el tercero que cobra la comisión y practica las retenciones. Lo que cambia con el tiempo vive en sus versiones. (nuevo)
/// </summary>
public class CollectionAgreement : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CollectionAgreementKind Kind { get; set; } = CollectionAgreementKind.Bank;

    /// <summary>La cuenta propia recaudadora (de Tesorería, por su <c>PublicId</c>).</summary>
    public Guid OwnBankAccountPublicId { get; set; }

    /// <summary>La oficina de la cuenta recaudadora: la de los recibos y las partidas del convenio.</summary>
    public int BranchId { get; set; }

    /// <summary>El banco, el corresponsal o la pasarela como persona (el tercero de la comisión y de las retenciones, FR-218).</summary>
    public int? PartnerPersonId { get; set; }

    /// <summary>El código del convenio ante el aliado (el que trae el archivo).</summary>
    public string? PartnerAgreementCode { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// <c>RCD_CollectionAgreementVersions</c>: lo que rige un convenio desde una fecha (FR-088): a qué identifica la referencia, su longitud con el dígito y el algoritmo del
/// dígito, el formato del archivo diario (ámbito <c>CollectionAgreementFile</c> u, en una pasarela, <c>PaymentGatewayFile</c>, como dato versionado de
/// <c>COR_BankFileFormats</c>) y la comisión pactada, que es informativa: la conciliación registra la que cobró el aliado. (nuevo)
/// </summary>
public class CollectionAgreementVersion : AuditableEntity
{
    public int CollectionAgreementId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public PaymentReferenceMode ReferenceMode { get; set; } = PaymentReferenceMode.ByProduct;

    /// <summary>Longitud total de la referencia con su dígito; cero, la que traiga.</summary>
    public int ReferenceLength { get; set; }

    public CheckDigitAlgorithm CheckDigit { get; set; } = CheckDigitAlgorithm.Modulo10;

    public int BankFileFormatId { get; set; }

    /// <summary>Comisión pactada por pago (informativa).</summary>
    public decimal? CommissionPerPayment { get; set; }

    /// <summary>Comisión pactada como fracción del valor (informativa).</summary>
    public decimal? CommissionRate { get; set; }

    public string? Note { get; set; }

    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>
/// <c>RCD_PaymentReferences</c>: la referencia de pago estable de un producto (o de una persona), con su dígito de verificación (FR-090; pedido del dueño para R2).
/// Es determinista —la base es el número del producto y el dígito sale del algoritmo— y se registra al emitirla o al encontrarla en un archivo; una referencia nunca se
/// reutiliza: la del producto cancelado se retira. (nuevo)
/// </summary>
public class PaymentReference : AuditableEntity
{
    /// <summary>La referencia completa, con su dígito (única).</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>La base sin el dígito: el número del producto o el documento de la persona, normalizado.</summary>
    public string BaseNumber { get; set; } = string.Empty;

    public string CheckDigitValue { get; set; } = string.Empty;

    public CheckDigitAlgorithm Algorithm { get; set; }

    public PaymentReferenceMode Mode { get; set; } = PaymentReferenceMode.ByProduct;

    public int PersonId { get; set; }

    /// <summary>El producto (crédito o cuenta) de Cartera o de Captaciones, por su <c>PublicId</c>; nulo en una referencia por persona.</summary>
    public Guid? ProductPublicId { get; set; }

    public ApplicationTarget? Target { get; set; }

    public string? ProductNumber { get; set; }

    public PaymentReferenceState Status { get; set; } = PaymentReferenceState.Active;

    public DateTime IssuedAt { get; set; }

    public DateTime? RetiredAt { get; set; }
}

/// <summary>Lo común de un archivo cargado de un convenio o de una pasarela (sin tabla propia: cada clase tiene la suya). (nuevo)</summary>
public abstract class CollectionFileBase : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int CollectionAgreementId { get; set; }

    public int CollectionAgreementVersionId { get; set; }

    public string? FileName { get; set; }

    /// <summary>La huella del archivo (FR-197): el mismo archivo no se carga dos veces.</summary>
    public string FileSha256 { get; set; } = string.Empty;

    public Guid? FileAttachmentPublicId { get; set; }

    public DateOnly FileDate { get; set; }

    public int Lines { get; set; }

    public int Applied { get; set; }

    public int Unidentified { get; set; }

    public int Rejected { get; set; }

    public int Omitted { get; set; }

    public decimal TotalInformed { get; set; }

    public decimal TotalApplied { get; set; }

    public decimal TotalUnidentified { get; set; }

    public CollectionFileState Status { get; set; } = CollectionFileState.Applied;

    public int LoadedByUserId { get; set; }

    public DateTime LoadedAt { get; set; }

    /// <summary>La corrida no interactiva que lo aplicó (<c>ProcessingRunKind.AgreementFile</c>).</summary>
    public int? ProcessingRunId { get; set; }
}

/// <summary>Lo común de una línea de un archivo de convenio o de pasarela. (nuevo)</summary>
public abstract class CollectionFileLineBase : AuditableEntity
{
    public int LineNumber { get; set; }

    public DateOnly PaymentDate { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    /// <summary>El identificador del pago que da el aliado: un pago que ya llegó en otro archivo no se aplica dos veces.</summary>
    public string? PartnerSequence { get; set; }

    public string? PayerTaxId { get; set; }

    public string? PayerName { get; set; }

    public CollectionFileLineOutcome Outcome { get; set; } = CollectionFileLineOutcome.Pending;

    public string? Code { get; set; }

    public string? Message { get; set; }

    public long? ReceiptId { get; set; }

    public int? UnidentifiedItemId { get; set; }

    public int? PaymentReferenceId { get; set; }
}

/// <summary><c>RCD_AgreementFiles</c>: el archivo diario de un convenio de banco o de corresponsal (FR-089, FR-092). (nuevo)</summary>
public class AgreementFile : CollectionFileBase
{
}

/// <summary><c>RCD_AgreementFileLines</c>: un pago informado por el convenio. (nuevo)</summary>
public class AgreementFileLine : CollectionFileLineBase
{
    public int AgreementFileId { get; set; }
}

/// <summary><c>RCD_GatewayFiles</c>: el archivo de una pasarela o del botón PSE (FR-091), que trae también el resultado de cada transacción. (nuevo)</summary>
public class GatewayFile : CollectionFileBase
{
}

/// <summary><c>RCD_GatewayFileLines</c>: una transacción de la pasarela; sólo las aprobadas se aplican. (nuevo)</summary>
public class GatewayFileLine : CollectionFileLineBase
{
    public int GatewayFileId { get; set; }

    /// <summary>El resultado tal como lo da la pasarela (aprobada, rechazada, pendiente).</summary>
    public string? GatewayOutcome { get; set; }

    public string? GatewayReference { get; set; }

    public DateOnly? GatewayDate { get; set; }
}

/// <summary>
/// <c>RCD_AgreementReconciliations</c>: lo informado por un archivo contra lo abonado en la cuenta recaudadora, neto de comisión, IVA y cada retención con su tercero
/// (FR-093, FR-218). Lo que queda sin explicar sobre la tolerancia exige la aprobación de otra persona; el hecho <c>ComisionDeConvenioRegistrada</c> nace al quedar
/// explicada o aprobada. El abono queda identificado en Recaudo (<c>BankItemOutcome.AgreementSettlement</c>). (nuevo)
/// </summary>
public class AgreementReconciliation : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int CollectionAgreementId { get; set; }

    public int? AgreementFileId { get; set; }

    public int? GatewayFileId { get; set; }

    public Guid StatementLinePublicId { get; set; }

    public DateOnly CreditedOn { get; set; }

    public decimal InformedAmount { get; set; }

    public decimal CreditedAmount { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal WithholdingsAmount { get; set; }

    /// <summary>Cada retención con su clase y su tercero (JSON).</summary>
    public string WithholdingsJson { get; set; } = "[]";

    public decimal UnexplainedAmount { get; set; }

    public decimal Tolerance { get; set; }

    public AgreementReconciliationState Status { get; set; } = AgreementReconciliationState.Explained;

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? Note { get; set; }
}
