using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCreditLimits</c> (feature 013, K1; data-model §29.3, parte <c>credit-limits</c>): un cupo de una persona
/// (consolidado, por línea o rotativo). Hecho sin diff de auditoría. Al activar nace <c>LND_CreditLimits</c>; lo otorgado a mano
/// antes prevalece y se revalida (<c>Cartera.CupoPorRevalidar</c>).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationCreditLimitRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public int PersonId { get; init; }

    public CreditLimitKind LimitKind { get; init; }

    /// <summary>Sólo en el cupo por línea.</summary>
    public string? CreditLineCode { get; init; }

    public decimal Amount { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateOnly? ValidTo { get; init; }

    public string Reason { get; init; } = string.Empty;
}
