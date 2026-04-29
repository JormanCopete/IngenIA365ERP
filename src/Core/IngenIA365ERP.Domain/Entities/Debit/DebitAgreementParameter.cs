using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Debit;

/// <summary>Maps to [dbo].[DEB_AgreementParameters] (deb_parconv).</summary>
public class DebitAgreementParameter : AuditableEntity
{
    [MaxLength(10)]
    public string AgreementCode { get; set; } = string.Empty;

    public int TokenRS { get; set; }

    [MaxLength(100)]
    public string? Name { get; set; }
}
