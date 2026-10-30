using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Sessions;

// Feature 015, US2 (T0089; R-14; FR-031, FR-032, FR-182; data-model §2.3): el arqueo de una sesión o de una bóveda, por medio, con su detalle (denominaciones,
// lotes de datáfono, referencias). Son hechos (H): se insertan completos al registrar lo contado. Sólo la aprobación de una diferencia se escribe después,
// una vez, en la línea ([EscrituraUnica]).

/// <summary><c>RCD_CashCounts</c> (H): el arqueo de cierre, sorpresivo o diferido de una sesión (o de una bóveda). (nuevo)</summary>
public class CashCount : AuditableEntity, IHechoInmutable
{
    public int? CashSessionId { get; set; }

    public int? VaultId { get; set; }

    public CashCountKind Kind { get; set; }

    public DateTime CountedAt { get; set; }

    public int CountedByUserId { get; set; }

    public int? WitnessUserId { get; set; }

    /// <summary>Lo sellado en la sesión: el cajero contó sin ver lo esperado.</summary>
    public bool IsBlind { get; set; }

    /// <summary>El acta del arqueo sorpresivo o del billete retenido.</summary>
    public Guid? MinutesAttachmentPublicId { get; set; }

    /// <summary>El código de verificación del informe del cierre o del acta (FR-156).</summary>
    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary>
/// <c>RCD_CashCountLines</c> (H): lo esperado, lo contado y la diferencia de un medio, con su tolerancia, su motivo y su tratamiento. La aprobación de la
/// diferencia sobre la tolerancia (otra persona, con motivo) se escribe una sola vez. (nuevo)
/// </summary>
public class CashCountLine : AuditableEntity, IHechoInmutable
{
    public int CashCountId { get; set; }

    public int PaymentMeansId { get; set; }

    public CashCountMethod Method { get; set; }

    public decimal Expected { get; set; }

    public decimal Counted { get; set; }

    public decimal Difference { get; set; }

    public decimal Tolerance { get; set; }

    public bool WithinTolerance { get; set; }

    public string? Reason { get; set; }

    public CountTreatment Treatment { get; set; }

    [EscrituraUnica] public Guid? ApprovalRequestPublicId { get; set; }

    [EscrituraUnica] public int? ApprovedByUserId { get; set; }

    [EscrituraUnica] public DateTime? ApprovedAt { get; set; }

    [EscrituraUnica] public string? ApprovalReason { get; set; }

    /// <summary>La diferencia necesita la aprobación de otra persona y todavía no la tiene.</summary>
    public bool PorAprobar => Difference != 0m && !WithinTolerance && ApprovedAt is null;
}

/// <summary><c>RCD_CashCountDenominations</c> (H): el efectivo contado por denominación. (nuevo)</summary>
public class CashCountDenomination : AuditableEntity, IHechoInmutable
{
    public int CashCountLineId { get; set; }

    public int CashDenominationId { get; set; }

    public int Quantity { get; set; }

    public decimal Amount { get; set; }
}

/// <summary><c>RCD_CashCountTerminalBatches</c> (H): un lote de datáfono contado. (nuevo)</summary>
public class CashCountTerminalBatch : AuditableEntity, IHechoInmutable
{
    public int CashCountLineId { get; set; }

    public int CardTerminalId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

/// <summary>
/// <c>RCD_CashCountReferences</c> (H): una referencia (transferencia, consignación, cheque) cotejada en el arqueo, con su valor y si se confirmó. El medio del
/// recibo que cubre (<c>RCD_ReceiptMeans.Id</c>) se anota cuando la referencia viene de un recibo. (nuevo)
/// </summary>
public class CashCountReference : AuditableEntity, IHechoInmutable
{
    public int CashCountLineId { get; set; }

    public long? ReceiptMeansId { get; set; }

    public string Reference { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public bool Confirmed { get; set; }
}
