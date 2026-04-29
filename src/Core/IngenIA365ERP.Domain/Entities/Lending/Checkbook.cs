using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Checkbooks].</summary>
public class Checkbook : AuditableEntity
{
    public long AccountNumber { get; set; }
    public long RangeStart { get; set; }
    public long RangeEnd { get; set; }
    public DateOnly DeliveryDate { get; set; }
    [MaxLength(2)]
    public string IsBlocked { get; set; } = string.Empty;
}
