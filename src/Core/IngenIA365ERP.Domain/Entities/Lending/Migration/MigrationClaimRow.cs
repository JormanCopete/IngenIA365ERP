using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationClaims</c> (feature 013, K1; data-model §29.3, parte <c>claims</c>; plantillas.md §5.3.15): un reclamo sobre el reporte a
/// centrales de un crédito, en trámite al corte; llave crédito + central + radicación. Hecho sin diff de auditoría. La persona no viene en la
/// plantilla: es el deudor del crédito, que se resuelve entre partes. Al activar nace <c>LND_CreditReportClaims</c> en <c>InProcess</c>.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationClaimRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public string BureauCode { get; init; } = string.Empty;

    public CreditReportClaimKind Kind { get; init; }

    /// <summary>Fecha de radicación (<c>fechaDeRadicacion</c>): el plazo legal corre desde aquí.</summary>
    public DateOnly ReceivedOn { get; init; }

    /// <summary><c>prorrogado</c>.</summary>
    public bool IsExtended { get; init; }

    public string? Description { get; init; }

    /// <summary>El deudor del crédito, si ya se resolvió; nula en la preparación de la parte.</summary>
    public int? PersonId { get; init; }
}
