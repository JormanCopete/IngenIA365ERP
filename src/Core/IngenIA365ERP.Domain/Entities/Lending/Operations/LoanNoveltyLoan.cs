using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_LoanNoveltyLoans</c> (feature 013, K3, T0871; data-model §25.2; FR-090): un crédito que toca la novedad y su papel —el modificado,
/// cada crédito que recoge una refinanciación y el enlazado que resulta de una novación o refinanciación—. Único por novedad y crédito. No es
/// hecho, pero no cambia: sólo se agrega. (nuevo)
/// </summary>
public class LoanNoveltyLoan : AuditableEntity
{
    public int LoanNoveltyId { get; init; }

    public int LoanId { get; init; }

    /// <summary>Navegación para enlazar en el mismo guardado el crédito enlazado todavía sin Id (papel <c>Resulting</c>).</summary>
    public IngenIA365ERP.Domain.Entities.Lending.Loans.Loan? Loan { get; init; }

    public NoveltyLoanRole Role { get; init; }
}
