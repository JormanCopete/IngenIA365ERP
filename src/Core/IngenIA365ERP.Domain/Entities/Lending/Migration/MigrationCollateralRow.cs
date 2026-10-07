using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCollaterals</c> (feature 013, K1; data-model §29.3, parte <c>collaterals</c>): una garantía y el crédito que
/// respalda (una garantía puede respaldar varios: una fila por par). Hecho sin diff de auditoría. Al activar nace
/// <c>LND_Collaterals</c>.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationCollateralRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    /// <summary>Llave de la garantía en el archivo; agrupa las filas de una misma garantía.</summary>
    public string CollateralKey { get; init; } = string.Empty;

    public string SourceLoanNumber { get; init; } = string.Empty;

    public CollateralKind CollateralKind { get; init; }

    public int OwnerPersonId { get; init; }

    public bool IsMortgage { get; init; }

    public decimal CommercialValue { get; init; }

    public DateOnly? AppraisalDate { get; init; }

    public string? PolicyNumber { get; init; }

    public DateOnly? PolicyExpiresOn { get; init; }

    public string Description { get; init; } = string.Empty;
}
