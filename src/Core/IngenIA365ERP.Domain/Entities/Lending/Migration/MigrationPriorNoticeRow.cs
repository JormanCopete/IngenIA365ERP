using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationPriorNotices</c> (feature 013, K1; data-model §29.3, parte <c>prior-notices</c>): una comunicación previa al
/// reporte negativo ya enviada en el sistema de origen. Hecho sin diff de auditoría. Al activar nace <c>LND_PriorNotices</c>
/// (columnas <c>Migrated*</c>).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationPriorNoticeRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public DateOnly EpisodeStartDate { get; init; }

    public string CommunicationNumber { get; init; } = string.Empty;

    public DateOnly SentOn { get; init; }

    /// <summary>Canal (enum de Compliance como texto).</summary>
    public string Channel { get; init; } = string.Empty;

    public string? EvidenceReference { get; init; }
}
