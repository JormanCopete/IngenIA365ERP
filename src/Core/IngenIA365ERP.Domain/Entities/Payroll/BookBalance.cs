using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_BookBalances] (nom_sallib).</summary>
public class BookBalance : AuditableEntityLong
{
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public int ConceptId { get; set; }
    public decimal SequenceNumber { get; set; }
    public int PeriodYear { get; set; }
    public decimal InitialAmount { get; set; }
    public decimal JanCharge { get; set; }
    public decimal JanPayment { get; set; }
    public decimal FebCharge { get; set; }
    public decimal FebPayment { get; set; }
    public decimal MarCharge { get; set; }
    public decimal MarPayment { get; set; }
    public decimal AprCharge { get; set; }
    public decimal AprPayment { get; set; }
    public decimal MayCharge { get; set; }
    public decimal MayPayment { get; set; }
    public decimal JunCharge { get; set; }
    public decimal JunPayment { get; set; }
    public decimal JulCharge { get; set; }
    public decimal JulPayment { get; set; }
    public decimal AugCharge { get; set; }
    public decimal AugPayment { get; set; }
    public decimal SepCharge { get; set; }
    public decimal SepPayment { get; set; }
    public decimal OctCharge { get; set; }
    public decimal OctPayment { get; set; }
    public decimal NovCharge { get; set; }
    public decimal NovPayment { get; set; }
    public decimal DecCharge { get; set; }
    public decimal DecPayment { get; set; }

    // Navigation
    public Employee? Employee { get; set; }
}
