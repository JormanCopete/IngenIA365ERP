using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Loans;

/// <summary>
/// <c>LND_LoanParties</c> (feature 013; data-model §4.2; FR-036, FR-043, FR-061, FR-092): codeudores y avalistas del crédito (el
/// deudor está en <see cref="Loan"/>). Cuenta en el endeudamiento consolidado como codeudor y en la consulta «como codeudor». No es
/// hecho. Forma sin comportamiento.
/// </summary>
public class LoanParty : AuditableEntity
{
    public int LoanId { get; set; }

    public int PersonId { get; set; }

    /// <summary><c>CoDebtor</c> o <c>Guarantor</c>.</summary>
    public CreditPartyRole Role { get; set; }

    public DateOnly JoinedOn { get; set; }

    public int? SourceApplicationPartyId { get; set; }

    /// <summary>Liberado tras la firmeza de la cancelación (FR-082).</summary>
    public DateOnly? ReleasedOn { get; set; }

    /// <summary>Operación que lo liberó (<c>LND_LoanOperations</c>, bigint).</summary>
    public long? ReleaseOperationId { get; set; }

    /// <summary>K3: garante que no consiente la novedad (FR-092).</summary>
    public DateOnly? ExtinguishedOn { get; set; }

    public string? ExtinguishReason { get; set; }

    public int? ExtinguishDecisionId { get; set; }
}
