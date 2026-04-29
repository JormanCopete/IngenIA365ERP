using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_WithholdingParameters] (nom_parretfte).</summary>
public class WithholdingParameter : AuditableEntity
{
    public int PayrollCompanyId { get; set; }
    public int UvtRangeStart { get; set; }
    public int UvtRangeEnd { get; set; }
    public decimal Rate { get; set; }
    public int AdditionalUvt { get; set; }
}
