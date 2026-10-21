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

    /// <summary>Una tanda viva cuenta en la carga; una reemplazada conserva sus filas y deja de contar.</summary>
    public bool Viva => Status == MigrationPartStatus.Imported && !IsDeleted;

    /// <summary>Una tanda nueva de la parte (T0740): la siguiente de <paramref name="anteriores"/> tandas de la misma parte.</summary>
    public static MigrationLoadPart Nueva(int carga, string parte, int anteriores, string archivo, string huella, int filas, int usuario, DateTime ahora,
        string? resumenJson) => new()
    {
        MigrationLoadId = carga,
        PartKey = parte,
        BatchNumber = anteriores + 1,
        Status = MigrationPartStatus.Imported,
        FileName = archivo.Length <= 260 ? archivo : archivo[..260],
        FileSha256 = huella,
        RowCount = filas,
        ImportedByUserId = usuario,
        ImportedAt = ahora,
        ReviewSummaryJson = resumenJson,
    };

    /// <summary><c>replace=true</c>: la tanda pasa a <c>Superseded</c> (sus filas se conservan y dejan de contar).</summary>
    public void Reemplazar()
    {
        if (Status != MigrationPartStatus.Imported) throw new InvalidOperationException("La tanda ya fue reemplazada.");
        Status = MigrationPartStatus.Superseded;
    }
}
