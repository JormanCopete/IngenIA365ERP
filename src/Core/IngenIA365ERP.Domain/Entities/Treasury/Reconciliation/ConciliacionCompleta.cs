using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;

// Feature 015, T2 (fase 41, T0447; FR-128 a FR-130, FR-168, FR-218; data-model §13.2): lo que trae la conciliación completa: las reglas configurables de emparejamiento,
// las notas bancarias con su aprobación y su hecho, y las dispersiones de Nómina informadas a Tesorería. (nuevo)

/// <summary>
/// <c>TES_ReconciliationRules</c>: una regla de emparejamiento automático (FR-129) —criterio, días de holgura y prioridad—, para todas las cuentas o para una. Sin reglas
/// activas, el cruce es el de la conciliación mínima. (nuevo)
/// </summary>
public class ReconciliationRule : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ReconciliationRuleCriterion Criterion { get; set; }

    public int ToleranceDays { get; set; }

    public int Priority { get; set; }

    /// <summary>La cuenta propia a la que aplica; nula, a todas.</summary>
    public int? BankAccountId { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// <c>TES_BankNotes</c>: una nota bancaria (FR-130, FR-218) —GMF, comisión con su IVA, intereses en bruto con su retención, impuestos u otra— de una cuenta propia, casi
/// siempre desde una línea del extracto. La registra una persona y, si <c>NotasBancarias.ExigenAprobacion</c>, la aprueba otra; al aprobarse nace
/// <c>NotaBancariaRegistrada</c> y queda como registro de los libros para cruzar con su línea. Con el banco (o el tercero que cobró) como tercero. (nuevo)
/// </summary>
public class BankNote : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int BankAccountId { get; set; }

    public Guid? StatementLinePublicId { get; set; }

    public BankNoteKind Kind { get; set; }

    public DateOnly NoteDate { get; set; }

    /// <summary>El movimiento en el banco: un cargo (GMF, comisión, impuesto) o un abono (intereses netos).</summary>
    public StatementLineDirection Direction { get; set; }

    /// <summary>Lo que se movió en el banco (el neto).</summary>
    public decimal Amount { get; set; }

    /// <summary>La base: la comisión antes del IVA, o los intereses en bruto.</summary>
    public decimal BaseAmount { get; set; }

    public decimal VatAmount { get; set; }

    public decimal WithholdingAmount { get; set; }

    public int? ThirdPartyPersonId { get; set; }

    public string? Description { get; set; }

    public BankNoteState Status { get; set; } = BankNoteState.PendingApproval;

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public int? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? RejectionReason { get; set; }
}

/// <summary>
/// <c>TES_PayrollDispersions</c>: la dispersión que Nómina generó con su archivo y le informó a Tesorería (<c>DispersionDeNominaGenerada</c> v1, E-05; FR-168): la cuenta de
/// origen, el total, las líneas y el día de pago. Nómina sigue pagando con su archivo (D27); Tesorería la usa como compromiso de la posición de liquidez y como registro de
/// los libros para conciliar el cargo del banco. (nuevo; no estaba en data-model §13.2)
/// </summary>
public class PayrollDispersion : AuditableEntity
{
    public Guid MessagePublicId { get; set; }

    public Guid FilePublicId { get; set; }

    public Guid? RunPublicId { get; set; }

    public int? BankAccountId { get; set; }

    public string? SourceBankCode { get; set; }

    public string? SourceAccountNumber { get; set; }

    public decimal Total { get; set; }

    public int LineCount { get; set; }

    public DateOnly PayDate { get; set; }

    public DateTime ReceivedAt { get; set; }
}
