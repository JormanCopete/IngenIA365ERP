using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_PeopleFinancial] — one-to-one with Person for financial capacity.
/// Legacy: extracted from sys_maenit financial fields.
/// </summary>
public class PersonFinancial : AuditableEntity
{
    public int PersonId { get; set; }

    public decimal DebtCapacity { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal VariableIncome { get; set; }
    public decimal? RentalIncome { get; set; }
    public decimal? PensionIncome { get; set; }
    public decimal? ThirdPartyDebts { get; set; }
    public decimal? MonthlyFixedExpenses { get; set; }
    public decimal? PersonalExpenses { get; set; }
    public decimal? PensionDeduction { get; set; }
    public decimal CreditScore { get; set; }
    public decimal CreditBureauScore { get; set; }

    [MaxLength(4)]
    public string? CreditBureauRating { get; set; }

    public decimal ExternalDebtPayment { get; set; }
    public decimal ExternalDebtBalance { get; set; }
    public decimal PastDueCreditBureau { get; set; }

    // Navigation
    public Person Person { get; set; } = null!;
}
