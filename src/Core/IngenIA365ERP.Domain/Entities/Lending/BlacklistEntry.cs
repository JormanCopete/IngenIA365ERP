using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Blacklist].</summary>
public class BlacklistEntry : AuditableEntity
{
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    public int ListCode { get; set; }
    public DateOnly EntryDate { get; set; }
    [MaxLength(2)]
    public string Status { get; set; } = string.Empty;
    [MaxLength(2)]
    public string IdType { get; set; } = string.Empty;
    [MaxLength(20)]
    public string IdentificationNumber { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? PersonType { get; set; }
    [MaxLength(80)]
    public string CompanyName { get; set; } = string.Empty;
    [MaxLength(80)]
    public string? PersonName { get; set; }
    public int Nationality { get; set; }
    [MaxLength(80)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    [MaxLength(2)]
    public string IsImported { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
}
