using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting;

namespace IngenIA365ERP.Domain.Entities.Treasury;

/// <summary>Maps to [dbo].[TRS_Concepts] (TES_CPTOS).</summary>
public class TreasuryConcept : AuditableEntity
{
    [MaxLength(5)]
    public string ConceptCode { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ShortName { get; set; }

    [MaxLength(5)]
    public string? ConceptType { get; set; }

    // Feature 009 (R16): el concepto de tesoreria no tenia cuentas; los cheques y facturas contabilizan con estas.
    public int? DebitAccountId { get; set; }
    public ChartOfAccount? DebitAccount { get; set; }
    public int? CreditAccountId { get; set; }
    public ChartOfAccount? CreditAccount { get; set; }
}
