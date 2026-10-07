using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationBlocks</c> (feature 013, K1; data-model §29.3, parte <c>blocks</c>, C64): un bloqueo de una persona. Con
/// <c>Deceased</c> trae la aseguradora y el estado de la reclamación; con <c>Insolvency</c>, el estado del proceso. Hecho sin diff
/// de auditoría. Al activar nacen <c>LND_DebtorBlocks</c> y, según la causa, <c>LND_InsuranceClaims</c> o
/// <c>LND_InsolvencyProceedings</c>.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationBlockRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public int PersonId { get; init; }

    public DebtorBlockReason BlockReason { get; init; }

    public DateOnly Since { get; init; }

    /// <summary>El crédito embargado, en un embargo de un crédito.</summary>
    public string? SourceLoanNumber { get; init; }

    public DateOnly? Until { get; init; }

    public string SupportReference { get; init; } = string.Empty;

    /// <summary>Con <c>Deceased</c>: la aseguradora del seguro de vida deudores.</summary>
    public int? InsurerPersonId { get; init; }

    /// <summary>Con <c>Deceased</c>; nulo = <c>InProcess</c>.</summary>
    public InsuranceClaimStatus? InsuranceClaimStatus { get; init; }

    /// <summary>Con <c>Insolvency</c>; nulo = <c>Negotiating</c>.</summary>
    public InsolvencyProceedingStatus? InsolvencyStatus { get; init; }
}
