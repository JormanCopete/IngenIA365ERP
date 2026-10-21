using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocumentLoans</c> (feature 013, C.3; data-model §27.2): cada crédito que cubre un <see cref="IssuedLendingDocument"/>,
/// con la última operación y transacción consideradas y el saldo al corte. Hecho de sólo inserción (<see cref="IHechoInmutable"/>).
/// </summary>
public class IssuedDocumentLoan : AuditableEntity, IHechoInmutable
{
    public int IssuedDocumentId { get; init; }

    public int LoanId { get; init; }

    public long? LastOperationId { get; init; }

    public long? LastTransactionId { get; init; }

    public decimal? BalanceAtCutoff { get; init; }

    /// <summary>El pago que canceló el crédito (firmeza del paz y salvo).</summary>
    public long? PayoffOperationId { get; init; }
}
