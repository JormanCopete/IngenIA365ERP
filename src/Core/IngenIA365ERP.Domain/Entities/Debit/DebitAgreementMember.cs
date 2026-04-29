using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Debit;

/// <summary>Maps to [dbo].[DEB_AgreementMembers] (deb_enpactors).</summary>
public class DebitAgreementMember : AuditableEntity
{
    public DateOnly ProcessDate { get; set; }

    [MaxLength(20)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(5)]
    public string VoucherCode { get; set; } = string.Empty;

    public long DocumentNumber { get; set; }
    public bool IsApplied { get; set; }
}
