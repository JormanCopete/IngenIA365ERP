using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_PreLiquidationResponses] (nom_respreliq).</summary>
public class PreLiquidationResponse : AuditableEntityLong
{
    [MaxLength(10)]
    public string PilaCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string TaxId { get; set; } = string.Empty;

    public int CheckDigit { get; set; }
    public int TotalEmployees { get; set; }
    public int IbcAmount { get; set; }
    public int ContributionAmount { get; set; }
    public int UpcAmount { get; set; }
    public int SolidarityAmount { get; set; }
    public int GeneralAuth { get; set; }
    public int GeneralValue { get; set; }
    public int MaternityAuth { get; set; }
    public int MaternityValue { get; set; }

    [MaxLength(15)]
    public string WorkRiskAuth { get; set; } = string.Empty;

    public int WorkRiskValue { get; set; }

    [MaxLength(5)]
    public string EntityClass { get; set; } = string.Empty;
}
