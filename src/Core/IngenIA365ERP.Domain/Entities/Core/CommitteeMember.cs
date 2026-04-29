using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Maps to [dbo].[COR_CommitteeMembers] (cop_comiteasoc).
/// Many-to-many: PersonId (codigoter) - CommitteeId (idComite).
/// </summary>
public class CommitteeMember : AuditableEntity
{
    public int PersonId { get; set; }

    public int CommitteeId { get; set; }

    [MaxLength(100)]
    public string? LegacyUser { get; set; }

    public DateTime? LegacyDate { get; set; }

    // Navigation properties
    public Person Person { get; set; } = null!;
    public Committee Committee { get; set; } = null!;
}
