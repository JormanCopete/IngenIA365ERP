using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>Maps to [dbo].[ADM_Subscriptions].</summary>
public class Subscription : AuditableEntity
{
    public int TenantId { get; set; }

    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal MonthlyPrice { get; set; }

    [MaxLength(5)]
    public string Currency { get; set; } = "COP";

    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public DateOnly? LastPaymentDate { get; set; }
    public DateOnly? NextBillingDate { get; set; }

    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    // Navigation
    public Tenant? Tenant { get; set; }
}
