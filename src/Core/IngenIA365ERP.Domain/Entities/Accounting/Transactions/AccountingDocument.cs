using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting.Transactions;

/// <summary>
/// Comprobante contable (feature 009, FR-025, FR-028, FR-030, FR-037). Vive en
/// <c>Transactions</c> porque, una vez contabilizado, es inmutable (constitución, Principio XI):
/// la única corrección es la reversión, un documento nuevo con las líneas invertidas y la
/// referencia en ambos sentidos. El número se asigna al contabilizar; los borradores no lo
/// tienen. El origen dice qué módulo lo generó y a qué documento suyo corresponde: un
/// comprobante de módulo se consulta en Contabilidad, pero sólo su módulo lo reversa.
/// </summary>
public class AccountingDocument : AuditableEntityLong
{
    public int VoucherTypeId { get; set; }
    public VoucherType? VoucherType { get; set; }

    /// <summary>Consecutivo por tipo; null mientras es borrador (FR-020).</summary>
    public long? Number { get; set; }

    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
    public DocumentKind Kind { get; set; } = DocumentKind.Regular;

    /// <summary>CNT (manual), NOM, CAR, INV, TES, CDT, ACT.</summary>
    public string OriginModule { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public Guid? SourcePublicId { get; set; }

    /// <summary>Null sólo en la apertura, que se fecha antes del primer período (FR-084).</summary>
    public int? PeriodId { get; set; }
    public AccountingPeriod? Period { get; set; }

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    public int RegisteredByUserId { get; set; }
    public string RegisteredBy { get; set; } = string.Empty;
    public int? PostedByUserId { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedAt { get; set; }

    public long? ReversesDocumentId { get; set; }
    public AccountingDocument? ReversesDocument { get; set; }
    public long? ReversedByDocumentId { get; set; }
    public AccountingDocument? ReversedByDocument { get; set; }
    public string? ReversalReason { get; set; }

    public ICollection<JournalEntry> Lines { get; set; } = [];

    public bool EsDeModulo => OriginModule != ModuloContabilidad;
    public bool EstaCuadrado => TotalDebit == TotalCredit;

    public const string ModuloContabilidad = "CNT";
}
