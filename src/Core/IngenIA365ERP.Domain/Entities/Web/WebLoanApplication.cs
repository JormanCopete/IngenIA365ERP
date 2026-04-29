using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Web;

/// <summary>Maps to [dbo].[WEB_LoanApplications] (web_solcred).</summary>
public class WebLoanApplication : AuditableEntityLong
{
    public int? PersonId { get; set; }
    public int? SequenceNumber { get; set; }
    public DateOnly? ApplicationDate { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public int? CreditLineId { get; set; }
    public decimal? InterestRate { get; set; }
    public decimal? RequestedAmount { get; set; }
    public int? TermMonths { get; set; }

    [MaxLength(2)]
    public string? Periodicity { get; set; }

    public decimal? InstallmentAmount { get; set; }

    [MaxLength(300)]
    public string? Purpose { get; set; }

    [MaxLength(20)]
    public string? LegacyCodigoTer { get; set; }
}
