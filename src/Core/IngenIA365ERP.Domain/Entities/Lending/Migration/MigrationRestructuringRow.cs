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

    /// <summary>Números de origen de los créditos antecesores, separados por coma.</summary>
    public string? PredecessorLoanNumbers { get; init; }
}
