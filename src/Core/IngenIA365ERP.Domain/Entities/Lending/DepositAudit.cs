using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_DepositAudit].</summary>
public class DepositAudit : AuditableEntityLong
{
    [MaxLength(2)]
    public string Action { get; set; } = string.Empty;
    [MaxLength(20)]
    public string? PersonCode { get; set; }
    public int? DepositLineId { get; set; }
    public long AccountNumber { get; set; }
    public DateOnly? CreationDate_Old { get; set; }
    public DateOnly? CreationDate_New { get; set; }
    [MaxLength(20)]
    public string? LegalRep_Old { get; set; }
    [MaxLength(20)]
    public string? LegalRep_New { get; set; }
    [MaxLength(50)]
    public string? RepName_Old { get; set; }
    [MaxLength(50)]
    public string? RepName_New { get; set; }
    [MaxLength(40)]
    public string? Address_Old { get; set; }
    [MaxLength(40)]
    public string? Address_New { get; set; }
    [MaxLength(20)]
    public string? Phone_Old { get; set; }
    [MaxLength(20)]
    public string? Phone_New { get; set; }
    [MaxLength(20)]
    public string? CellPhone_Old { get; set; }
    [MaxLength(20)]
    public string? CellPhone_New { get; set; }
    [MaxLength(2)]
    public string? Status_Old { get; set; }
    [MaxLength(2)]
    public string? Status_New { get; set; }
    public DateOnly? ExemptionDate_Old { get; set; }
    public DateOnly? ExemptionDate_New { get; set; }
    [MaxLength(2)]
    public string? AutoDebit_Old { get; set; }
    [MaxLength(2)]
    public string? AutoDebit_New { get; set; }
    public DateOnly? FirstDeduction_Old { get; set; }
    public DateOnly? FirstDeduction_New { get; set; }
    public DateOnly? EntryDate_Old { get; set; }
    public DateOnly? EntryDate_New { get; set; }
    [MaxLength(2)]
    public string? DeductionType_Old { get; set; }
    [MaxLength(2)]
    public string? DeductionType_New { get; set; }
    [MaxLength(2)]
    public string? Periodicity_Old { get; set; }
    [MaxLength(2)]
    public string? Periodicity_New { get; set; }
    [MaxLength(2)]
    public string? Cycle_Old { get; set; }
    [MaxLength(2)]
    public string? Cycle_New { get; set; }
    [MaxLength(2)]
    public string? Seal_Old { get; set; }
    [MaxLength(2)]
    public string? Seal_New { get; set; }
    [MaxLength(2)]
    public string? Protector_Old { get; set; }
    [MaxLength(2)]
    public string? Protector_New { get; set; }
    public int? RegSignatures_Old { get; set; }
    public int? RegSignatures_New { get; set; }
    public int? ReqSignatures_Old { get; set; }
    public int? ReqSignatures_New { get; set; }
    [MaxLength(2)]
    public string? Exempt_Old { get; set; }
    [MaxLength(2)]
    public string? Exempt_New { get; set; }
    [MaxLength(50)]
    public string? UserName_Old { get; set; }
    [MaxLength(50)]
    public string? UserName_New { get; set; }
    public DateTime SystemDate { get; set; }
    [MaxLength(50)]
    public string? AuditUserId { get; set; }
}
