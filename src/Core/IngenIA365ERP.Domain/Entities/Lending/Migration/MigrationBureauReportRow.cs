using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationBureauReports</c> (feature 013, K1; data-model §29.3, parte <c>bureau-reports</c>): el último estado reportado
/// de un crédito de origen a una central. Hecho sin diff de auditoría. Al activar llena
/// <c>LND_PriorNotices.MigratedReportedNegativeSince</c> del episodio.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationBureauReportRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public string BureauCode { get; init; } = string.Empty;

    public DateOnly? EpisodeStartDate { get; init; }

    public DateOnly? ReportedNegativeSince { get; init; }

    public string LastReportedStatus { get; init; } = string.Empty;
}
