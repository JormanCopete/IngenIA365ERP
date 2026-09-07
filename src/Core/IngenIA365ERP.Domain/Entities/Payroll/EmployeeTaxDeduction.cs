using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_EmployeeTaxDeductions]. Deducciones y rentas exentas declaradas
/// por el empleado para depurar la base de retención (intereses de vivienda, medicina
/// prepagada, dependientes, pensión voluntaria, AFC). Los topes de cada clase son
/// parámetros legales.
/// </summary>
public class EmployeeTaxDeduction : AuditableEntity
{
    public int EmployeeId { get; set; }
    public TaxDeductionKind Kind { get; set; }
    public decimal? MonthlyAmount { get; set; }
    public decimal? Percent { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    public Employee? Employee { get; set; }

    public bool IsValidAt(DateTime date) =>
        ValidFrom <= date && (ValidTo is null || ValidTo >= date);
}
