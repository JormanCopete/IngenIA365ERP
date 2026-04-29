using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Shifts] (inv_turnos).</summary>
public class Shift : AuditableEntity
{
    public int ShiftCode { get; set; }

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? StartTime { get; set; }

    [MaxLength(10)]
    public string? EndTime { get; set; }
}
