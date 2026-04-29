using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending;

/// <summary>Maps to [dbo].[LND_DepositSeals].</summary>
public class DepositSeal : AuditableEntity
{
    public long AccountNumber { get; set; }
    public byte[]? SealImage { get; set; }
}
