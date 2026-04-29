using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ProvisionParameters].</summary>
public class ProvisionParameter : AuditableEntity
{
    public int Period { get; set; }
    public int Code { get; set; }
    public decimal RateB { get; set; }
    public decimal RateC { get; set; }
    public decimal RateD { get; set; }
    public decimal RateE { get; set; }
}
