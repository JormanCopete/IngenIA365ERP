using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_InvoiceLineItems].</summary>
public class InvoiceLineItem : AuditableEntityLong
{
    public long InvoiceId { get; set; }
    public int CreditLineId { get; set; }
    public long PortfolioNumber { get; set; }
    public int AccrualPeriod { get; set; }
    public decimal? CapitalBalance { get; set; }
    public decimal? ExtraBalance { get; set; }
    public decimal? InterestBalance { get; set; }
    public decimal? DefaultBalance { get; set; }
    public decimal? InsuranceBalance { get; set; }

    // Navigation
    public CreditLineParameter? CreditLine { get; set; }
}
