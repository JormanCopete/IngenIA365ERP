using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_MigrationCoDebtors</c> (feature 013, K1; data-model §29.3, parte <c>co-debtors</c>): un codeudor (o la parte con su rol)
/// de un crédito de origen. Hecho sin diff de auditoría. Al activar nace <c>LND_LoanParties</c>. Llave crédito +
/// codeudor (plantillas.md §5.3.6).
/// </summary>
[SinDiffDeAuditoria]
public class MigrationCoDebtorRow : AuditableEntity, IHechoInmutable
{
    public int MigrationLoadPartId { get; init; }

    public int RowNumber { get; init; }

    public string SourceLoanNumber { get; init; } = string.Empty;

    public int CoDebtorPersonId { get; init; }

    /// <summary><c>CoDebtor</c> por defecto o <c>Guarantor</c> (<c>rol</c>).</summary>
    public CreditPartyRole Role { get; init; } = CreditPartyRole.CoDebtor;

    /// <summary>Desde cuándo es parte del crédito (<c>desde</c>); nula = desde el desembolso.</summary>
    public DateOnly? Since { get; init; }
}
