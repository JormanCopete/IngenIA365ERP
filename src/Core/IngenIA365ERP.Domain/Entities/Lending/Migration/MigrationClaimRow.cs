using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationClaims</c> (feature 013, K1; data-model §29.3, parte <c>claims</c>): un reclamo de una persona sobre el reporte
/// a centrales, en trámite al corte. Hecho sin diff de auditoría. Al activar nace <c>LND_CreditReportClaims</c> en
/// <c>InProcess</c>.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationClaimRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public int PersonId { get; init; }

    public CreditReportClaimKind Kind { get; init; }

    public DateOnly ReceivedOn { get; init; }

    public string? SourceLoanNumber { get; init; }

    public string? BureauCode { get; init; }

    public DateOnly? DueDate { get; init; }

    public string Description { get; init; } = string.Empty;
}
