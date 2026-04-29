using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_AuxiliaryDocuments] (cnt_docaux).</summary>
public class AuxiliaryDocument : AuditableEntityLong
{
    public string? LegacyKey { get; set; }
    public int PeriodYear { get; set; }
    public string AuxiliaryType { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public int? PersonId { get; set; }
    public int BranchId { get; set; }
    public int CostCenterId { get; set; }
    public string? DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Detail { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal OriginalValue { get; set; }
    public decimal InitialBalance { get; set; }
    public string? InvoiceNumber { get; set; }

    // Monthly debit/credit columns
    public decimal JanDebit { get; set; }
    public decimal JanCredit { get; set; }
    public decimal FebDebit { get; set; }
    public decimal FebCredit { get; set; }
    public decimal MarDebit { get; set; }
    public decimal MarCredit { get; set; }
    public decimal AprDebit { get; set; }
    public decimal AprCredit { get; set; }
    public decimal MayDebit { get; set; }
    public decimal MayCredit { get; set; }
    public decimal JunDebit { get; set; }
    public decimal JunCredit { get; set; }
    public decimal JulDebit { get; set; }
    public decimal JulCredit { get; set; }
    public decimal AugDebit { get; set; }
    public decimal AugCredit { get; set; }
    public decimal SepDebit { get; set; }
    public decimal SepCredit { get; set; }
    public decimal OctDebit { get; set; }
    public decimal OctCredit { get; set; }
    public decimal NovDebit { get; set; }
    public decimal NovCredit { get; set; }
    public decimal DecDebit { get; set; }
    public decimal DecCredit { get; set; }
    public decimal Period13Amount { get; set; }

    // Navigation
    public ChartOfAccount Account { get; set; } = null!;
    public Person? Person { get; set; }
    public Branch Branch { get; set; } = null!;
    public CostCenter CostCenter { get; set; } = null!;
}
