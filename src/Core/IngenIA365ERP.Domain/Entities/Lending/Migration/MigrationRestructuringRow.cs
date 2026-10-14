using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationRestructurings</c> (feature 013, K1; data-model §29.3, parte <c>restructurings</c>): marcas de reestructuración
/// o modificación de un crédito de origen. Hecho sin diff de auditoría. Al activar marca el crédito y alimenta la regla de
/// reestructurados.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationRestructuringRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    /// <summary><c>Restructuring</c> (5) o <c>Modification</c> (6): columna <c>clase</c> de la plantilla (C97).</summary>
    public TermsVersionCause Cause { get; init; }

    public int RestructuringCount { get; init; }

    public DateOnly LastRestructuredOn { get; init; }

    public RatingCategory RatingAtRestructuring { get; init; }

    public int ConsecutivePaidInstallments { get; init; }

    /// <summary>El crédito del que nació (<c>creditoAnterior</c>); lo sigue el historial de mora (T49) aunque no esté en la carga.</summary>
    public string? PredecessorLoanNumber { get; init; }
}
