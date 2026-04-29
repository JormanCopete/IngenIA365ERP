using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Maps to [dbo].[ACC_FinancialReports] (cnt_infmedian).</summary>
public class FinancialReport : AuditableEntity
{
    public int FormatId { get; set; }
    public int ConceptId { get; set; }
    public int PersonId { get; set; }
    public int AccountId { get; set; }
    public int Year { get; set; }
    public int? DocumentTypeCode { get; set; }
    public string? VerificationDigit { get; set; }
    public string? LastName1 { get; set; }
    public string? LastName2 { get; set; }
    public string? FirstName { get; set; }
    public string? CompanyName { get; set; }
    public string? FirstName2 { get; set; }
    public string? SecondName { get; set; }
    public int? Municipality { get; set; }
    public string? Address { get; set; }
    public string? SavingsAccount { get; set; }
    public decimal? Value1 { get; set; }
    public decimal? Value2 { get; set; }
    public decimal? Value3 { get; set; }
    public decimal? Value4 { get; set; }
    public decimal? Value5 { get; set; }
    public decimal? Value6 { get; set; }
    public decimal? Value7 { get; set; }
    public decimal? Value8 { get; set; }
    public decimal? Value9 { get; set; }
    public decimal? Value10 { get; set; }
    public string? ProcessedByLegacySystem { get; set; }

    // Navigation
    public Person Person { get; set; } = null!;
    public ChartOfAccount Account { get; set; } = null!;
}
