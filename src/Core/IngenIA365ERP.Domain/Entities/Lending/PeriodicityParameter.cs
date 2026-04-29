using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PeriodicityParameters].</summary>
public class PeriodicityParameter : AuditableEntity
{
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(3)]
    public string DeductionClass { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int EndDay { get; set; }
    [MaxLength(3)]
    public string DayCount { get; set; } = string.Empty;
}
