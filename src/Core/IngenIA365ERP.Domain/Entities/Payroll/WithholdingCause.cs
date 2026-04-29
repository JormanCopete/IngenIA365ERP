using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_WithholdingCauses] (nom_cauret).</summary>
public class WithholdingCause : AuditableEntity
{
    public int Code { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;

    public int IndemnityType { get; set; }
    public int AutoDeductions { get; set; }
}
