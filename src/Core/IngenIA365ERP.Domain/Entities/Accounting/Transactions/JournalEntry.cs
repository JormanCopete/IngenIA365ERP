using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting.Transactions;

/// <summary>
/// Línea de un comprobante (feature 009, FR-025). Toda línea lleva sucursal; tercero, documento
/// cruce, centro de costo y base gravable según las reglas de la cuenta. <see cref="Date"/> e
/// <see cref="IsPosted"/> repiten lo del documento a propósito: todos los saldos son sumas sobre
/// esta tabla (R4) y se agregan sin unir con el documento.
/// </summary>
public class JournalEntry : AuditableEntityLong
{
    public long DocumentId { get; set; }
    public AccountingDocument? Document { get; set; }
    public int LineNumber { get; set; }

    public int AccountId { get; set; }
    public ChartOfAccount? Account { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }

    public int? PersonId { get; set; }
    public Person? Person { get; set; }

    public int? CrossDocumentTypeId { get; set; }
    public CrossDocumentType? CrossDocumentType { get; set; }
    public string? CrossDocumentNumber { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
    public decimal? TaxBase { get; set; }

    /// <summary>Fecha del documento, desnormalizada para agregar por rango sin unir.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Verdadero cuando el documento está contabilizado o reversado (los borradores no cuentan).</summary>
    public bool IsPosted { get; set; }

    /// <summary>Conciliación bancaria: cuándo se emparejó con una línea del extracto.</summary>
    public DateTime? ReconciledAt { get; set; }

    public decimal Importe => Debit > 0 ? Debit : Credit;
}
