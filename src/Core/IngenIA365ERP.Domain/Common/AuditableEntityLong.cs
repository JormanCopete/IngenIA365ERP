namespace IngenIA365ERP.Domain.Common;

public abstract class AuditableEntityLong : BaseEntityLong
{
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
