using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationLoadParts</c> (feature 013, K1; data-model §29.2, C64): una fila <b>por archivo</b> (tanda) de una parte de la
/// plantilla <c>lending.migration.{parte}</c>. Cada tanda es todo o nada para sí misma. No es hecho: importar con
/// <c>replace=true</c> pasa las tandas anteriores de la parte a <see cref="MigrationPartStatus.Superseded"/>, y sus filas de
/// preparación se conservan y dejan de contar.
/// </summary>
public class MigrationLoadPart : AuditableEntity
{
    public int MigrationLoadId { get; set; }

    /// <summary>La parte (§29.3): <c>loans</c>, <c>installments</c>, <c>balances</c>, …</summary>
    public string PartKey { get; set; } = string.Empty;

    /// <summary>Consecutivo de la tanda dentro de la parte.</summary>
    public int BatchNumber { get; set; }

    public MigrationPartStatus Status { get; set; } = MigrationPartStatus.Imported;

    public string FileName { get; set; } = string.Empty;

    /// <summary>El mismo archivo otra vez (con la tanda <c>Imported</c>) responde «sin cambio».</summary>
    public string FileSha256 { get; set; } = string.Empty;

    public int RowCount { get; set; }

    public int ImportedByUserId { get; set; }

    public DateTime ImportedAt { get; set; }

    /// <summary>JSON: avisos de la revisión previa (los errores impiden importar).</summary>
    public string? ReviewSummaryJson { get; set; }
}
