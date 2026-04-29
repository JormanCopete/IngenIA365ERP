using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Web;

/// <summary>Maps to [dbo].[WEB_ExtraPayments] (web_extras).</summary>
public class WebExtraPayment : AuditableEntityLong
{
    public int? SequenceNumber { get; set; }
    public DateOnly? PaymentDate { get; set; }
    public decimal? Amount { get; set; }
}
