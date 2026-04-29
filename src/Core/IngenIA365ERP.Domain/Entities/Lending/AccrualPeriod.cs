using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_AccrualPeriods].</summary>
public class AccrualPeriod : AuditableEntityLong
{
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string BranchId { get; set; } = string.Empty;
    [MaxLength(10)]
    public string CostCenterId { get; set; } = string.Empty;
    [MaxLength(2)]
    public string Periodicity { get; set; } = string.Empty;
    [MaxLength(2)]
    public string PaymentCycle { get; set; } = string.Empty;
    public int AccrualPeriodNumber { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal AccruedAmount { get; set; }
    public decimal UsuryRate { get; set; }
    [MaxLength(3)]
    public string AccrualScope { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AccrualService { get; set; } = string.Empty;
}
