using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_MoneyLaunderingDeclarations].</summary>
public class MoneyLaunderingDeclaration : AuditableEntityLong
{
    public long DeclarationId { get; set; }
    public DateTime DeclarationDate { get; set; }
    [MaxLength(20)]
    public string PersonCode { get; set; } = string.Empty;
    [MaxLength(40)]
    public string EconomicActivity { get; set; } = string.Empty;
    [MaxLength(5)]
    public string VoucherType { get; set; } = string.Empty;
    public long DocumentNumber { get; set; }
    public int TransactionSequence { get; set; }
    [MaxLength(3)]
    public string OperationType { get; set; } = string.Empty;
    [MaxLength(3)]
    public string OperationDetail { get; set; } = string.Empty;
    [MaxLength(25)]
    public string AffectedProduct { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [MaxLength(20)]
    public string ClientId { get; set; } = string.Empty;
    [MaxLength(2)]
    public string ClientIdType { get; set; } = string.Empty;
    [MaxLength(120)]
    public string ClientName { get; set; } = string.Empty;
    [MaxLength(120)]
    public string ClientSurname { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Address { get; set; } = string.Empty;
    [MaxLength(40)]
    public string? Phone { get; set; }
    public string? Remarks { get; set; }
}
