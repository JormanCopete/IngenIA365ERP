using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_CostCenters] (sys_cencos + nom_cencos + cnt_cencos).
/// </summary>
public class CostCenter : AuditableEntity
{
    [MaxLength(20)]
    public string? LegacyCode { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CompanyName { get; set; }

    [MaxLength(20)]
    public string? CompanyTaxId { get; set; }

    public short PayrollType { get; set; }

    public short Period { get; set; }

    public short PayrollPeriodicity { get; set; }

    [MaxLength(30)]
    public string? PayrollStatus { get; set; }
}
