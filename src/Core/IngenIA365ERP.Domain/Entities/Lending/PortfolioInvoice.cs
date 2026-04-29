using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_PortfolioInvoices].</summary>
public class PortfolioInvoice : AuditableEntityLong
{
    public decimal InvoiceNumber { get; set; }
    [MaxLength(120)]
    public string Description { get; set; } = string.Empty;
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int CreditLineId { get; set; }
    public decimal PortfolioNumber { get; set; }
    public int Period { get; set; }
    [MaxLength(5)]
    public string VoucherType { get; set; } = string.Empty;
    public long DocumentNumber { get; set; }
    public decimal Amount { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
