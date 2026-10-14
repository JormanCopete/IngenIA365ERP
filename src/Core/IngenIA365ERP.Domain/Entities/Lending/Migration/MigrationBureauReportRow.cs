using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationBureauReports</c> (feature 013, K1; data-model §29.3, parte <c>bureau-reports</c>; plantillas.md §5.3.14): el último reporte
/// vigente de un crédito a una central; llave crédito + central. Hecho sin diff de auditoría. Un reporte negativo vigente llena al activar
/// <c>LND_PriorNotices.MigratedReportedNegativeSince</c> del episodio; uno sin aviso previo lo retira el primer archivo de esa central (FR-123).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationBureauReportRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>El crédito de <c>loans</c> o <c>charged-off</c>.</summary>
    public string SourceLoanNumber { get; init; } = string.Empty;

    /// <summary>Código de la central (<c>central</c>, hasta 20).</summary>
    public string BureauCode { get; init; } = string.Empty;

    /// <summary>Último período reportado (<c>ultimoPeriodoReportado</c>, mes <c>AAAA-MM</c>).</summary>
    public short LastReportedYear { get; init; }

    public byte LastReportedMonth { get; init; }

    /// <summary>La novedad tal como se reportó (<c>novedadReportada</c>).</summary>
    public string LastReportedStatus { get; init; } = string.Empty;

    /// <summary><c>datoNegativo</c>.</summary>
    public bool IsNegative { get; init; }

    /// <summary>Con negativo: el episodio (<c>episodio</c>).</summary>
    public DateOnly? EpisodeStartDate { get; init; }

    /// <summary>Con negativo: si tuvo aviso previo (<c>conAvisoPrevio</c>); «no» lo retira el primer archivo de esa central.</summary>
    public bool? WithPriorNotice { get; init; }
}
