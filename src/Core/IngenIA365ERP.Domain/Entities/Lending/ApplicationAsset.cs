using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_ApplicationAssets] (cop_solbienes).</summary>
public class ApplicationAsset : AuditableEntityLong
{
    public int ApplicationNumber { get; set; }
    [MaxLength(2)]
    public string AssetType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string AssetClass { get; set; } = string.Empty;
    [MaxLength(80)]
    public string? Address { get; set; }
    public int CityCode { get; set; }
    public decimal AssetValue { get; set; }
    [MaxLength(80)]
    public string? Brand { get; set; }
    [MaxLength(20)]
    public string? Model { get; set; }
}
