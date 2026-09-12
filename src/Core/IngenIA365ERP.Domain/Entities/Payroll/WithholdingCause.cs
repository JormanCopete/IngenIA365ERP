using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_WithholdingCauses] (nom_cauret).</summary>
public class WithholdingCause : AuditableEntity
{
    /// <summary>Código alfanumérico (hasta 10) que elige la cooperativa; único en la tabla. Numérico hasta el 2026-09-12.</summary>
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;

    public int IndemnityType { get; set; }
    public int AutoDeductions { get; set; }
}
