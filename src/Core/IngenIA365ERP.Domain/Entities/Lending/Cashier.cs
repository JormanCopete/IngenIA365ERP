using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_Cashiers].</summary>
public class Cashier : AuditableEntity
{
    [MaxLength(20)]
    public string CashierCode { get; set; } = string.Empty;
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;
    [MaxLength(5)]
    public string VoucherType { get; set; } = string.Empty;
    [MaxLength(2)]
    public string? Status { get; set; }
    public DateOnly? OpenDate { get; set; }
    public int VoucherConsecutive { get; set; }
    [MaxLength(50)]
    public string Description { get; set; } = string.Empty;
    public int PasswordTimeout { get; set; }
    [MaxLength(60)]
    public string PrinterName { get; set; } = string.Empty;
    public int TransactionType { get; set; }
}
