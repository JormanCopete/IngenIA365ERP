using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ApplicationCodebtors] (cop_solcodeudor).</summary>
public class ApplicationCodebtor : AuditableEntityLong
{
    public int ApplicationId { get; set; }
    [MaxLength(20)]
    public string CodeudorCode { get; set; } = string.Empty;
    public decimal? Salary { get; set; }
    public decimal? OtherIncome { get; set; }
    public decimal? RentalIncome { get; set; }
    public decimal? VariableIncome { get; set; }
    public decimal? EmployerDeductions { get; set; }
    public decimal? ThirdPartyDebts { get; set; }
    public decimal? OtherDeductions { get; set; }
    public decimal? MonthlyAvailable { get; set; }
    public decimal PensionIncome { get; set; }
    public decimal PensionDeduction { get; set; }
    public decimal ParafiscalDeduction { get; set; }
    [MaxLength(2)]
    public string PaymentCapacityPct { get; set; } = string.Empty;
    public decimal PersonalExpenses { get; set; }
    public decimal EmployerDeductionsCash { get; set; }
}
