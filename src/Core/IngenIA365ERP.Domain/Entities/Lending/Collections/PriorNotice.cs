using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_PriorNotices</c> (feature 013, C.3; data-model §26.11; FR-123, FR-137, SC-012; T77): aviso previo al reporte negativo,
/// por obligación, titular y <b>episodio</b> de mora. Cada comunicación es un <see cref="DebtorMessage"/> con propósito
/// <c>PriorNotice</c>; el estado (<c>PriorNoticeStatus</c>) se deriva al leer. Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>); puede venir migrado.
/// </summary>
public class PriorNotice : AuditableEntity, IHechoInmutable
{
    public int LoanId { get; init; }

    /// <summary>Titular avisado.</summary>
    public int PersonId { get; init; }

    /// <summary>Vencimiento de la primera cuota impaga del episodio.</summary>
    public DateOnly EpisodeStartDate { get; init; }

    /// <summary>1, o 2 en días distintos bajo <c>AvisoPrevio.UmbralDosComunicaciones</c>.</summary>
    public byte RequiredCommunications { get; init; }

    /// <summary><c>AvisoPrevio.DiasMinimos</c> vigente (piso nacional 20), sellado.</summary>
    public int MinimumDays { get; init; }

    /// <summary>Umbral vigente, sellado.</summary>
    public decimal? ThresholdAmount { get; init; }

    /// <summary>Aviso migrado.</summary>
    public int? MigrationLoadId { get; init; }

    public DateOnly? MigratedFirstSentOn { get; init; }

    public DateOnly? MigratedSecondSentOn { get; init; }

    public string? MigratedEvidenceReference { get; init; }

    /// <summary>Reporte vigente en centrales del episodio (C64).</summary>
    public DateOnly? MigratedReportedNegativeSince { get; init; }

    public string? MigratedBureauCode { get; init; }
}
