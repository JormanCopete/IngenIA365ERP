using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_InvoiceDetails].</summary>
public class InvoiceDetail : AuditableEntity
{
    [MaxLength(2)]
    public string DetailCode { get; set; } = string.Empty;
    public string? DetailText { get; set; }
}
