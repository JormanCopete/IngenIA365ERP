using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>Maps to [dbo].[INV_Invoices] (inv_facturas).</summary>
public class InventoryInvoice : AuditableEntityLong
{
    [MaxLength(10)]
    public string InvoiceCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Resolution { get; set; }

    public DateOnly? ResolutionDate { get; set; }
    public int VatRegime { get; set; }

    [MaxLength(10)]
    public string? Prefix { get; set; }

    [MaxLength(20)]
    public string? InitialNumber { get; set; }

    [MaxLength(20)]
    public string? FinalNumber { get; set; }

    public long InvoiceConsecutive { get; set; }

    [MaxLength(5)]
    public string? EmployeeVoucherCode { get; set; }

    public int? EmployeeCreditLineId { get; set; }

    [MaxLength(2)]
    public string? EmployeeDeductionType { get; set; }

    public int? EmployeeTerm { get; set; }

    [MaxLength(5)]
    public string? EmployerVoucherCode { get; set; }

    public int? EmployerCreditLineId { get; set; }

    [MaxLength(2)]
    public string? EmployerDeductionType { get; set; }

    public int? EmployerTerm { get; set; }
    public int? AdjustmentTypeId { get; set; }

    [MaxLength(5)]
    public string? PortfolioVoucherCode { get; set; }

    public int? CreditLineId { get; set; }

    [MaxLength(2)]
    public string? DeductionType { get; set; }

    public int? Term { get; set; }
    public int UpdatesCosts { get; set; }
    public bool PrintsBonusTickets { get; set; }
    public bool OpensRegister { get; set; }

    [MaxLength(2)]
    public string? CommissionGroup { get; set; }

    public decimal CommissionWithholdingRate { get; set; }
    public bool PreventCostUtility { get; set; }
    public bool VoucherConsecutive { get; set; }
    public bool SingleDiscountOnly { get; set; }
    public bool VatWithDiscount { get; set; }

    [MaxLength(5)]
    public string? ThirdPartySpecialLineId { get; set; }

    [MaxLength(5)]
    public string? ThirdPartyCreditLineId { get; set; }

    [MaxLength(5)]
    public string? ThirdPartySpecialVoucher { get; set; }

    [MaxLength(5)]
    public string? ThirdPartyVoucherCode { get; set; }
}
