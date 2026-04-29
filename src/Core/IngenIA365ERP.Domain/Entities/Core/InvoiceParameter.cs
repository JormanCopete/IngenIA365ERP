using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_InvoiceParameters] (sys_parfactura).
/// </summary>
public class InvoiceParameter : AuditableEntity
{
    [MaxLength(10)]
    public string? LegacyCode { get; set; }

    [MaxLength(20)]
    public string? Tag415 { get; set; }

    [MaxLength(4)]
    public string? Tag8020 { get; set; }

    [MaxLength(4)]
    public string? Tag3900 { get; set; }

    [MaxLength(4)]
    public string? Tag96 { get; set; }

    [MaxLength(4)]
    public string? BarcodeType { get; set; }

    [MaxLength(2)]
    public string? InvoicePrintParam { get; set; }

    [MaxLength(4)]
    public string? InvoiceGroup { get; set; }
}
