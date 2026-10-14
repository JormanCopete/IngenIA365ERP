using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationChargedOff</c> (feature 013, K1; data-model §29.3, parte <c>charged-off</c>): un crédito castigado en el sistema
/// de origen con sus saldos. Hecho sin diff de auditoría. Al activar nace el crédito <c>WrittenOff</c> con saldo inicial en
/// control: sus recuperaciones funcionan desde K1. Columnas de plantillas.md §5.3.8.
/// </summary>
[SinDiffDeAuditoria]
public class MigrationChargedOffRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public int PersonId { get; init; }

    public string CreditLineCode { get; init; } = string.Empty;

    /// <summary>La oficina del crédito (<c>oficina</c>): la sucursal vinculada a una oficina (T13).</summary>
    public Guid OfficePublicId { get; init; }

    public DateOnly? DisbursementDate { get; init; }

    public DateOnly WrittenOffOn { get; init; }

    public decimal PrincipalBalance { get; init; }

    public decimal InterestBalance { get; init; }

    public decimal OtherBalance { get; init; }

    /// <summary>Soporte histórico del castigo como dato, sólo en la preparación (T64 rige lo que se decide en el ERP, C64).</summary>
    public string HistoricalApprovalReference { get; init; } = string.Empty;
}
